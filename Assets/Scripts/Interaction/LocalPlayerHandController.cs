using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Detects nearby LocalGrabbable objects and picks them up / drops them
    // with Interact, deferring to LocalPoolAimController when it wants
    // Interact instead (entering/exiting aim mode with the cue in hand).
    [RequireComponent(typeof(LocalFpsPlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPlayerHandController : MonoBehaviour
    {
        [SerializeField] private float pickupRange = 2f;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string attackActionName = "Attack";

        // Optional — an object this player starts the game holding. Runs
        // through the exact same PickUp() as a normal pickup (see Start)
        // rather than a separate "already held" code path, so it ends up in
        // an identical state (kinematic, collider off, IsHeld true) to one
        // picked up mid-game — including being droppable/re-pickable later.
        // Not for the cue, which is only ever picked up through
        // LocalCuePickupTrigger.
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
        // The procedural pickup: takes over the cue whenever it's present
        // and enabled; disable it to fall back to cuePickupTrigger.
        private LocalCueHolder cueHolder;
        // Hands-on pickup/carry/throw of ordinary objects; disabled, objects
        // fall back to LocalGrabbable.PickUp/Throw (stuck in front of the player).
        private LocalObjectHands objectHands;
        private PlayerInput playerInput;
        private InputAction interactAction;
        private InputAction attackAction;
        private LocalGrabbable heldObject;
        private float chargedThrowPower;
        private bool throwArmed;   // Attack pressed since the object is in hand

        public LocalGrabbable HeldObject => heldObject;

        private bool CueHolderActive => cueHolder != null && cueHolder.isActiveAndEnabled;

        // Cue pickup state, whichever system runs it: busy from the reach
        // until the hands have let go; settled once held and done moving
        // into the carry pose (others may then move the cue).
        public bool CueBusy => CueHolderActive
            ? cueHolder.IsBusy
            : cuePickupTrigger != null && cuePickupTrigger.PickUpCue != null && cuePickupTrigger.PickUpCue.IsBusy;
        public bool CueSettled => CueHolderActive
            ? cueHolder.IsSettled
            : cuePickupTrigger == null || cuePickupTrigger.PickUpCue == null || cuePickupTrigger.PickUpCue.IsSettled;

        private bool ObjectHandsActive => objectHands != null && objectHands.isActiveAndEnabled;

        // The hands are in the middle of a gesture (reaching for, lifting,
        // throwing or letting go of the cue or an object): nothing else
        // should drive them (punches, another pickup).
        public bool HandsMoving =>
            (CueBusy && !CueSettled) ||
            (ObjectHandsActive && objectHands.IsBusy && !objectHands.IsSettled);

        private void Awake()
        {
            poolAimController = GetComponent<LocalPoolAimController>();
            cuePickupTrigger = GetComponent<LocalCuePickupTrigger>();
            // The procedural cue pickup needs no wiring to any cue, so it's
            // added to every player that doesn't carry one — spawned players
            // included (the old LocalCuePickupTrigger route needs a cue
            // assigned per player in the scene, which a spawned player never
            // gets). A LocalCueHolder placed on the prefab and disabled is
            // left as is: that's the way back to the old route.
            cueHolder = GetComponent<LocalCueHolder>();
            if (cueHolder == null) cueHolder = gameObject.AddComponent<LocalCueHolder>();
            // Same for ordinary objects (a disabled one on the prefab = the
            // old carry, stuck in front of the player).
            objectHands = GetComponent<LocalObjectHands>();
            if (objectHands == null) objectHands = gameObject.AddComponent<LocalObjectHands>();
            // Powers: activating one's own (Next) and undergoing the
            // opponent's. Neither needs wiring, and the current player
            // prefab had lost both (no power could be triggered any more).
            if (GetComponent<LocalPoolPowerController>() == null) gameObject.AddComponent<LocalPoolPowerController>();
            if (GetComponent<LocalPoolPowerEffectReceiver>() == null) gameObject.AddComponent<LocalPoolPowerEffectReceiver>();
            playerInput = GetComponent<PlayerInput>();
            // The throw direction: the player's own camera when left empty.
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null) cameraTransform = cam.transform;
            }

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
                heldObject.PickUp(transform);
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
        // charge/release gesture as LocalPoolAimController's cue shot, just
        // applied to whatever's currently in hand instead of the cue ball.
        // Excludes the cue itself: it's not meant to be thrown away, and
        // this frees Attack up for LocalPoolAimController.UpdateAim() to
        // read on its own once the player actually enters aim mode with it.
        private void HandleThrow()
        {
            if (heldObject == null || IsCue(heldObject))
            {
                chargedThrowPower = 0f;
                throwArmed = false;
                return;
            }
            // Hands still reaching/lifting, or already mid-throw.
            if (ObjectHandsActive && objectHands.IsBusy && !objectHands.IsSettled)
            {
                throwArmed = false;
                return;
            }

            // Only a press made once the object is in hand starts a throw: an
            // Attack still held from before (e.g. a punch's click) went
            // straight into the windup right after the pickup.
            if (attackAction.WasPressedThisFrame()) throwArmed = true;
            if (!throwArmed)
            {
                chargedThrowPower = 0f;
                return;
            }

            if (attackAction.IsPressed())
            {
                chargedThrowPower = Mathf.Min(chargedThrowPower + throwChargeSpeed * Time.deltaTime, maxThrowPower);
                // The arm cocks back with the charge (0 → 1 over the power range).
                if (ObjectHandsActive)
                    objectHands.SetCharge(Mathf.InverseLerp(0f, maxThrowPower, chargedThrowPower));
            }
            else if (chargedThrowPower > 0f)
            {
                throwArmed = false;
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
            // With the hands: the object stays held until it leaves the hand
            // partway through the swing (heldObject cleared then).
            if (ObjectHandsActive && objectHands.TryThrow(thrown, impulse, () => { if (heldObject == thrown) heldObject = null; }))
                return;
            heldObject = null;
            thrown.Throw(impulse);
        }

        private static bool IsCue(LocalGrabbable heldObject) =>
            heldObject != null && heldObject.TryGetComponent(out Cue _);

        private void TryPickUp()
        {
            // Mid-gesture (e.g. arms coming back after a throw): wait.
            if (HandsMoving) return;

            // The cue's own pickup (FBBIK reach-and-grab, gated by its
            // InteractionTrigger) is tried first, through this same call
            // chain rather than a separate Update() reading Interact
            // independently (that raced against Drop() on the same press).
            if (CueHolderActive)
            {
                if (cueHolder.TryStartPickup()) return;
            }
            else if (cuePickupTrigger != null && cuePickupTrigger.TryStartPickup())
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent(out LocalGrabbable grabbable) || grabbable.IsHeld)
                    continue;

                // The cue is handled exclusively by LocalCuePickupTrigger.
                if (grabbable.TryGetComponent(out Cue _))
                    continue;

                heldObject = grabbable;
                if (!ObjectHandsActive || !objectHands.TryStartPickup(grabbable))
                    grabbable.PickUp(transform);
                return;
            }
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
            // The cue releases through its InteractionSystem interaction
            // (PickUpCue.ReleaseCue) instead of a plain LocalGrabbable.Drop().
            // Mid-throw the throw itself lets go (and clears heldObject).
            if (ObjectHandsActive && objectHands.IsThrowing) return;

            bool released = CueHolderActive
                ? cueHolder.TryRelease(heldObject)
                : cuePickupTrigger != null && cuePickupTrigger.TryReleaseIfCue(heldObject);
            if (!released && ObjectHandsActive)
                released = objectHands.TryDrop(heldObject);
            if (!released)
                heldObject.Drop();

            heldObject = null;
        }

        // Same as Drop() above, but callable from outside — used by
        // LocalPlayerRagdollController to force-release whatever's in hand
        // right before ragdolling (can't stay gripping something while
        // physics takes over the whole body).
        public void ForceDrop()
        {
            // Knocked down mid-gesture (even mid-throw): let go at once.
            if (ObjectHandsActive && objectHands.IsBusy)
            {
                objectHands.ReleaseNow();
                if (heldObject != null && !IsCue(heldObject)) heldObject = null;
            }
            if (heldObject != null) Drop();
        }

        // Called by LocalCuePickupTrigger as soon as its FBBIK reach-and-grab
        // starts — this only updates the bookkeeping (HeldObject,
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
