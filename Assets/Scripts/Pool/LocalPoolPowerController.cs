using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Launches the power in stock with Next (Q / B) and switches between the
    // two with SwitchPower (wheel, D-pad). When it may be launched is set per
    // power (PoolMatchRules.CheckPowerUse). Added by LocalPlayerHandController.
    // A pocket power (Aimant, Bloquer une poche) or a table power (Trou)
    // starts a pick instead (LocalPocketPointer): Interact confirms, Next
    // cancels and keeps the power.
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolPowerController : MonoBehaviour
    {
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string usePowerActionName = "Next";
        [SerializeField] private string interactActionName = "Interact";
        [Tooltip("Action qui change de pouvoir sélectionné (molette, D-pad gauche / droite). Absente de l'asset = pas de changement.")]
        [SerializeField] private string switchPowerActionName = "SwitchPower";
        [Tooltip("Délai minimal (secondes) entre deux changements : une molette ou un pavé tactile envoie plusieurs crans d'un coup.")]
        [SerializeField] private float switchCooldown = 0.18f;

        private PlayerInput playerInput;
        private InputAction usePowerAction;
        private InputAction interactAction;
        private InputAction switchPowerAction;
        private float nextSwitchTime;
        private LocalPoolAimController aimController;
        private LocalPocketPointer pointer;

        // A PocketTargetPower or a TablePointPower while picking, else null.
        private PoolPower choosingPower;
        // The pick ended this frame: Interact still counts as taken for the
        // scripts after this one, or the same press would grab or drop the cue.
        private int choiceEndedFrame = -1;

        // Read by LocalPoolAimController (stays out of the way, keeps
        // Interact for itself) and the HUD (banner).
        public bool IsChoosingPocket => choosingPower != null || choiceEndedFrame == Time.frameCount;
        public PoolPower ChoosingPower => choosingPower;
        public bool IsChoosingTablePoint => choosingPower is TablePointPower;
        public PoolPocket TargetedPocket => choosingPower is PocketTargetPower ? pointer.Pointed : null;

        // What the pick points at (felt point or pocket), for the gesture.
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
            switchPowerAction = map.FindAction(switchPowerActionName, throwIfNotFound: false);
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

            // Effective index: in split screen a player only ever triggers
            // their own power; in hot-seat solo it's the side being played.
            int effectivePlayer = rules.GetEffectivePlayerIndex(playerInput.playerIndex);

            // A pick under way ends by itself (UpdateChoice).
            if (switchPowerAction != null && switchPowerAction.WasPerformedThisFrame() && Time.unscaledTime >= nextSwitchTime)
            {
                float value = switchPowerAction.ReadValue<float>();
                if (Mathf.Abs(value) > 0.01f && rules.CycleSelectedPower(effectivePlayer, value > 0f ? 1 : -1))
                    nextSwitchTime = Time.unscaledTime + switchCooldown;
            }

            if (choosingPower != null)
            {
                UpdateChoice(rules, effectivePlayer);
                return;
            }

            if (!usePowerAction.WasPressedThisFrame()) return;

            // A refused press is reported so the HUD can say why.
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
            // Not the power in hand any more, not usable now, or ball in hand
            // took the camera: drop the pick, keep the power.
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
