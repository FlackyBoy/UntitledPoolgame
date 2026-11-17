using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace UntitledPoolGame.Pool
{
    // 8-ball in Speed Pool: a ring of the player's colour on the felt under
    // each of their balls, seen only in their own view (and their teammate's,
    // in their colour). The screen is split, so each halo is switched on or
    // off for each camera just before it draws (beginCameraRendering).
    // Not parented to the ball (it rolls): follows it in LateUpdate.
    public class OwnBallHalo : MonoBehaviour
    {
        private static readonly List<OwnBallHalo> halos = new List<OwnBallHalo>();
        // Each camera's player (-1: not a player's view), looked up once per match.
        private static readonly Dictionary<Camera, int> playerOfCamera = new Dictionary<Camera, int>();
        private static Color[] playerColours = new Color[0];
        private static Sprite ring;

        private PoolBall ball;
        private int side;
        private SpriteRenderer sprite;
        private bool live;

        // A halo under the ball for its owner's side. colours: each player's
        // colour (the halo takes the viewer's).
        public static void Add(PoolBall ball, int side, float sizeInDiameters, Color[] colours)
        {
            if (halos.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            }
            playerColours = colours;
            var go = new GameObject($"Halo {ball.name}");
            var halo = go.AddComponent<OwnBallHalo>();
            halo.ball = ball;
            halo.side = side;
            halo.sprite = go.AddComponent<SpriteRenderer>();
            halo.sprite.sprite = Ring();
            halo.sprite.shadowCastingMode = ShadowCastingMode.Off;
            halo.sprite.receiveShadows = false;
            halo.sprite.enabled = false;
            // The sprite is one unit wide.
            go.transform.localScale = Vector3.one * (ball.Radius * 2f * sizeInDiameters);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            halos.Add(halo);
        }

        // Before every match (SpeedPoolRuleSet.FitToPlayers): the players and
        // their cameras may have changed too.
        public static void RemoveAll()
        {
            foreach (OwnBallHalo halo in halos)
                if (halo != null) Destroy(halo.gameObject);
            halos.Clear();
            playerOfCamera.Clear();
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        private void LateUpdate()
        {
            live = ball != null && ball.isActiveAndEnabled;
            if (!live) return;
            // Just above the felt, under the ball.
            transform.position = ball.transform.position + Vector3.down * (ball.Radius * 0.98f);
        }

        private void OnDestroy() => halos.Remove(this);

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            int player = PlayerOf(camera);
            int viewerSide = PlayerBallGroups.SideOf(player);
            Color colour = player >= 0 && player < playerColours.Length ? playerColours[player] : Color.white;
            foreach (OwnBallHalo halo in halos)
            {
                bool show = halo.live && viewerSide >= 0 && halo.side == viewerSide;
                halo.sprite.enabled = show;
                if (show) halo.sprite.color = colour;
            }
        }

        private static int PlayerOf(Camera camera)
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

        // A soft-edged white ring, tinted by the SpriteRenderer; made once.
        private static Sprite Ring()
        {
            if (ring != null) return ring;
            const int Size = 128;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Own ball halo", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            float half = Size * 0.5f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                    // Solid ring from 0.62 to 0.92, a faint fill inside.
                    float a = d > 1f ? 0f
                        : d > 0.92f ? Mathf.InverseLerp(1f, 0.92f, d)
                        : d > 0.62f ? 1f
                        : Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(0.56f, 0.62f, d));
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            ring = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            return ring;
        }
    }
}
