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

        // Cue-only: PickUpCue/InteractionSystem owns the cue's actual
        // attachment (parenting, kinematic, collider, reach animation) —
        // calling PickUp()/Drop() here as well would fight that (snap to
        // holdLocalPosition, then get overridden by the reach animation
        // next frame). This only keeps IsHeld in sync so it still reads
        // correctly everywhere else (this method's own guard included).
        public void MarkExternallyHeld(bool held)
        {
            IsHeld = held;

            // With the collider off (below) and gravity still on, the cue
            // would fall through the floor during the hands' approach —
            // PickUpCue only makes it kinematic later, once they arrive —
            // and its grip points, which the hands chase, would drag the
            // body down through the floor with it. Undone on release: a
            // normal release already did it (PickUpCue.OnDrop), but a reach
            // cut short never got that far.
            if (rb != null) rb.isKinematic = held;

            // PickUp()/Drop() turn the collider off/on; this path skips them,
            // so without this the cue's solid collider stays live while it's
            // in the hands and shoves the balls (and climbs onto them) when
            // it moves. Triggers (e.g. the pickup zone) are left alone.
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
            // The collider was off while held, so wherever it currently sits may
            // be embedded in something solid — nudge it up before physics
            // resumes so depenetration has room to push it out cleanly.
            transform.position += Vector3.up * 0.1f;

            rb.isKinematic = false;
            // A thin object (e.g. the cue) released with any speed can
            // tunnel through the floor in a single physics step.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            col.enabled = true;
            IsHeld = false;
        }

        // Drop, then give it a shove — impulse is already a full world-space
        // force vector (direction * power), computed by the thrower (who
        // knows their own look direction), not this object.
        public void Throw(Vector3 impulse)
        {
            if (!IsHeld) return;

            Drop();
            rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
