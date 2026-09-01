using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A Mario Kart-style item box sitting on the felt — whoever's shooting
    // gets the power the moment the cue ball rolls close enough to it.
    // Single-use for now (disables itself on pickup); a respawn-after-a-delay
    // pass can come later if the table feels too empty after the first pickup.
    //
    // Checked explicitly every FixedUpdate via a distance test rather than
    // Collider/OnTriggerEnter: Unity's built-in trigger events are still a
    // discrete per-physics-step overlap check under the hood (continuous
    // collision detection only protects SOLID collisions from tunneling, not
    // triggers), so a ball moving fast enough could in principle cross a thin
    // trigger zone between two steps and never fire Enter at all. A manual
    // distance check has the same one-check-per-step limitation in theory,
    // but removes any doubt about Rigidbody/trigger-pairing requirements and
    // makes the pickup radius trivial to see (gizmo) and reason about.
    public class PoolPowerCrate : MonoBehaviour
    {
        [SerializeField] private PoolPower power;
        [SerializeField] private float pickupRadius = 0.15f;

        private bool collected;

        private void Awake()
        {
            // So there's actually something to see on the table — without
            // this, an empty GameObject with just this component is invisible.
            if (GetComponentInChildren<Renderer>() == null)
                CreatePlaceholderVisual();
        }

        private void CreatePlaceholderVisual()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(transform, worldPositionStays: false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * (pickupRadius * 1.2f);
            Destroy(visual.GetComponent<Collider>()); // purely visual — pickup logic below doesn't use colliders at all
        }

        private void FixedUpdate()
        {
            if (collected || power == null) return;

            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.MatchStarted) return;
            // While the cue ball is being dragged around during ball-in-hand
            // placement (top-down view after a foul), it isn't "rolling
            // through" anything — it's just being repositioned. Without this,
            // simply carrying it near a crate while choosing where to place it
            // would pick the power up too.
            if (rules.BallInHand) return;

            PoolBall cueBall = PoolBall.FindCueBall();
            if (cueBall == null) return;

            float distance = Vector3.Distance(transform.position, cueBall.transform.position);
            if (distance > pickupRadius + cueBall.Radius) return;

            collected = true;
            rules.GrantPower(rules.CurrentPlayer, power);
            gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
