using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Offline counterpart to PlayerHandController — detects nearby
    // LocalGrabbable objects and picks them up / drops them with Interact,
    // deferring to LocalPoolAimController when it wants Interact instead
    // (entering/exiting aim mode with the cue in hand). No networking.
    [RequireComponent(typeof(LocalFpsPlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPlayerHandController : MonoBehaviour
    {
        [SerializeField] private float pickupRange = 2f;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string interactActionName = "Interact";

        private LocalPoolAimController poolAimController;
        private PlayerInput playerInput;
        private InputAction interactAction;
        private LocalGrabbable heldObject;

        public LocalGrabbable HeldObject => heldObject;

        private void Awake()
        {
            poolAimController = GetComponent<LocalPoolAimController>();
            playerInput = GetComponent<PlayerInput>();

            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
        }

        private void Update()
        {
            if (poolAimController != null && poolAimController.WantsInteractThisFrame(heldObject))
                return;

            if (!(interactAction.WasPressedThisFrame() || interactAction.WasPerformedThisFrame()))
                return;

            if (heldObject != null)
                Drop();
            else
                TryPickUp();
        }

        private void TryPickUp()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent(out LocalGrabbable grabbable) || grabbable.IsHeld)
                    continue;

                // The cue specifically is turn-gated — otherwise the player who
                // isn't up could grab it and hold the other one's turn hostage.
                // Everything else (non-cue Grabbables) is free to pick up anytime.
                if (grabbable.TryGetComponent(out Cue _) && !CanUseCueNow())
                    continue;

                heldObject = grabbable;
                grabbable.PickUp(transform);
                return;
            }
        }

        private bool CanUseCueNow()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules == null || rules.CanPlayerShoot(playerInput.playerIndex);
        }

        private void Drop()
        {
            heldObject.Drop();
            heldObject = null;
        }
    }
}
