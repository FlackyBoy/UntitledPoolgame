using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Picks up and drops LocalGrabbable objects with Interact, unless
    // LocalPoolAimController wants Interact (entering/leaving aim).
    [RequireComponent(typeof(LocalFpsPlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPlayerHandController : MonoBehaviour
    {
        [SerializeField] private float pickupRange = 2f;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string attackActionName = "Attack";

        // Optional object held from the start, picked up through the normal
        // PickUp. Not for the cue.
        [SerializeField] private LocalGrabbable startingHeldObject;

        [Header("Throw (any held object except the cue)")]
        // Throwing reuses Attack: it's only read for the cue while aiming.
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

        // Cue pickup: busy from the reach until the hands let go; settled once
        // held and in the carry pose.
        public bool CueBusy => CueHolderActive
            ? cueHolder.IsBusy
            : cuePickupTrigger != null && cuePickupTrigger.PickUpCue != null && cuePickupTrigger.PickUpCue.IsBusy;
        public bool CueSettled => CueHolderActive
            ? cueHolder.IsSettled
            : cuePickupTrigger == null || cuePickupTrigger.PickUpCue == null || cuePickupTrigger.PickUpCue.IsSettled;

        private bool ObjectHandsActive => objectHands != null && objectHands.isActiveAndEnabled;

        // The hands are mid-gesture (reach, lift, throw, release): nothing
        // else should drive them.
        public bool HandsMoving =>
            (CueBusy && !CueSettled) ||
            (ObjectHandsActive && objectHands.IsBusy && !objectHands.IsSettled);

        private void Awake()
        {
            poolAimController = GetComponent<LocalPoolAimController>();
            cuePickupTrigger = GetComponent<LocalCuePickupTrigger>();
            // The procedural cue pickup needs no wiring, so it's added to every
            // player without one (spawned ones too). A disabled LocalCueHolder
            // on the prefab is left alone: that brings back the old route.
            cueHolder = GetComponent<LocalCueHolder>();
            if (cueHolder == null) cueHolder = gameObject.AddComponent<LocalCueHolder>();
            // Same for ordinary objects (a disabled one on the prefab = the
            // old carry, stuck in front of the player).
            objectHands = GetComponent<LocalObjectHands>();
            if (objectHands == null) objectHands = gameObject.AddComponent<LocalObjectHands>();
            // Powers (own and the opponent's): no wiring needed, added if missing.
            if (GetComponent<LocalPoolPowerController>() == null) gameObject.AddComponent<LocalPoolPowerController>();
            if (GetComponent<LocalPoolPowerEffectReceiver>() == null) gameObject.AddComponent<LocalPoolPowerEffectReceiver>();
            // Pointing at a power's target, thumbs up once sent.
            if (GetComponent<LocalPowerGesture>() == null) gameObject.AddComponent<LocalPowerGesture>();
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

        // Start, not Awake: the held object's own Awake must have run.
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

        // Hold Attack to charge a throw, release to throw, like the cue shot.
        // Not the cue.
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

            // Only a press made with the object in hand starts a throw (a held
            // Attack from a punch went straight into the windup).
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
            heldObject != null && heldObject.IsCue;

        private void TryPickUp()
        {
            // Mid-gesture (e.g. arms coming back after a throw): wait.
            if (HandsMoving) return;

            // The cue's own pickup first, in this same chain: a separate Update
            // raced against Drop on the same press.
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
                if (grabbable.IsCue)
                    continue;

                heldObject = grabbable;
                if (!ObjectHandsActive || !objectHands.TryStartPickup(grabbable))
                    grabbable.PickUp(transform);
                return;
            }
        }

        // Also used by LocalCuePickupTrigger.
        public bool CanUseCueNow()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules == null || rules.CanPlayerShoot(playerInput.playerIndex);
        }

        private void Drop()
        {
            // The cue is released through its interaction (PickUpCue). Mid-throw,
            // the throw lets go itself.
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

        // For the ragdoll: lets go of whatever is held.
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

        // Called when LocalCuePickupTrigger's reach starts: bookkeeping only
        // (HeldObject), the cue itself isn't touched.
        public void NotifyExternallyHeld(LocalGrabbable grabbable)
        {
            heldObject = grabbable;
        }
    }
}
