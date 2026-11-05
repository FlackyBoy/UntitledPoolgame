using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A magnet planted on the felt (ShooterMagnetPower): draws the table's
    // balls in for its duration.
    public class ShooterMagnet : MonoBehaviour
    {
        private const float DiscRadius = 0.02f;
        private const float DiscHeight = 0.008f;
        private static readonly Color Look = new Color(0.85f, 0.15f, 0.2f);

        private float radius, acceleration;

        public static ShooterMagnet Plant(Vector3 onFelt, float duration, float radius, float acceleration, GameObject vfxPrefab, float vfxScale)
        {
            GameObject disc = ShooterPowerFx.Shape(PrimitiveType.Cylinder, ShooterPowerFx.Solid(Look, "Shooter Magnet"), "Shooter Magnet");
            disc.transform.position = onFelt + Vector3.up * DiscHeight;
            disc.transform.localScale = new Vector3(DiscRadius * 2f, DiscHeight, DiscRadius * 2f);
            ShooterMagnet magnet = disc.AddComponent<ShooterMagnet>();
            magnet.radius = radius;
            magnet.acceleration = acceleration;
            ShooterPowerFx.Effect(vfxPrefab, onFelt, vfxScale, duration);
            Destroy(disc, duration);
            return magnet;
        }

        private void FixedUpdate()
        {
            Vector3 at = transform.position;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.Rigidbody.isKinematic) continue;
                Vector3 to = at - ball.transform.position;
                to.y = 0f;
                float distanceSqr = to.sqrMagnitude;
                if (distanceSqr > radius * radius || distanceSqr < 1e-6f) continue;
                ball.Rigidbody.AddForce(to / Mathf.Sqrt(distanceSqr) * acceleration, ForceMode.Acceleration);
            }
        }
    }
}
