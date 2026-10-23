using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // An item box on the felt: the shooter gets its power when the cue ball
    // rolls close enough. Power and visual are set by PoolPowerCrateManager.
    // A distance check every FixedUpdate rather than a trigger, which a fast
    // ball can cross between two steps; also simpler to see (gizmo).
    public class PoolPowerCrate : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 0.15f;

        private PoolPower power;
        private bool collected;

        // The visual is only swapped when the prefab changes.
        private GameObject currentVisual;
        private GameObject currentVisualPrefab;
        // Only for the placeholder; a real prefab is never tinted.
        private Renderer placeholderRenderer;

        // Called on each (re)spawn. prefab null = tinted placeholder cube.
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
            SitOnFelt();
        }

        // A token's origin is the felt: the visual is moved onto it, spawn
        // points aren't all at felt height. The pickup point doesn't move.
        private void SitOnFelt()
        {
            if (currentVisual == null || placeholderRenderer != null) return;
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return;
            Vector3 p = transform.position;
            currentVisual.transform.position = new Vector3(p.x, surface.FeltTopY, p.z);
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
            // Not while the cue ball is being placed.
            if (rules.BallInHand) return;

            // Any cue ball: Speed Pool has one per player, and the power goes
            // to that ball's owner (the shooter otherwise).
            PoolBall cueBall = null;
            foreach (PoolBall ball in PoolBall.Active)
            {
                // Parked (Speed Pool scratch) at its spawn: not rolling over anything.
                if (!ball.IsCueBall || ball.Rigidbody.isKinematic) continue;
                float reach = pickupRadius + ball.Radius;
                if ((transform.position - ball.transform.position).sqrMagnitude > reach * reach) continue;
                cueBall = ball;
                break;
            }
            if (cueBall == null) return;

            collected = true;
            rules.GrantPower(rules.IsSimultaneous && cueBall.CueOwner >= 0 ? cueBall.CueOwner : rules.CurrentPlayer, power);
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
