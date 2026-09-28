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
        [SerializeField] private string attackActionName = "Attack";

        // The cue specifically attaches to this hand bone instead of the
        // root (see GetHolderFor) — its Aim IK/Limb IK targets are fixed
        // sockets parented under this same bone in the prefab, so whichever
        // physical cue instance is currently attached here lines up with
        // them without any extra runtime rewiring. Every other Grabbable
        // still attaches to the root (see the field below), unaffected.
        [SerializeField] private Animator animator;

        // Optional — this player's own cue, already sitting parented under
        // the hand bone in the prefab (rest pose set by hand to match). Runs
        // through the exact same PickUp() as a normal pickup (see Awake)
        // rather than a separate "already held" code path, so it ends up in
        // an identical state (kinematic, collider off, IsHeld true) to one
        // picked up mid-game — including being droppable/re-pickable later.
        [SerializeField] private LocalGrabbable startingHeldObject;

        [Header("Throw (any held object except the cue)")]
        // Reuses Attack rather than a dedicated action — while holding a
        // non-cue object, Attack is otherwise completely unused (it only
        // ever gets read by LocalPoolAimController.UpdateAim(), which is
        // only reachable while holding the cue and aiming, so the two never
        // overlap on the same held object).
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float minThrowPower = 3f;
        [SerializeField] private float maxThrowPower = 12f;
        [SerializeField] private float throwChargeSpeed = 8f;

        private LocalPoolAimController poolAimController;
        private LocalCuePickupTrigger cuePickupTrigger;
        private PlayerInput playerInput;
        private InputAction interactAction;
        private InputAction attackAction;
        private LocalGrabbable heldObject;
        private float chargedThrowPower;

        public LocalGrabbable HeldObject => heldObject;

        private void Awake()
        {
            poolAimController = GetComponent<LocalPoolAimController>();
            cuePickupTrigger = GetComponent<LocalCuePickupTrigger>();
            playerInput = GetComponent<PlayerInput>();

            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
            attackAction = map.FindAction(attackActionName, throwIfNotFound: true);
        }

        // Not Awake — startingHeldObject's own LocalGrabbable.Awake() (which
        // sets up its rb/col) isn't guaranteed to have run yet at this
        // point, since it's a different GameObject and Unity doesn't order
        // Awake() across objects. Start() is: Unity guarantees every
        // Awake() in the scene finishes before any Start() runs.
        private void Start()
        {
            if (startingHeldObject != null && !startingHeldObject.IsHeld)
            {
                heldObject = startingHeldObject;
                heldObject.PickUp(GetHolderFor(heldObject));
            }
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
        // charge/release gesture as PoolAimController's cue shot, just
        // applied to whatever's currently in hand instead of the cue ball.
        // Excludes the cue itself: it's not meant to be thrown away, and
        // this frees Attack up for LocalPoolAimController.UpdateAim() to
        // read on its own once the player actually enters aim mode with it.
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
                Debug.LogWarning("[LocalPlayerHandController] Camera Transform is not assigned — can't compute a throw direction. Assign it in the Inspector (same camera object as Local Pool Aim Controller's own Camera Transform).");
                chargedThrowPower = 0f;
                return;
            }

            float power = Mathf.Max(chargedThrowPower, minThrowPower);
            Vector3 impulse = cameraTransform.forward * power;
            chargedThrowPower = 0f;

            LocalGrabbable thrown = heldObject;
            heldObject = null;
            thrown.Throw(impulse);
        }

        private static bool IsCue(LocalGrabbable heldObject) =>
            heldObject != null && heldObject.TryGetComponent(out Cue _);

        private void TryPickUp()
        {
            // The cue's own pickup (FBBIK reach-and-grab, gated on distance
            // AND facing) is tried first, through this same call chain —
            // see LocalCuePickupTrigger's class comment for why it isn't a
            // separate Update() reading Interact independently (that raced
            // against Drop() on the same button press).
            if (cuePickupTrigger != null && cuePickupTrigger.TryStartPickup())
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent(out LocalGrabbable grabbable) || grabbable.IsHeld)
                    continue;

                // The cue is handled exclusively by LocalCuePickupTrigger
                // (FBBIK reach-and-grab, gated on distance AND facing, not
                // just plain proximity like every other Grabbable here).
                if (grabbable.TryGetComponent(out Cue _))
                    continue;

                heldObject = grabbable;
                grabbable.PickUp(GetHolderFor(grabbable));
                return;
            }
        }

        public Transform GetHolderFor(LocalGrabbable grabbable)
        {
            if (animator != null && grabbable.TryGetComponent(out Cue _))
            {
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand != null) return rightHand;
            }
            return transform;
        }

        // Was private — still used internally (see below), but
        // LocalCuePickupTrigger needs the same turn-gating check now that
        // it owns cue pickup exclusively (see TryPickUp's Cue skip above).
        public bool CanUseCueNow()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules == null || rules.CanPlayerShoot(playerInput.playerIndex);
        }

        private void Drop()
        {
            // The cue releases through PickUp2Handed's own interaction
            // (Resume) instead of a plain LocalGrabbable.Drop() — see
            // LocalCuePickupTrigger's class comment.
            if (cuePickupTrigger == null || !cuePickupTrigger.TryReleaseIfCue(heldObject))
                heldObject.Drop();

            heldObject = null;
        }

        // Same as Drop() above, but callable from outside — used by
        // LocalPlayerRagdollController to force-release whatever's in hand
        // right before ragdolling (can't stay gripping something while
        // physics takes over the whole body).
        public void ForceDrop()
        {
            if (heldObject != null) Drop();
        }

        // Called by LocalCuePickupTrigger once its FBBIK reach-and-grab
        // finishes and it has already called LocalGrabbable.PickUp() on the
        // cue itself — this only updates the bookkeeping (HeldObject,
        // read by IsHoldingCue/turn-gating/ExitAim/etc., and so this
        // script's own Update() correctly routes the next Interact press to
        // Drop() instead of TryPickUp()), it does not touch the cue's
        // Transform/Rigidbody itself.
        public void NotifyExternallyHeld(LocalGrabbable grabbable)
        {
            heldObject = grabbable;
        }
    }
}
