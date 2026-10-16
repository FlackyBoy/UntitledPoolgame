using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Lets a player activate their stored power (see PoolMatchRules.
    // GrantPower/TryActivatePower) with a button press. When it may be
    // launched (own turn only, opponent's turn only, while the balls roll)
    // is set per power — see PoolPower and PoolMatchRules.CheckPowerUse.
    // Reads the "Next" action (Q / gamepad B-Circle in the local
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

        // What the pick is on right now — the point of the felt, or the
        // pocket — for the pointing gesture (LocalPowerGesture). False while
        // nothing valid is under the pointer.
        public bool TryGetPickTarget(out Vector3 point)
        {
            point = tablePoint;
            // On the playing surface itself: the felt's collider runs under
            // the cushions, where the arm kept pointing at the clamped point.
            if (choosingPower is TablePointPower) return hasTablePoint && pointer.TableHitInPlayArea;
            PoolPocket pocket = TargetedPocket;
            if (pocket != null) point = pocket.transform.position;
            return pocket != null;
        }

        private Vector3 tablePoint;
        private bool hasTablePoint;

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

            // When to launch is decided per power (PoolPower) and checked in
            // one place, PoolMatchRules.CheckPowerUse; a refused press is
            // reported so the HUD can say why instead of ignoring it.
            PowerRefusal refusal = rules.CheckPowerUse(effectivePlayer);
            PoolPower power = rules.GetHeldPower(effectivePlayer);
            // Ball in hand owns the camera and Interact, the 8-ball call
            // owns the pointer: a pick waits until they're done.
            if (refusal == PowerRefusal.None && IsPicker(power) && IsPointerTakenByAim())
                refusal = PowerRefusal.PointerBusy;
            if (refusal != PowerRefusal.None)
            {
                PoolMatchRules.NotifyPowerRefused(effectivePlayer, refusal);
                return;
            }

            if (IsPicker(power))
            {
                choosingPower = power;
                return;
            }

            rules.TryActivatePower(effectivePlayer);
        }

        private static bool IsPicker(PoolPower power) => power is PocketTargetPower || power is TablePointPower;

        private bool IsPointerTakenByAim() =>
            aimController != null && (aimController.IsPlacementViewActive || aimController.IsCallPocketActive);

        private void UpdateChoice(PoolMatchRules rules, int effectivePlayer)
        {
            // The power is no longer the one in hand (turn passed in
            // hot-seat solo, match restarted), or no longer usable now (turn
            // changed, balls started rolling), or ball in hand took over the
            // camera: drop the pick, keep the power.
            PoolPower held = rules.GetHeldPower(effectivePlayer);
            if (held != choosingPower || rules.CheckPowerUse(effectivePlayer) != PowerRefusal.None
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
                hasTablePoint = pointer.PointTable(tablePower.Margin(level), tablePower.PreviewRadius(level), out Vector3 point);
                if (hasTablePoint) tablePoint = point;
                if (hasTablePoint && confirm)
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
            hasTablePoint = false;
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
