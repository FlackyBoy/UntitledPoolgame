using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: a crate object that does something to the balls once
    // placed (PlayAnywhereProp): portal, pipe, fan, spring. Its zones are
    // boxes in the object's own space (they turn and scale with it), tested
    // against both balls every physics step, like PlayAnywhereOutOfBounds:
    // no trigger colliders, which PlayAnywhereProp.Spawn may replace. The
    // test follows the ball's path since the last step, so a fast ball
    // can't skip a thin zone. In the crate or in hand, it does nothing.
    // Dropped straight in the scene (no PlayAnywhereProp, even during
    // play), it works as if placed: a quick way to try one.
    public abstract class PlayAnywhereGadget : MonoBehaviour
    {
        private PlayAnywhereProp prop;
        private bool propLooked;
        // Per player, the ball's position last step, in this object's space.
        private readonly Vector3[] lastLocal = new Vector3[2];
        private readonly bool[] hasLast = new bool[2];
        private readonly float[] cooldownUntil = new float[2];
        // Farther than this in one step, the ball was moved (out of bounds,
        // a pipe): its path doesn't count.
        private const float TeleportDistance = 0.6f;

        private void FixedUpdate()
        {
            // Added by Spawn right after this object is made.
            if (!propLooked)
            {
                prop = GetComponent<PlayAnywhereProp>();
                propLooked = true;
            }
            PlayAnywhereRuleSet rules = PlayAnywhereRuleSet.Current;
            if ((prop != null && !prop.IsPlaced) || rules == null) return;

            for (int p = 0; p < 2; p++)
            {
                PoolBall ball = rules.PlayableBall(p);
                if (ball == null)
                {
                    hasLast[p] = false;
                    continue;
                }
                Vector3 world = ball.Rigidbody.position;
                Vector3 local = transform.InverseTransformPoint(world);
                bool moved = hasLast[p] && (transform.TransformPoint(lastLocal[p]) - world).sqrMagnitude > TeleportDistance * TeleportDistance;
                Vector3 from = hasLast[p] && !moved ? lastLocal[p] : local;
                lastLocal[p] = local;
                hasLast[p] = true;
                if (Time.fixedTime < cooldownUntil[p]) continue;
                Act(rules, p, ball, from, local);
            }
        }

        // from → to: the ball's path this step, in this object's space.
        protected abstract void Act(PlayAnywhereRuleSet rules, int player, PoolBall ball, Vector3 from, Vector3 to);

        // Not acted on again for a moment (the ball leaves the zone first).
        protected void Cooldown(int player, float seconds) => cooldownUntil[player] = Time.fixedTime + seconds;

        // The ball was moved by this object: next step's path starts there.
        protected void MarkMoved(int player, PoolBall ball) => lastLocal[player] = transform.InverseTransformPoint(ball.Rigidbody.position);

        // Horizontal, in the world: where this object sends a ball.
        protected Vector3 FlatDirection(Vector3 localDirection)
        {
            Vector3 d = Vector3.ProjectOnPlane(transform.TransformDirection(localDirection), Vector3.up);
            return d.sqrMagnitude > 1e-6f ? d.normalized : transform.forward;
        }

        protected static bool Inside(Vector3 point, Vector3 center, Vector3 size)
        {
            Vector3 d = point - center;
            return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.y) <= size.y * 0.5f && Mathf.Abs(d.z) <= size.z * 0.5f;
        }

        // The segment from → to touches the box (slab test). A still ball
        // inside counts.
        protected static bool Crosses(Vector3 from, Vector3 to, Vector3 center, Vector3 size)
        {
            Vector3 a = from - center, d = to - from, half = size * 0.5f;
            float t0 = 0f, t1 = 1f;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-6f)
                {
                    if (Mathf.Abs(a[i]) > half[i]) return false;
                    continue;
                }
                float ta = (-half[i] - a[i]) / d[i], tb = (half[i] - a[i]) / d[i];
                if (ta > tb) (ta, tb) = (tb, ta);
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1) return false;
            }
            return true;
        }

        // Sent off at this velocity, already rolling (no slide to brake it).
        protected static void Launch(PoolBall ball, Vector3 velocity)
        {
            Rigidbody rb = ball.Rigidbody;
            rb.linearVelocity = velocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            rb.angularVelocity = Vector3.Cross(Vector3.up, flat) / Mathf.Max(0.001f, ball.Radius);
        }

        protected static void Log(PlayAnywhereRuleSet rules, string message, Object context)
        {
            if (rules.Settings.debugLogs) Debug.Log($"[PlayAnywhere] {message}", context);
        }

        // The zones in the Scene view, to check them against the meshes.
        protected abstract void DrawZones();

        protected static void DrawZone(Vector3 center, Vector3 size, Color colour)
        {
            Gizmos.color = colour;
            Gizmos.DrawWireCube(center, size);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            DrawZones();
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
