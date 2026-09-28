using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Marker on the table's felt collider — lets ball-in-hand placement (see
    // PoolMatchRules.BallInHand and the aim controllers' placement mode) find
    // exactly where a player's look-ray crosses the table, regardless of
    // whatever ball/rail/cue happens to be in the way along that ray, and
    // keeps the placed ball from being dropped through a rail or off the felt.
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

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Clamps a world position to the playable felt area (this object's
        // local X/Z), pulled in by margin (typically the ball's own radius)
        // so it can't be placed overlapping a rail or off the table.
        //
        // Deliberately NOT transform.InverseTransformPoint/TransformPoint:
        // those divide by this object's own scale, but the Surface is built
        // with localScale = (Play Length, thickness, Play Width) — a huge,
        // non-uniform scale — so the "local" coordinates they produce are
        // normalized to roughly -0.5..0.5, not real meters, while
        // halfLength/halfWidth ARE real meters. Clamping the tiny normalized
        // value against the much larger meter bounds meant the clamp almost
        // never actually triggered, letting the ball be placed anywhere. This
        // instead only rotates and translates (never scales) to get a
        // true-meters offset from this object's position, so the bounds
        // check compares matching units regardless of the Surface's scale.
        public Vector3 ClampToPlayArea(Vector3 worldPosition, float margin)
        {
            Vector3 offset = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
            offset.x = Mathf.Clamp(offset.x, -halfLength + margin, halfLength - margin);
            offset.z = Mathf.Clamp(offset.z, -halfWidth + margin, halfWidth - margin);
            offset.y = 0f;
            return transform.position + transform.rotation * offset;
        }

        // Opposite of ClampToPlayArea: how far a point walking along direction
        // (from origin) has to travel before it's outside the table's
        // rectangle (this object's local X/Z, inflated by margin) — used to
        // stand a player behind the cue without ending up on top of/inside
        // the table when the cue ball sits well inside the rails. Returns 0
        // if origin is already outside. Same rotation-only local frame as
        // ClampToPlayArea (never divides by Surface's own non-uniform scale).
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

        // Distance along dir (from pos, on a single axis) until |coordinate|
        // reaches bound. 0 if already at/past the bound; +infinity if moving
        // parallel to it or away from it (never reaches it going forward).
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
