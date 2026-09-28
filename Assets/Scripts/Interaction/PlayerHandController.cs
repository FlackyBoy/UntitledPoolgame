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
        [SerializeField] private string attackActionName = "Attack";

        [Header("Throw (any held object except the cue)")]
        // See LocalPlayerHandController for why Attack is safe to reuse
        // here (never overlaps with PoolAimController's own use of it,
        // since that only ever happens while holding the cue specifically).
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float minThrowPower = 3f;
        [SerializeField] private float maxThrowPower = 12f;
        [SerializeField] private float throwChargeSpeed = 8f;

        private PoolAimController poolAimController;
        private InputAction interactAction;
        private InputAction attackAction;
        private Grabbable heldObject;
        private float chargedThrowPower;

        public Grabbable HeldObject => heldObject;

        private void Awake()
        {
            poolAimController = GetComponent<PoolAimController>();

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
            attackAction = map.FindAction(attackActionName, throwIfNotFound: true);
        }

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
            if (!IsOwner) return;

            interactAction.Enable();
            attackAction.Enable();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            interactAction.Disable();
            attackAction.Disable();
        }

        private void Update()
        {
            if (poolAimController != null && poolAimController.WantsInteractThisFrame(heldObject))
                return;

            HandleThrow();

            if (!(interactAction.WasPressedThisFrame() || interactAction.WasPerformedThisFrame()))
                return;

            if (heldObject != null)
                Drop();
            else
                TryPickUp();
        }

        // Holding Attack charges throw power, releasing throws — same
        // charge/release gesture as PoolAimController's cue shot, applied to
        // whatever's currently in hand instead. Excludes the cue itself.
        private void HandleThrow()
        {
            if (heldObject == null || IsCue(heldObject))
            {
                chargedThrowPower = 0f;
                return;
            }

            if (attackAction.IsPressed())
            {
                chargedThrowPower = Mathf.Min(chargedThrowPower + throwChargeSpeed * Time.deltaTime, maxThrowPower);
            }
            else if (chargedThrowPower > 0f)
            {
                Throw();
            }
        }

        private void Throw()
        {
            if (cameraTransform == null)
            {
                Debug.LogWarning("[PlayerHandController] Camera Transform is not assigned — can't compute a throw direction. Assign it in the Inspector (same camera object as Pool Aim Controller's own Camera Transform).");
                chargedThrowPower = 0f;
                return;
            }

            float power = Mathf.Max(chargedThrowPower, minThrowPower);
            Vector3 impulse = cameraTransform.forward * power;
            chargedThrowPower = 0f;

            Grabbable thrown = heldObject;
            heldObject = null;
            thrown.RequestThrowServerRpc(impulse);
        }

        private static bool IsCue(Grabbable heldObject) =>
            heldObject != null && heldObject.TryGetComponent(out Cue _);

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
