using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Owner-only: detects nearby Grabbable objects and picks them up / drops
    // them with Interact. Defers Interact to PoolAimController when it wants
    // to handle it instead (entering/exiting aim mode with the cue in hand).
    [RequireComponent(typeof(FpsPlayerController))]
    public class PlayerHandController : NetworkBehaviour
    {
        [SerializeField] private float pickupRange = 2f;

        [Header("Input (whole asset — actions looked up by name)")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string interactActionName = "Interact";

        private PoolAimController poolAimController;
        private InputAction interactAction;
        private Grabbable heldObject;

        public Grabbable HeldObject => heldObject;

        private void Awake()
        {
            poolAimController = GetComponent<PoolAimController>();

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
        }

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
            if (!IsOwner) return;

            interactAction.Enable();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            interactAction.Disable();
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
                if (hit.TryGetComponent(out Grabbable grabbable) && !grabbable.IsHeld)
                {
                    heldObject = grabbable;
                    grabbable.RequestPickUpServerRpc(NetworkObject);
                    return;
                }
            }
        }

        private void Drop()
        {
            heldObject.RequestDropServerRpc();
            heldObject = null;
        }
    }
}
