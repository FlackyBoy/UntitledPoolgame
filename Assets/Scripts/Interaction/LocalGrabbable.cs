using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Offline counterpart to Grabbable — same pickup/carry/drop behaviour, but
    // no networking at all: everything happens directly and locally, since
    // split-screen has no server to be authoritative through.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class LocalGrabbable : MonoBehaviour
    {
        [SerializeField] private Vector3 holdLocalPosition = new Vector3(0.3f, -0.3f, 0.6f);
        [SerializeField] private Vector3 holdLocalEulerAngles = new Vector3(70f, 0f, 0f);

        public Vector3 HoldLocalPosition => holdLocalPosition;
        public Quaternion HoldLocalRotation => Quaternion.Euler(holdLocalEulerAngles);
        public bool IsHeld { get; private set; }

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
            col.enabled = true;
            IsHeld = false;
        }
    }
}
