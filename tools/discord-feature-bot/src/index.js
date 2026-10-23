// Discord bot "/feature": sends an idea or request from a Discord channel to the
// Notion "Tâches" database with the status "À trier", so Claude can sort it later
// (task in TODO.md, or idea in pistes.md).
//
// Runs as a Cloudflare Worker that receives Discord interactions over HTTP: no
// gateway connection, so it fits the free plan and has no dependency.
//
// Secrets (never in the repo): DISCORD_PUBLIC_KEY, NOTION_TOKEN.
// Variables (wrangler.toml): NOTION_DATABASE_ID, ALLOWED_ROLE_IDS.

const NOTION_VERSION = "2022-06-28";
const NOTION_TEXT_MAX = 2000; // Notion limit per rich_text item.

const InteractionType = { PING: 1, APPLICATION_COMMAND: 2 };
const ResponseType = { PONG: 1, CHANNEL_MESSAGE: 4, DEFERRED_CHANNEL_MESSAGE: 5 };
const EPHEMERAL = 64;

export default {
  async fetch(request, env, ctx) {
    if (request.method !== "POST") return new Response("UntitledPoolGame /feature bot", { status: 200 });

    // Discord rejects the endpoint URL unless invalid signatures are refused.
    const body = await request.text();
    if (!(await isValidSignature(request, body, env.DISCORD_PUBLIC_KEY))) {
      return new Response("Bad signature", { status: 401 });
    }

    const interaction = JSON.parse(body);
    if (interaction.type === InteractionType.PING) return json({ type: ResponseType.PONG });

    if (interaction.type === InteractionType.APPLICATION_COMMAND && interaction.data.name === "feature") {
      const user = interaction.member?.user ?? interaction.user;
      if (!hasAllowedRole(interaction.member, env.ALLOWED_ROLE_IDS)) {
        return reply("Tu n'as pas de rôle qui permet d'envoyer une demande.", EPHEMERAL);
      }

      // Discord waits 3 s at most for the first answer; Notion can be slower, so
      // answer "thinking…" now and finish in the background (token valid 15 min).
      ctx.waitUntil(handleFeature(interaction, user, env));
      return json({ type: ResponseType.DEFERRED_CHANNEL_MESSAGE });
    }

    return reply("Commande inconnue.", EPHEMERAL);
  },
};

async function handleFeature(interaction, user, env) {
  const options = Object.fromEntries((interaction.data.options ?? []).map((o) => [o.name, o.value]));
  const title = String(options.titre ?? "").trim();
  const details = String(options.details ?? "").trim();
  const author = user.global_name || user.username;

  let page;
  try {
    page = await createNotionPage(env, title, details, author);
  } catch (error) {
    console.error(error);
    await editOriginal(interaction, { content: `⚠️ La demande n'a pas pu être envoyée dans Notion (${error.message}).` });
    return;
  }

  const message = await editOriginal(interaction, {
    content: `💡 **${title}** — envoyé dans Notion par ${author}, en attente de tri.` + (details ? `\n> ${details.replace(/\n/g, "\n> ")}` : ""),
    allowed_mentions: { parse: [] },
  });

  // The message link only exists once the reply is posted, hence a second write.
  // The page is already saved, so a failure here is only logged.
  if (interaction.guild_id && message?.id) {
    const link = `https://discord.com/channels/${interaction.guild_id}/${interaction.channel_id}/${message.id}`;
    await notion(env, `pages/${page.id}`, "PATCH", { properties: { "Lien Discord": { url: link } } }).catch(console.error);
  }
}

function createNotionPage(env, title, details, author) {
  const children = details
    ? chunks(details, NOTION_TEXT_MAX).map((text) => ({
        object: "block",
        type: "paragraph",
        paragraph: { rich_text: [{ type: "text", text: { content: text } }] },
      }))
    : [];

  return notion(env, "pages", "POST", {
    parent: { database_id: env.NOTION_DATABASE_ID },
    properties: {
      Nom: { title: [{ text: { content: title.slice(0, NOTION_TEXT_MAX) } }] },
      Statut: { status: { name: "À trier" } },
      Auteur: { rich_text: [{ text: { content: author } }] },
    },
    children,
  });
}

async function notion(env, path, method, payload) {
  const response = await fetch(`https://api.notion.com/v1/${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${env.NOTION_TOKEN}`,
      "Notion-Version": NOTION_VERSION,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });
  const data = await response.json();
  if (!response.ok) throw new Error(`Notion ${response.status} : ${data.message ?? "erreur"}`);
  return data;
}

async function editOriginal(interaction, payload) {
  const url = `https://discord.com/api/v10/webhooks/${interaction.application_id}/${interaction.token}/messages/@original`;
  const response = await fetch(url, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!response.ok) {
    console.error(`Discord ${response.status}: ${await response.text()}`);
    return null;
  }
  return response.json();
}

async function isValidSignature(request, body, publicKeyHex) {
  const signature = request.headers.get("X-Signature-Ed25519");
  const timestamp = request.headers.get("X-Signature-Timestamp");
  if (!signature || !timestamp || !publicKeyHex) return false;
  try {
    const key = await crypto.subtle.importKey("raw", hexToBytes(publicKeyHex), { name: "Ed25519" }, false, ["verify"]);
    return await crypto.subtle.verify("Ed25519", key, hexToBytes(signature), new TextEncoder().encode(timestamp + body));
  } catch {
    return false;
  }
}

// Checked here rather than only in Discord's server settings > Integrations, so a
// forgotten setting there cannot open the command to everyone. An empty list
// refuses everyone. The command is registered for servers only, so member exists.
function hasAllowedRole(member, allowedList) {
  const ids = (allowedList ?? "").split(",").map((s) => s.trim()).filter(Boolean);
  return (member?.roles ?? []).some((role) => ids.includes(role));
}

function hexToBytes(hex) {
  const bytes = new Uint8Array(hex.length / 2);
  for (let i = 0; i < bytes.length; i++) bytes[i] = parseInt(hex.substr(i * 2, 2), 16);
  return bytes;
}

function chunks(text, size) {
  const parts = [];
  for (let i = 0; i < text.length; i += size) parts.push(text.slice(i, i + size));
  return parts;
}

function reply(content, flags = 0) {
  return json({ type: ResponseType.CHANNEL_MESSAGE, data: { content, flags } });
}

function json(data) {
  return new Response(JSON.stringify(data), { headers: { "Content-Type": "application/json" } });
}
