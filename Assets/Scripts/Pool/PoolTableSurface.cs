using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // On the felt collider: where placement rays hit the table, and the play
    // area balls are clamped to.
    public class PoolTableSurface : MonoBehaviour
    {
        // One table per scene in practice — lets the aim controllers find it
        // for ball-in-hand placement without a hand-wired Inspector reference.
        public static PoolTableSurface Instance { get; private set; }

        [SerializeField] private float halfLength;
        [SerializeField] private float halfWidth;

        public float HalfLength => halfLength;
        public float HalfWidth => halfWidth;

        public void Configure(float halfLength, float halfWidth)
        {
            this.halfLength = halfLength;
            this.halfWidth = halfWidth;
        }

        // The felt's own collider: a power aimed at the table raycasts it
        // alone (nothing in the way counts), and its top is the felt height.
        public Collider Felt { get; private set; }
        public float FeltTopY => Felt != null ? Felt.bounds.max.y : transform.position.y;

        private void Awake()
        {
            Instance = this;
            Felt = GetComponent<Collider>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Clamps a point to the felt, pulled in by margin (usually the ball's
        // radius). Rotation and offset only: the surface's non-uniform scale
        // would turn InverseTransformPoint's result into -0.5..0.5 units.
        public Vector3 ClampToPlayArea(Vector3 worldPosition, float margin)
        {
            Vector3 offset = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
            offset.x = Mathf.Clamp(offset.x, -halfLength + margin, halfLength - margin);
            offset.z = Mathf.Clamp(offset.z, -halfWidth + margin, halfWidth - margin);
            offset.y = 0f;
            return transform.position + transform.rotation * offset;
        }

        // How far a point going along direction travels before leaving the
        // table rectangle (inflated by margin); 0 if already out. Used to
        // stand the player behind the cue.
        public float DistanceToClearPlayArea(Vector3 origin, Vector3 direction, float margin)
        {
            Quaternion inverseRotation = Quaternion.Inverse(transform.rotation);
            Vector3 localOrigin = inverseRotation * (origin - transform.position);
            Vector3 localDirection = inverseRotation * direction;

            float exitViaX = AxisExitDistance(localOrigin.x, localDirection.x, halfLength + margin);
            float exitViaZ = AxisExitDistance(localOrigin.z, localDirection.z, halfWidth + margin);
            float distance = Mathf.Min(exitViaX, exitViaZ);
            return float.IsInfinity(distance) ? 0f : distance;
        }

        // Distance along one axis until |pos| reaches bound; 0 if already
        // there, infinity if moving away or parallel.
        private static float AxisExitDistance(float pos, float dir, float bound)
        {
            if (Mathf.Abs(pos) >= bound) return 0f;
            if (Mathf.Approximately(dir, 0f)) return float.PositiveInfinity;

            float target = dir > 0f ? bound : -bound;
            float distance = (target - pos) / dir;
            return distance > 0f ? distance : float.PositiveInfinity;
        }
    }
}
