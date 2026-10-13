using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Lets a player activate their stored power (see PoolMatchRules.
    // GrantPower/TryActivatePower) with a button press. Only Effect powers
    // are actually gated to the holder's own turn — see PoolPower.
    // RequiresOwnTurn — Attack/Defense can be triggered whenever, since
    // they target/react to the opponent instead of the holder's own next
    // shot. Reads the "Next" action (Q / gamepad B-Circle in the local
    // input asset; it used to share X-Square with Attack). Added to the
    // player automatically by LocalPlayerHandController when missing.
    //
    // A PocketTargetPower (Aimant, Bloquer une poche) first needs a pocket:
    // the press starts a pick instead — a white pointer at the centre of
    // this player's view, the pocket closest to it gets a ring, Interact
    // confirms, "Next" again cancels (the power stays in hand).
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolPowerController : MonoBehaviour
    {
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string usePowerActionName = "Next";
        [SerializeField] private string interactActionName = "Interact";
        [Tooltip("Choix du trou : angle maximal (degrés) entre le centre de la vue et un trou pour qu'il soit visé. Plus grand = plus tolérant, mais deux trous proches deviennent durs à départager de loin.")]
        [SerializeField] private float maxPointAngle = 8f;
        [Tooltip("Taille (pixels) du pointeur blanc affiché pendant le choix du trou.")]
        [SerializeField] private float pointerSize = 8f;

        private PlayerInput playerInput;
        private InputAction usePowerAction;
        private InputAction interactAction;
        private LocalPoolAimController aimController;
        private Camera viewCamera;
        private float minPointDot;

        private PocketTargetPower choosingPower;
        private PoolPocket targetedPocket;
        // The pick ended (confirmed or cancelled) this frame: Interact must
        // still count as taken for the scripts running after this one
        // (aim entry, cue pickup), or the confirming press would also grab
        // or drop the cue.
        private int choiceEndedFrame = -1;

        // Read by LocalPoolAimController (stays out of the way, keeps
        // Interact for itself) and the HUD (banner).
        public bool IsChoosingPocket => choosingPower != null || choiceEndedFrame == Time.frameCount;
        public PocketTargetPower ChoosingPower => choosingPower;
        public PoolPocket TargetedPocket => targetedPocket;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            aimController = GetComponent<LocalPoolAimController>();
            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            usePowerAction = map.FindAction(usePowerActionName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
            minPointDot = Mathf.Cos(maxPointAngle * Mathf.Deg2Rad);
        }

        private void Start()
        {
            // Start, not Awake: PlayerInputManager assigns the split-screen
            // camera after the player's Awake.
            viewCamera = playerInput.camera != null ? playerInput.camera : GetComponentInChildren<Camera>(true);
        }

        private void OnDisable() => EndChoice();

        private void Update()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.MatchStarted || rules.GameOver)
            {
                EndChoice();
                return;
            }

            // GetEffectivePlayerIndex, NOT the raw PlayerInput.playerIndex:
            // in real split-screen it resolves to the same physical player
            // every time, so a held power can only ever be triggered by
            // whoever it actually belongs to (a plain CanPlayerShoot() check
            // would let a press meant for one player's index activate/
            // consume the OTHER player's power via its solo-testing
            // fallback). In hot-seat solo (one PlayerInput playing both
            // sides), it resolves to whichever side is currently up instead.
            int effectivePlayer = rules.GetEffectivePlayerIndex(playerInput.playerIndex);

            if (choosingPower != null)
            {
                UpdateChoice(rules, effectivePlayer);
                return;
            }

            if (!usePowerAction.WasPressedThisFrame()) return;

            PoolPower power = rules.GetHeldPower(effectivePlayer);
            if (!CanActivate(rules, power, effectivePlayer)) return;

            if (power is PocketTargetPower targeted)
            {
                // The top-down views (ball in hand, 8-ball call) own the
                // camera and Interact; the pick waits until they're done.
                if (aimController != null && aimController.IsPlacementViewActive) return;
                choosingPower = targeted;
                return;
            }

            rules.TryActivatePower(effectivePlayer);
        }

        // Only Effect powers (change the holder's own next shot) are
        // actually restricted to their own turn — see PoolPower.
        // RequiresOwnTurn. Attack/Defense stay activatable regardless of
        // whose turn it is. In hot-seat solo this check can never trigger
        // either way: effectivePlayer already collapses to CurrentPlayer,
        // so there's nothing meaningfully "off-turn" to block with a single
        // shared device.
        private static bool CanActivate(PoolMatchRules rules, PoolPower power, int player) =>
            power != null && !(power.RequiresOwnTurn && rules.CurrentPlayer != player);

        private void UpdateChoice(PoolMatchRules rules, int effectivePlayer)
        {
            // The power is no longer the one in hand (turn passed in
            // hot-seat solo, match restarted), or no longer usable now, or a
            // top-down view took over the camera: drop the pick, keep the power.
            PoolPower held = rules.GetHeldPower(effectivePlayer);
            if (held != choosingPower || !CanActivate(rules, held, effectivePlayer)
                || (aimController != null && aimController.IsPlacementViewActive)
                || usePowerAction.WasPressedThisFrame())
            {
                EndChoice();
                return;
            }

            SetTargetedPocket(FindPointedPocket());

            if (targetedPocket != null && (interactAction.WasPressedThisFrame() || interactAction.WasPerformedThisFrame()))
            {
                PoolPocket picked = targetedPocket;
                EndChoice();
                rules.TryActivatePower(effectivePlayer, picked);
            }
        }

        // The open pocket closest to the centre of this player's view, within
        // maxPointAngle. By angle rather than a raycast: a pocket is a small
        // hole, hard to hit exactly from across the table.
        private PoolPocket FindPointedPocket()
        {
            if (viewCamera == null) return null;
            Transform view = viewCamera.transform;
            Vector3 origin = view.position, forward = view.forward;

            PoolPocket best = null;
            float bestDot = minPointDot;
            foreach (PoolPocket pocket in PoolPocket.Active)
            {
                if (!PocketTargetPower.CanTarget(pocket)) continue;
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

        private void SetTargetedPocket(PoolPocket pocket)
        {
            if (pocket == targetedPocket) return;
            if (targetedPocket != null) targetedPocket.SetTargeted(false);
            targetedPocket = pocket;
            if (targetedPocket != null) targetedPocket.SetTargeted(true);
        }

        private void EndChoice()
        {
            if (choosingPower == null) return;
            SetTargetedPocket(null);
            choosingPower = null;
            choiceEndedFrame = Time.frameCount;
        }

        private static readonly Color PointerOutline = new Color(0f, 0f, 0f, 0.6f);

        // Provisional (OnGUI, like the combat bars): the white pointer at the
        // centre of this player's viewport while picking a pocket, plus a
        // hint line when the UI Toolkit HUD isn't there to show its banner.
        private void OnGUI()
        {
            if (choosingPower == null || viewCamera == null) return;

            Rect view = viewCamera.pixelRect;
            float x = view.x + view.width / 2f, y = Screen.height - view.y - view.height / 2f;
            float outline = pointerSize + 4f;
            GUI.color = PointerOutline;
            GUI.DrawTexture(new Rect(x - outline / 2f, y - outline / 2f, outline, outline), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - pointerSize / 2f, y - pointerSize / 2f, pointerSize, pointerSize), Texture2D.whiteTexture);

            if (PoolMatchRules.ExternalHud) return;
            string hint = targetedPocket != null
                ? $"{choosingPower.PowerName} → {targetedPocket.DescribeLocation()} (Interact pour valider, pouvoir pour annuler)"
                : $"{choosingPower.PowerName} : vise un trou (pouvoir pour annuler)";
            GUI.Box(new Rect(x - 220f, Screen.height - view.yMax + 20f, 440f, 28f), hint);
        }
    }
}
