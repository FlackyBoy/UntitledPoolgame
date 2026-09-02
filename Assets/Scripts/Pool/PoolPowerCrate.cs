using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A Mario Kart-style item box sitting on the felt — whoever's shooting
    // gets the power the moment the cue ball rolls close enough to it. Power
    // + color are assigned by PoolPowerCrateManager (random draw from
    // PoolPowerSpawnSettings.availablePowers), not fixed in the Inspector —
    // this component only handles detection/visuals, the manager owns the
    // spawn/respawn lifecycle (which location, when, with what).
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
        [SerializeField] private float pickupRadius = 0.15f;

        private PoolPower power;
        private Renderer visualRenderer;
        private bool collected;

        private void Awake()
        {
            // So there's actually something to see on the table — without
            // this, an empty GameObject with just this component is invisible.
            visualRenderer = GetComponentInChildren<Renderer>();
            if (visualRenderer == null)
                visualRenderer = CreatePlaceholderVisual();
        }

        private Renderer CreatePlaceholderVisual()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(transform, worldPositionStays: false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * (pickupRadius * 1.2f);
            Destroy(visual.GetComponent<Collider>()); // purely visual — pickup logic below doesn't use colliders at all
            return visual.GetComponent<Renderer>();
        }

        // Called by PoolPowerCrateManager whenever this crate (re)spawns —
        // rolls the visual color to match the power's PowerType and clears
        // the collected flag so it's pickable again.
        public void Initialize(PoolPower assignedPower, Color color)
        {
            power = assignedPower;
            collected = false;
            // .material (not .sharedMaterial) instances a per-object copy —
            // safe to recolor without touching every other crate/ball that
            // happens to share the same source material asset.
            if (visualRenderer != null) visualRenderer.material.color = color;
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
            PoolPowerCrateManager.Instance?.NotifyCollected(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
