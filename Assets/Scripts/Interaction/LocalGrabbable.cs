using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Generic pickup/carry/drop for any physical object. While held:
    // kinematic, collider off, parented to the holder.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class LocalGrabbable : MonoBehaviour
    {
        [SerializeField] private Vector3 holdLocalPosition = new Vector3(0.3f, -0.3f, 0.6f);
        [SerializeField] private Vector3 holdLocalEulerAngles = new Vector3(70f, 0f, 0f);

        public Vector3 HoldLocalPosition => holdLocalPosition;
        public Quaternion HoldLocalRotation => Quaternion.Euler(holdLocalEulerAngles);
        public bool IsHeld { get; private set; }

        // Carries a Cue: read every frame by the hands, the aim, the melee
        // and the animation, so looked up once (Awake).
        public bool IsCue { get; private set; }

        // For the cue, whose attachment is done by PickUpCue: only keeps
        // IsHeld in sync (PickUp/Drop would fight the reach).
        public void MarkExternallyHeld(bool held)
        {
            IsHeld = held;

            // Kinematic, or the cue falls through the floor during the reach
            // and drags the body with it.
            if (rb != null) rb.isKinematic = held;

            // Solid colliders off while held, or the cue shoves the balls.
            // Triggers stay.
            foreach (Collider solid in GetComponents<Collider>())
            {
                if (!solid.isTrigger) solid.enabled = !held;
            }
        }

        private Rigidbody rb;
        private Collider col;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            IsCue = TryGetComponent(out Cue _);
        }

        public void PickUp(Transform holder)
        {
            if (IsHeld) return;

            transform.SetParent(holder, worldPositionStays: false);
            transform.SetLocalPositionAndRotation(holdLocalPosition, Quaternion.Euler(holdLocalEulerAngles));

            rb.isKinematic = true;
            col.enabled = false;
            IsHeld = true;
        }

        public void Drop()
        {
            if (!IsHeld) return;

            transform.SetParent(null, worldPositionStays: true);
            // Nudged up so depenetration can push it out of whatever it's in.
            transform.position += Vector3.up * 0.1f;

            rb.isKinematic = false;
            // A thin object (e.g. the cue) released with any speed can
            // tunnel through the floor in a single physics step.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            col.enabled = true;
            IsHeld = false;
        }

        // Lets go of an object held by LocalObjectHands, with an optional
        // impulse (a throw).
        public void ReleaseFromHand(Vector3 impulse)
        {
            transform.SetParent(null, worldPositionStays: true);
            MarkExternallyHeld(false);
            if (rb == null) return;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (impulse != Vector3.zero) rb.AddForce(impulse, ForceMode.Impulse);
        }

        public Rigidbody Body => rb;

        // Drop, then the thrower's impulse (world space).
        public void Throw(Vector3 impulse)
        {
            if (!IsHeld) return;

            Drop();
            rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
