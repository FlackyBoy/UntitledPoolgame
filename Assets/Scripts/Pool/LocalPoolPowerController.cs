using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Lets a player activate their stored power (see PoolMatchRules.
    // GrantPower/TryActivatePower) with a button press. Only Effect powers
    // are actually gated to the holder's own turn — see PoolPower.
    // RequiresOwnTurn — Attack/Defense can be triggered whenever, since
    // they target/react to the opponent instead of the holder's own next
    // shot. Reads the "Next" action (F / gamepad B-Circle in the local
    // input asset; it used to share X-Square with Attack). Added to the
    // player automatically by LocalPlayerHandController when missing.
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
            // every time, so a held power can only ever be triggered by
            // whoever it actually belongs to (a plain CanPlayerShoot() check
            // would let a press meant for one player's index activate/
            // consume the OTHER player's power via its solo-testing
            // fallback). In hot-seat solo (one PlayerInput playing both
            // sides), it resolves to whichever side is currently up instead.
            int effectivePlayer = rules.GetEffectivePlayerIndex(playerInput.playerIndex);

            PoolPower power = rules.GetHeldPower(effectivePlayer);
            if (power == null) return;

            // Only Effect powers (change the holder's own next shot) are
            // actually restricted to their own turn — see PoolPower.
            // RequiresOwnTurn. Attack/Defense stay activatable regardless of
            // whose turn it is. In hot-seat solo this check can never
            // trigger either way: effectivePlayer already collapses to
            // CurrentPlayer above, so there's nothing meaningfully
            // "off-turn" to block with a single shared device.
            if (power.RequiresOwnTurn && rules.CurrentPlayer != effectivePlayer) return;

            rules.TryActivatePower(effectivePlayer);
        }
    }
}
