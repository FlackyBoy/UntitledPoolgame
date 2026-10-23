# Bot Discord `/feature`

Dans le salon Discord dédié, `/feature titre:… details:…` crée une page dans la base Notion « Tâches — UntitledPoolGame » avec le statut **À trier**, l'auteur et le lien du message. Claude range ensuite ces pages (action `inbox` du skill `notion-todo`) : tâche dans la TODO, ou idée dans les Pistes.

Le bot est un Worker Cloudflare (offre gratuite) : Discord lui envoie chaque commande par HTTP, il répond « réfléchit… », crée la page Notion, puis remplace sa réponse par la confirmation.

**Aucun jeton dans le dépôt.** Le jeton Notion et la clé publique Discord vont dans les secrets du Worker.

## Fichiers

- `src/index.js` — le Worker (vérification de la signature Discord, réponse différée, appel à l'API Notion).
- `wrangler.toml` — configuration du Worker : nom, ID de la base Notion, liste facultative des personnes autorisées.
- `command.json` — définition de la commande `/feature`, envoyée une fois à Discord.

## Mise en place (une fois)

### 1. Discord : l'application et le bot
1. Sur https://discord.com/developers/applications, cliquer sur **New Application** et la nommer (ex. « Pool Idées »).
2. Dans **General Information**, noter l'**Application ID** et la **Public Key**.
3. Dans **Bot**, cliquer sur **Reset Token** et noter le jeton. Il ne sert qu'à enregistrer la commande (étape 4) : ne le donner à personne, ne pas le coller dans le dépôt.
4. Dans **OAuth2 > URL Generator**, cocher `applications.commands` et `bot`, ouvrir l'URL générée et inviter le bot sur le serveur (il faut le droit « Gérer le serveur »).

### 2. Notion : l'intégration
1. Sur https://www.notion.so/my-integrations, créer une intégration **interne** et noter son **jeton** (`ntn_…`).
2. Ouvrir la base « Tâches — UntitledPoolGame », menu **•••** > **Connexions** > ajouter l'intégration.
3. La base doit avoir le statut **À trier** et les champs **Auteur** (texte) et **Lien Discord** (URL) : Claude les ajoute.

### 3. Cloudflare : le Worker
**Sans rien installer** (tableau de bord) :
1. Sur https://dash.cloudflare.com, **Workers & Pages** > **Create** > **Create Worker**, le nommer `untitledpoolgame-feature`, **Deploy**.
2. **Edit code** : remplacer le code par le contenu de `src/index.js`, puis **Deploy**.
3. **Settings > Variables and Secrets** :
   - `DISCORD_PUBLIC_KEY` (Secret) = la Public Key de l'étape 1 ;
   - `NOTION_TOKEN` (Secret) = le jeton de l'étape 2 ;
   - `NOTION_DATABASE_ID` (Text) = `2f7670d7d35343a0bca9d3c4d3e25cd9` ;
   - `ALLOWED_ROLE_IDS` (Text) = les rôles autorisés, voir *Qui peut l'utiliser*.
4. Noter l'adresse du Worker (`https://untitledpoolgame-feature.<compte>.workers.dev`).

**Avec Node.js** (dans ce dossier) :
```sh
npx wrangler login
npx wrangler deploy
npx wrangler secret put DISCORD_PUBLIC_KEY
npx wrangler secret put NOTION_TOKEN
```

### 4. Brancher Discord sur le Worker
1. Dans le portail Discord, **General Information** > **Interactions Endpoint URL** : coller l'adresse du Worker, **Save**. Discord vérifie l'adresse tout de suite : si ça échoue, la Public Key du Worker est fausse.
2. Enregistrer la commande (depuis ce dossier, dans Git Bash) :
   ```sh
   curl -X POST "https://discord.com/api/v10/applications/<APPLICATION_ID>/guilds/<SERVEUR_ID>/commands" \
     -H "Authorization: Bot <JETON_DU_BOT>" -H "Content-Type: application/json" \
     --data @command.json
   ```
   L'ID du serveur s'obtient par clic droit sur le serveur > **Copier l'identifiant du serveur** (activer d'abord le *Mode développeur* dans Paramètres > Avancés). Enregistrée sur le serveur, la commande apparaît tout de suite.
3. Limiter la commande au salon : **Paramètres du serveur > Intégrations >** le bot **> /feature** : retirer *Tous les salons* et ajouter le salon dédié.

## Qui peut l'utiliser

Seuls les membres qui ont l'un des rôles de `ALLOWED_ROLE_IDS` (identifiants séparés par des virgules). Pour copier l'identifiant d'un rôle : *Mode développeur* activé, **Paramètres du serveur > Rôles**, clic droit sur le rôle > **Copier l'identifiant du rôle**. Ajouter ou retirer un rôle = changer la variable dans Cloudflare, sans redéployer le code.

- Liste vide = personne : le bot refuse tant que la variable n'est pas remplie.
- Les autres reçoivent un message que seuls eux voient.
- Pour qu'ils ne voient même pas la commande, faire la même restriction dans **Intégrations** (étape 4.3) : *@everyone* décoché, les rôles ajoutés. Le Worker vérifie quand même, au cas où ce réglage serait oublié.

## Tester

1. Dans le salon, taper `/feature`, remplir `titre` (et `details`), valider.
2. Le bot répond « réfléchit… », puis « 💡 **titre** — envoyé dans Notion… ».
3. Dans Notion, une page avec ce titre, le statut *À trier*, l'auteur et le lien du message.
4. En cas d'erreur, le bot l'affiche (« ⚠️ … Notion 404 » : l'intégration n'est pas connectée à la base ; « Notion 400 » : un champ manque). Le détail est dans Cloudflare > le Worker > **Logs**.
