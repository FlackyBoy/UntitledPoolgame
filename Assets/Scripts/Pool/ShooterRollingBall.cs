using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A ball a power sends rolling on the felt (ShooterBigBallPower,
    // ShooterHomingBallPower). Not a PoolBall: it's not in the rules' count and
    // no pocket takes it. On the balls' layer, so it pushes them and stops on
    // the cushions. Hits on players are tested by the rule set.
    public class ShooterRollingBall : MonoBehaviour
    {
        // Rolling away, the shooter is still in reach.
        private const float OwnerGrace = 0.6f;
        private const float BelowFelt = 0.2f;

        public struct Spec
        {
            public float radius, mass, speed, damage, lifetime, steering;
            public Color color;
            public bool spentOnHit;
            public string label;
        }

        private static readonly List<ShooterRollingBall> active = new List<ShooterRollingBall>();
        public static IReadOnlyList<ShooterRollingBall> Active => active;

        private ShooterMiniRuleSet rules;
        private Spec spec;
        private Rigidbody rb;
        private Transform chased;
        private float dieAt;
        private float ownerSafeUntil;

        public int Owner { get; private set; }
        public float Radius => spec.radius;
        public float Damage => spec.damage;
        public string Label => spec.label;
        public Vector3 Velocity => rb.linearVelocity;
        public bool OwnerSafe => Time.time < ownerSafeUntil;

        public static ShooterRollingBall Launch(ShooterMiniRuleSet rules, int owner, Spec spec, Vector3 position, Vector3 direction, Transform chased)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Shooter " + spec.label;
            sphere.layer = LayerMask.NameToLayer("Poolball");
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * (spec.radius * 2f);
            sphere.GetComponent<Renderer>().sharedMaterial = ShooterPowerFx.Solid(spec.color, spec.label);

            ShooterRollingBall ball = sphere.AddComponent<ShooterRollingBall>();
            ball.rules = rules;
            ball.Owner = owner;
            ball.spec = spec;
            ball.chased = chased;
            ball.dieAt = Time.time + spec.lifetime;
            ball.ownerSafeUntil = Time.time + OwnerGrace;
            ball.rb = sphere.AddComponent<Rigidbody>();
            ball.rb.mass = spec.mass;
            ball.rb.linearDamping = 0.1f;
            ball.rb.angularDamping = 0.1f;
            ball.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ball.rb.linearVelocity = direction * spec.speed;
            return ball;
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private void OnDestroy()
        {
            Renderer shown = GetComponent<Renderer>();
            if (shown != null) Destroy(shown.sharedMaterial);
        }

        private void FixedUpdate()
        {
            if (chased == null) return;
            Vector3 to = chased.position - rb.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-6f) return;
            Vector3 flat = rb.linearVelocity;
            flat.y = 0f;
            rb.AddForce((to.normalized * spec.speed - flat) * spec.steering, ForceMode.Acceleration);
        }

        private void Update()
        {
            if (Time.time >= dieAt || transform.position.y < rules.FeltY - BelowFelt
                || (rules.TryHitWithRoller(this) && spec.spentOnHit))
                Destroy(gameObject);
        }
    }
}
