using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A Mario Kart-style item box sitting on the felt — whoever's shooting
    // gets the power the moment the cue ball rolls close enough to it. Power
    // + visual are assigned by PoolPowerCrateManager (random draw from
    // PoolPowerSpawnSettings.availablePowers, prefab from GetCratePrefab),
    // not fixed in the Inspector — this component only handles
    // detection/visuals, the manager owns the spawn/respawn lifecycle
    // (which location, when, with what).
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
        private bool collected;

        // Tracks what's currently instantiated so Initialize() only swaps
        // the visual when the assigned prefab actually changes (e.g. a
        // respawn rolled a different PowerType) instead of destroying and
        // recreating it every single time.
        private GameObject currentVisual;
        private GameObject currentVisualPrefab;
        // Only set when currentVisual is the auto-generated placeholder —
        // a user-supplied prefab is trusted to already look distinct for
        // its type, so it's never recolored (a flat material tint could
        // easily fight a real model's own materials/textures).
        private Renderer placeholderRenderer;

        // Called by PoolPowerCrateManager whenever this crate (re)spawns.
        // prefab may be null — falls back to a tinted placeholder cube for
        // whichever PowerType doesn't have one assigned in
        // PoolPowerSpawnSettings yet.
        public void Initialize(PoolPower assignedPower, GameObject prefab, Color color)
        {
            power = assignedPower;
            collected = false;

            if (currentVisual == null || prefab != currentVisualPrefab)
            {
                if (currentVisual != null) Destroy(currentVisual);
                currentVisualPrefab = prefab;
                currentVisual = prefab != null ? CreatePrefabVisual(prefab) : CreatePlaceholderVisual();
            }

            if (placeholderRenderer != null) placeholderRenderer.material.color = color;
        }

        private GameObject CreatePrefabVisual(GameObject prefab)
        {
            placeholderRenderer = null;
            GameObject visual = Instantiate(prefab, transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            return visual;
        }

        private GameObject CreatePlaceholderVisual()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(transform, worldPositionStays: false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * (pickupRadius * 1.2f);
            Destroy(visual.GetComponent<Collider>()); // purely visual — pickup logic below doesn't use colliders at all
            placeholderRenderer = visual.GetComponent<Renderer>();
            return visual;
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
