using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Picking a pocket by looking at it, shared by everything that asks the
    // player for one: a PocketTargetPower (LocalPoolPowerController) and the
    // 8-ball call (LocalPoolAimController). Also picks a point of the felt
    // for a TablePointPower (PointTable). While a pick is on, a white
    // pointer sits at the centre of this player's view and the open pocket
    // closest to it gets PoolPocket's ring and halo. Only the looking part
    // lives here; each user reads its own confirm/cancel input. (Later the
    // pick is meant to be a pointing finger + thumbs-up gesture: only this
    // class and the confirm input change.)
    //
    // Added on demand by its users (For), one per player.
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPocketPointer : MonoBehaviour
    {
        [Tooltip("Choix du trou : angle maximal (degrés) entre le centre de la vue et un trou pour qu'il soit visé. Plus grand = plus tolérant, mais deux trous proches deviennent durs à départager de loin.")]
        [SerializeField] private float maxPointAngle = 8f;
        [Tooltip("Taille (pixels) du pointeur blanc affiché pendant le choix du trou.")]
        [SerializeField] private float pointerSize = 8f;

        private PlayerInput playerInput;
        private Camera viewCamera;
        private bool showing;
        private PoolPocket pointed;

        public PoolPocket Pointed => pointed;
        // The viewport the pointer is drawn in (hint lines of the users).
        public Camera ViewCamera => viewCamera;

        public static LocalPocketPointer For(GameObject player)
        {
            LocalPocketPointer pointer = player.GetComponent<LocalPocketPointer>();
            return pointer != null ? pointer : player.AddComponent<LocalPocketPointer>();
        }

        private void Awake() => playerInput = GetComponent<PlayerInput>();

        private void Start()
        {
            // Start, not Awake: PlayerInputManager assigns the split-screen
            // camera after the player's Awake.
            viewCamera = playerInput.camera != null ? playerInput.camera : GetComponentInChildren<Camera>(true);
        }

        private void OnDisable() => Release();

        // Called every frame of a pick: shows the pointer, moves the ring to
        // the pocket looked at, and returns it (null if none is close enough).
        public PoolPocket Point()
        {
            showing = true;
            ShowTableRing(false);
            SetPointed(FindPointedPocket());
            return pointed;
        }

        // Same, for a point of the table (TablePointPower): where the view
        // meets the felt, kept margin from the cushions and pockets, with a
        // ring of previewRadius around it. False when not looking at the felt.
        public bool PointTable(float margin, float previewRadius, out Vector3 point)
        {
            showing = true;
            SetPointed(null);
            point = Vector3.zero;
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null || surface.Felt == null || viewCamera == null
                || !surface.Felt.Raycast(new Ray(viewCamera.transform.position, viewCamera.transform.forward), out RaycastHit hit, MaxTableDistance))
            {
                ShowTableRing(false);
                return false;
            }

            point = TablePointPower.ClampToTable(hit.point, margin);
            ShowTableRing(true);
            RingLine.Set(tableRing, point + Vector3.up * RingSettings.targetRingHeight, previewRadius * PoolPocket.RingPulse());
            return true;
        }

        // End of a pick (confirmed, cancelled or abandoned).
        public void Release()
        {
            showing = false;
            SetPointed(null);
            ShowTableRing(false);
        }

        private const float MaxTableDistance = 20f;
        private LineRenderer tableRing;
        private static PoolPotEffectSettings ringSettings;

        // The pocket ring's look (PoolPotEffectSettings), for the table ring too.
        private static PoolPotEffectSettings RingSettings =>
            ringSettings != null ? ringSettings : ringSettings = PoolSettingsLoader.LoadOrDefault<PoolPotEffectSettings>("PoolPotEffectSettings");

        private void ShowTableRing(bool show)
        {
            if (show && tableRing == null)
                tableRing = RingLine.Create(transform, "TablePointRing", RingSettings.targetRingWidth, RingSettings.targetRingColor);
            if (tableRing != null) tableRing.enabled = show;
        }

        // By angle rather than a raycast: a pocket is a small hole, hard to
        // hit exactly from across the table. A closed pocket can't be picked.
        private PoolPocket FindPointedPocket()
        {
            if (viewCamera == null) return null;
            Transform view = viewCamera.transform;
            Vector3 origin = view.position, forward = view.forward;

            PoolPocket best = null;
            float bestDot = Mathf.Cos(maxPointAngle * Mathf.Deg2Rad);
            foreach (PoolPocket pocket in PoolPocket.Active)
            {
                if (pocket.IsClosed) continue;
                Vector3 toPocket = pocket.transform.position - origin;
                float distance = toPocket.magnitude;
                if (distance < 1e-4f) continue;
                float dot = Vector3.Dot(toPocket / distance, forward);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = pocket;
                }
            }
            return best;
        }

        private void SetPointed(PoolPocket pocket)
        {
            if (pocket == pointed) return;
            if (pointed != null) pointed.SetTargeted(false);
            pointed = pocket;
            if (pointed != null) pointed.SetTargeted(true);
        }

        private static readonly Color PointerOutline = new Color(0f, 0f, 0f, 0.6f);

        // Provisional (OnGUI, like the combat bars).
        private void OnGUI()
        {
            if (!showing || viewCamera == null) return;

            Rect view = viewCamera.pixelRect;
            float x = view.x + view.width / 2f, y = Screen.height - view.y - view.height / 2f;
            float outline = pointerSize + 4f;
            GUI.color = PointerOutline;
            GUI.DrawTexture(new Rect(x - outline / 2f, y - outline / 2f, outline, outline), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - pointerSize / 2f, y - pointerSize / 2f, pointerSize, pointerSize), Texture2D.whiteTexture);
        }
    }
}
