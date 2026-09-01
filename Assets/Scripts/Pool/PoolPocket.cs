using UnityEngine;

namespace UntitledPoolGame.Pool
{
    [RequireComponent(typeof(SphereCollider))]
    public class PoolPocket : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<SphereCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PoolBall ball))
                ball.OnPocketed();
        }

        // Always-on (not just when selected) so all 6 pockets can be compared
        // to a custom table's real pocket openings at once — the generated
        // positions are only an idealized-rectangle approximation and often
        // need nudging/resizing to line up with a specific model.
        private void OnDrawGizmos()
        {
            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, collider.radius * transform.lossyScale.x);
        }
    }
}
