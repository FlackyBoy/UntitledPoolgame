using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Online counterpart to LocalPoolPowerController — see that file for why
    // "Next" is repurposed as the "use power" action. No stable per-client
    // player index yet online (see TODO.md, same limitation as ball-in-hand
    // placement), so this can't gate on whose turn it actually is the way the
    // offline version does — it just activates whatever PoolMatchRules.
    // CurrentPlayer currently holds, same as everyone else running their own
    // local copy of the match.
    public class PoolPowerController : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string usePowerActionName = "Next";

        private InputAction usePowerAction;

        private void Awake()
        {
            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            usePowerAction = map.FindAction(usePowerActionName, throwIfNotFound: true);
        }

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
            if (!IsOwner) return;

            usePowerAction.Enable();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            usePowerAction.Disable();
        }

        private void Update()
        {
            if (!usePowerAction.WasPressedThisFrame()) return;

            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.MatchStarted || rules.GameOver) return;

            rules.TryActivatePower(rules.CurrentPlayer);
        }
    }
}
