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
    }
}
