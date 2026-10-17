using UnityEngine;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Player
{
    // One per ragdoll bone, next to its Muscle Collision Broadcaster. On a
    // strong enough hit it re-arms BehaviourPuppet's collision gate
    // (LocalPlayerRagdollController.NotifyCollision) and pushes this bone at
    // the contact point by the thrown object's momentum: the pins resist the
    // plain collision, this makes the reaction scale with the throw.
    public class RagdollHitRelay : MonoBehaviour
    {
        [SerializeField] private float minImpactSpeed = 3f;
        [SerializeField] private float impactForceMultiplier = 3f;

        private LocalPlayerRagdollController ragdollController;
        private Rigidbody muscleRigidbody;

        private void Awake()
        {
            ragdollController = GetComponentInParent<LocalPlayerRagdollController>();
            muscleRigidbody = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (ragdollController == null) return;
            if (!collision.gameObject.TryGetComponent(out LocalGrabbable grabbable)) return;
            if (grabbable.IsHeld) return;
            if (collision.rigidbody == null) return;

            float speed = collision.rigidbody.linearVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            ragdollController.NotifyCollision();

            if (muscleRigidbody != null)
            {
                Vector3 impulse = collision.rigidbody.linearVelocity * collision.rigidbody.mass * impactForceMultiplier;
                muscleRigidbody.AddForceAtPosition(impulse, collision.GetContact(0).point, ForceMode.Impulse);
            }
        }
    }
}
