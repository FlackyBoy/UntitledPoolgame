using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A grenade thrown on a curve (ShooterGrenadePower). On the balls' layer,
    // like the shots: it bounces off the table and the balls, and passes through
    // bodies. It only goes off at the end of its fuse: time it, or bounce it
    // behind a ball.
    public class ShooterGrenade : MonoBehaviour
    {
        private const float BelowFelt = 0.2f;
        private const float RollingDamping = 2f;
        // The lamp speeds up as the fuse runs out.
        private const float BlinkPerSecond = 3f;
        private const float LastBlinkPerSecond = 10f;
        private const float LastBlinkTime = 0.5f;
        private static readonly Color Body = new Color(0.2f, 0.32f, 0.16f);
        private static readonly Color Lit = new Color(1f, 0.25f, 0.05f);

        public struct Spec
        {
            public float radius, mass, fuse, bounciness, blastRadius, damage, knockSpeed, liftSpeed, ballSpeed, explosionScale;
            public GameObject explosionPrefab;
            public string label;
        }

        private ShooterMiniRuleSet rules;
        private int owner;
        private Spec spec;
        private float explodeAt;
        private Material look;
        private PhysicsMaterial bounce;

        public static ShooterGrenade Throw(ShooterMiniRuleSet rules, int owner, Spec spec, Vector3 position, Vector3 velocity, Collider ownerBody)
        {
            Material material = ShooterPowerFx.Solid(Body, spec.label);
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Shooter " + spec.label;
            sphere.layer = LayerMask.NameToLayer("Poolball");
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * (spec.radius * 2f);
            sphere.GetComponent<Renderer>().sharedMaterial = material;

            ShooterGrenade grenade = sphere.AddComponent<ShooterGrenade>();
            grenade.rules = rules;
            grenade.owner = owner;
            grenade.spec = spec;
            grenade.explodeAt = Time.time + spec.fuse;
            grenade.look = material;

            // Maximum: the felt's Minimum would otherwise kill every bounce.
            grenade.bounce = new PhysicsMaterial(spec.label)
            {
                bounciness = spec.bounciness,
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };
            Collider collider = sphere.GetComponent<Collider>();
            collider.sharedMaterial = grenade.bounce;
            if (ownerBody != null) Physics.IgnoreCollision(collider, ownerBody);

            Rigidbody rb = sphere.AddComponent<Rigidbody>();
            rb.mass = spec.mass;
            rb.angularDamping = RollingDamping;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            return grenade;
        }

        private void OnDestroy()
        {
            if (look != null) Destroy(look);
            if (bounce != null) Destroy(bounce);
        }

        private void Update()
        {
            float left = explodeAt - Time.time;
            if (left <= 0f)
            {
                Explode();
                return;
            }
            // Off the table: lost, it would only blow up in the room.
            if (transform.position.y < rules.FeltY - BelowFelt)
            {
                Destroy(gameObject);
                return;
            }
            float rate = left < LastBlinkTime ? LastBlinkPerSecond : BlinkPerSecond;
            look.SetColor("_BaseColor", Mathf.Repeat(Time.time * rate, 1f) < 0.5f ? Lit : Body);
        }

        private void Explode()
        {
            Vector3 at = transform.position;
            ShooterPowerFx.Effect(spec.explosionPrefab, at, spec.explosionScale, 3f);
            rules.Blast(at, owner, spec.blastRadius, spec.damage, spec.knockSpeed, spec.liftSpeed, spec.ballSpeed, spec.label);
            Destroy(gameObject);
        }
    }
}
