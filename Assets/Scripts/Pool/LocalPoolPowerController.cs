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
    // A PocketTargetPower (Aimant, Bloquer une poche) first needs a pocket,
    // a TablePointPower (Trou) a point of the table: the press starts a pick
    // instead (LocalPocketPointer: look at the pocket / the felt),
    // Interact confirms, "Next" again cancels (the power stays in hand).
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolPowerController : MonoBehaviour
    {
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string usePowerActionName = "Next";
        [SerializeField] private string interactActionName = "Interact";

        private PlayerInput playerInput;
        private InputAction usePowerAction;
        private InputAction interactAction;
        private LocalPoolAimController aimController;
        private LocalPocketPointer pointer;

        // A PocketTargetPower or a TablePointPower while picking, else null.
        private PoolPower choosingPower;
        // The pick ended (confirmed or cancelled) this frame: Interact must
        // still count as taken for the scripts running after this one
        // (aim entry, cue pickup), or the confirming press would also grab
        // or drop the cue.
        private int choiceEndedFrame = -1;

        // Read by LocalPoolAimController (stays out of the way, keeps
        // Interact for itself) and the HUD (banner).
        public bool IsChoosingPocket => choosingPower != null || choiceEndedFrame == Time.frameCount;
        public PoolPower ChoosingPower => choosingPower;
        public bool IsChoosingTablePoint => choosingPower is TablePointPower;
        public PoolPocket TargetedPocket => choosingPower is PocketTargetPower ? pointer.Pointed : null;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            aimController = GetComponent<LocalPoolAimController>();
            pointer = LocalPocketPointer.For(gameObject);
            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            usePowerAction = map.FindAction(usePowerActionName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
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

            if (power is PocketTargetPower || power is TablePointPower)
            {
                // Ball in hand owns the camera and Interact, the 8-ball call
                // owns the pointer: the pick waits until they're done.
                if (IsPointerTakenByAim()) return;
                choosingPower = power;
                return;
            }

            rules.TryActivatePower(effectivePlayer);
        }

        private bool IsPointerTakenByAim() =>
            aimController != null && (aimController.IsPlacementViewActive || aimController.IsCallPocketActive);

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
            // hot-seat solo, match restarted), or no longer usable now, or
            // ball in hand took over the camera: drop the pick, keep the power.
            PoolPower held = rules.GetHeldPower(effectivePlayer);
            if (held != choosingPower || !CanActivate(rules, held, effectivePlayer)
                || (aimController != null && aimController.IsPlacementViewActive)
                || usePowerAction.WasPressedThisFrame())
            {
                EndChoice();
                return;
            }

            bool confirm = interactAction.WasPressedThisFrame() || interactAction.WasPerformedThisFrame();
            if (choosingPower is TablePointPower tablePower)
            {
                int level = rules.GetHeldPowerLevel(effectivePlayer);
                if (pointer.PointTable(tablePower.Margin(level), tablePower.PreviewRadius(level), out Vector3 point) && confirm)
                {
                    EndChoice();
                    rules.TryActivatePower(effectivePlayer, point);
                }
                return;
            }

            PoolPocket picked = pointer.Point();
            if (picked != null && confirm)
            {
                EndChoice();
                rules.TryActivatePower(effectivePlayer, picked);
            }
        }

        private void EndChoice()
        {
            if (choosingPower == null) return;
            pointer.Release();
            choosingPower = null;
            choiceEndedFrame = Time.frameCount;
        }

        // Hint line when the UI Toolkit HUD isn't there to show its banner
        // (the pointer itself is drawn by LocalPocketPointer).
        private void OnGUI()
        {
            if (choosingPower == null || PoolMatchRules.ExternalHud || pointer.ViewCamera == null) return;
            Rect view = pointer.ViewCamera.pixelRect;
            PoolPocket pocket = pointer.Pointed;
            string hint = IsChoosingTablePoint
                ? $"{choosingPower.PowerName} : vise la table (Interact pour valider, pouvoir pour annuler)"
                : pocket != null
                    ? $"{choosingPower.PowerName} → {pocket.DescribeLocation()} (Interact pour valider, pouvoir pour annuler)"
                    : $"{choosingPower.PowerName} : vise un trou (pouvoir pour annuler)";
            GUI.Box(new Rect(view.x + view.width / 2f - 220f, Screen.height - view.yMax + 20f, 440f, 28f), hint);
        }
    }
}
