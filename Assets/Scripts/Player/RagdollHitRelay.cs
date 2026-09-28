using UnityEngine;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Player
{
    // One per ragdoll bone (alongside Muscle Collision Broadcaster). On a
    // qualifying hit, does two things: tells LocalPlayerRagdollController
    // to re-arm BehaviourPuppet's hasCollidedSinceGetUp gate (see
    // LocalPlayerRagdollController.NotifyCollision for why this is needed
    // on top of PuppetMaster's own native collision detection), and applies
    // its own extra impulse directly to this specific bone, at the exact
    // contact point, scaled by the thrown object's own momentum (mass ×
    // velocity). The raw Unity collision already transfers some momentum
    // on its own, but PuppetMaster's pin force resists it quite hard, so a
    // "realistic" 1:1 transfer barely reads once the muscle is still
    // fighting to stay pinned — this is what actually makes the reaction
    // scale visibly with how hard something was thrown, and land
    // differently depending on which bone/point was hit (torque falls out
    // of AddForceAtPosition automatically from the contact point's offset
    // from that bone's own center of mass).
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
