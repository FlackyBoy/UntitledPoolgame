using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace UntitledPoolGame.Pool
{
    // 2 v 2: each player marked with the team's colour (J1 and J3 in J1's,
    // J2 and J4 in J2's), to tell teammates from opponents at a glance.
    // (Tried and dropped: an outline, and the suit recoloured in the team's
    // colour, which also took the hair and looked cheap.)
    // - Chevron (Team Chevrons): a point over the head, turned to face each
    //   camera as it draws, hidden behind what hides it (depth-tested), not
    //   drawn for its own player's camera.
    // - Ring (Team Rings, off): on the ground at the feet.
    // Follows the body the player plays: the shrunk one in the Shooter
    // modes, the full-size one otherwise; none in the Sumo modes, where the
    // player's ball already wears the team's colour. Refreshed at each match
    // start (PoolMatchRules.StartMatch).
    public class PlayerTeamMarks : MonoBehaviour
    {

        // Head bone to the top of the hair, in body heights (big heads).
        private const float SkullAboveHeadBone = 0.13f;
        // The sprite's tip, below its centre, in sprite widths (ChevronSprite).
        private const float ChevronTipBelowCentre = 0.425f;

        private static PlayerTeamMarks instance;
        private static Sprite ringSprite, chevronSprite;
        private static Material unlit;

        private class Marked
        {
            public Transform body;
            public CharacterController controller;
            public Transform head, neck;
            public SpriteRenderer ring, chevron;
        }

        private readonly Marked[] players = new Marked[PoolMatchRules.MaxPlayers];
        // Each camera's player (-1: not a player's view), looked up once per match.
        private readonly Dictionary<Camera, int> playerOfCamera = new Dictionary<Camera, int>();
        private SpeedPoolSettings settings;

        public static void Refresh()
        {
            SpeedPoolSettings s = PoolSettingsLoader.LoadOrDefault<SpeedPoolSettings>("SpeedPoolSettings");
            // Nor in Hide and seek: a mark would show where a hider is.
            bool wanted = PlayerBallGroups.Teams && SumoPoolRuleSet.Current == null && HideAndSeekRuleSet.Current == null && (s.teamChevrons || s.teamRings);
            if (!wanted)
            {
                if (instance != null) Destroy(instance.gameObject);
                instance = null;
                return;
            }
            if (instance == null) instance = new GameObject("Team Marks").AddComponent<PlayerTeamMarks>();
            instance.Apply(s);
        }

        private void Awake()
        {
            // Unlit: a lit sprite took the shade of the felt and the room.
            if (unlit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) unlit = new Material(shader) { name = "Team Marks (unlit)" };
            }
            for (int p = 0; p < players.Length; p++)
                players[p] = new Marked
                {
                    ring = MakeSprite($"Team Ring J{p + 1}", RingSprite(), Quaternion.Euler(90f, 0f, 0f)),
                    chevron = MakeSprite($"Team Chevron J{p + 1}", ChevronSprite(), Quaternion.identity),
                };
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.rotation = rotation;
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            if (unlit != null) r.sharedMaterial = unlit;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }

        // The settings may have changed between matches: colours, and every
        // mark put on again.
        private void Apply(SpeedPoolSettings s)
        {
            settings = s;
            playerOfCamera.Clear();
            for (int p = 0; p < players.Length; p++)
            {
                Marked m = players[p];
                m.body = null;
                Color colour = s.PlayerCueColor(PlayerBallGroups.ColourOf(p));
                m.ring.color = colour;
                m.chevron.color = colour;
            }
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (instance == this) instance = null;
        }

        private void LateUpdate()
        {
            for (int p = 0; p < players.Length; p++)
            {
                Marked m = players[p];
                Transform body = BodyOf(p);
                if (body != m.body)
                {
                    m.body = body;
                    m.controller = body != null ? body.GetComponentInChildren<CharacterController>() : null;
                    Animator animator = body != null ? body.GetComponentInChildren<Animator>() : null;
                    bool human = animator != null && animator.isHuman;
                    m.head = human ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                    m.neck = human ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
                }
                Place(m);
            }
        }

        private void Place(Marked m)
        {
            CharacterController controller = m.controller;
            bool there = m.body != null && controller != null && m.body.gameObject.activeInHierarchy;
            m.ring.enabled = there && settings.teamRings && controller.enabled;
            m.chevron.enabled = there && settings.teamChevrons;
            if (!there) return;
            Transform t = controller.transform;
            float scale = Mathf.Max(t.lossyScale.x, t.lossyScale.z);
            if (m.ring.enabled)
            {
                // At the body's root, on the ground (the capsule's bottom is
                // 11 % of the body above it on New.prefab).
                m.ring.transform.position = t.position + Vector3.up * (0.01f * t.lossyScale.y);
                m.ring.transform.localScale = Vector3.one * (controller.radius * scale * 2f * settings.teamRingSize);
            }
        }

        // The chevron's tip over the top of the head. Placed as each camera
        // draws, not in LateUpdate: the IK (aiming, FinalIK) and PuppetMaster
        // move the head after our LateUpdate, which left the chevron a frame
        // behind and off to the side. The head bone is at the skull's base:
        // the top is further along the neck→head line (the head leans when
        // the player bends over the table), then the gap goes straight up.
        // Its size grows with the distance to this camera, so that it keeps
        // about the same size on screen: small up close (it doesn't hide the
        // player), still readable across the room. Team Chevron Size at Team
        // Chevron Size At body heights away, within the min / max scales.
        private void PlaceChevron(Marked m, Vector3 eye)
        {
            CharacterController controller = m.controller;
            Transform t = controller.transform;
            float height = controller.height * t.lossyScale.y;
            float distance = Vector3.Distance(eye, t.position) / Mathf.Max(height, 0.01f);
            float scale = Mathf.Clamp(distance / Mathf.Max(settings.teamChevronSizeAt, 0.1f), settings.teamChevronMinScale, settings.teamChevronMaxScale);
            float size = height * settings.teamChevronSize * scale;
            Vector3 top;
            if (m.head != null)
            {
                Vector3 along = m.neck != null ? (m.head.position - m.neck.position).normalized : Vector3.up;
                top = m.head.position + along * (height * SkullAboveHeadBone);
            }
            else top = t.TransformPoint(controller.center + Vector3.up * (controller.height * 0.5f));
            m.chevron.transform.position = top + Vector3.up * (height * settings.teamChevronHeight + size * ChevronTipBelowCentre);
            m.chevron.transform.localScale = Vector3.one * size;
        }

        // Each camera sees the chevrons face on, and not its own player's.
        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (settings == null || !settings.teamChevrons) return;
            int viewer = PlayerOf(camera);
            Quaternion facing = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
            for (int p = 0; p < players.Length; p++)
            {
                SpriteRenderer chevron = players[p].chevron;
                bool there = players[p].body != null && players[p].controller != null && players[p].body.gameObject.activeInHierarchy;
                chevron.enabled = there && p != viewer;
                if (!chevron.enabled) continue;
                PlaceChevron(players[p], camera.transform.position);
                chevron.transform.rotation = facing;
            }
        }

        private int PlayerOf(Camera camera)
        {
            if (playerOfCamera.TryGetValue(camera, out int player)) return player;
            player = -1;
            foreach (PlayerInput input in PlayerInput.all)
            {
                Camera own = input.camera != null ? input.camera : input.GetComponentInChildren<Camera>(true);
                if (own == camera) player = input.playerIndex;
            }
            playerOfCamera[camera] = player;
            return player;
        }

        // ---------- Bodies, sprites ----------

        // The shrunk body in the Shooter modes, else the player's own.
        private static Transform BodyOf(int player)
        {
            if (player >= PlayerBallGroups.Players) return null;
            ShooterMiniRuleSet shooter = ShooterMiniRuleSet.Current;
            if (shooter != null)
            {
                Player.LocalPlayerShrink body = shooter.BodyOf(player);
                return body != null ? body.transform : null;
            }
            PlayerInput input = PlayerInput.GetPlayerByIndex(player);
            return input != null ? input.transform : null;
        }

        // A thick ring with a light fill, white (tinted by the renderer).
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            ringSprite = Draw("Team ring", (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                return d > 1f ? 0f : d > 0.94f ? Mathf.InverseLerp(1f, 0.94f, d) : d > 0.66f ? 1f : Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0.6f, 0.66f, d));
            });
            return ringSprite;
        }

        // A point down, white (tinted by the renderer), with a dark edge so
        // that it reads on a light background too.
        private static Sprite ChevronSprite()
        {
            if (chevronSprite != null) return chevronSprite;
            const int Size = 128;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Team chevron", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
                for (int px = 0; px < Size; px++)
                {
                    // -1..1, y up. Triangle pointing down: tip at (0, -0.85), top edge at y = 0.75.
                    float x = (px + 0.5f) / Size * 2f - 1f, y = (py + 0.5f) / Size * 2f - 1f;
                    float inside = Mathf.Min(0.75f - y, (y + 0.85f) * 0.5f - Mathf.Abs(x) * 0.866f);
                    float a = Mathf.Clamp01((inside + 0.02f) / 0.04f);
                    // Dark edge, 0.12 wide, then the white (tinted) inside.
                    float light = Mathf.Clamp01((inside - 0.1f) / 0.04f);
                    byte v = (byte)Mathf.Lerp(30f, 255f, light);
                    pixels[py * Size + px] = new Color32(v, v, v, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            chevronSprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            return chevronSprite;
        }

        private static Sprite Draw(string name, System.Func<float, float, float> alpha)
        {
            const int Size = 128;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size * 2f - 1f, y = (py + 0.5f) / Size * 2f - 1f;
                    pixels[py * Size + px] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
