using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Lets a player activate their stored power (see PoolMatchRules.
    // GrantPower/TryActivatePower) with a button press, gated to their own
    // turn (same PlayerInput.playerIndex check used elsewhere). Reuses the
    // existing "Next" action — bound to Keyboard 2 / Gamepad D-pad right in
    // the default asset, and not used by anything else in this project —
    // instead of adding a brand new input binding; rename it to something
    // clearer in the input asset later if desired.
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolPowerController : MonoBehaviour
    {
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string usePowerActionName = "Next";

        private PlayerInput playerInput;
        private InputAction usePowerAction;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            usePowerAction = map.FindAction(usePowerActionName, throwIfNotFound: true);
        }

        private void Update()
        {
            if (!usePowerAction.WasPressedThisFrame()) return;

            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.MatchStarted || rules.GameOver) return;

            // GetEffectivePlayerIndex, NOT the raw PlayerInput.playerIndex:
            // in real split-screen it resolves to the same physical player
            // every time, so this strict check still only lets a held power
            // be triggered by whoever it actually belongs to (a plain
            // CanPlayerShoot() check would let a press meant for one player's
            // index activate/consume the OTHER player's power via its
            // solo-testing fallback). In hot-seat solo (one PlayerInput
            // playing both sides), it resolves to whichever side is
            // currently up instead, so activation isn't permanently stuck to
            // slot 0.
            int effectivePlayer = rules.GetEffectivePlayerIndex(playerInput.playerIndex);
            if (rules.CurrentPlayer != effectivePlayer) return;

            rules.TryActivatePower(effectivePlayer);
        }
    }
}
