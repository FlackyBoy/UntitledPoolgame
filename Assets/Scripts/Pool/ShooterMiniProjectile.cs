using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A small ball fired by LocalShooterWeapon. On the balls' layer, so it
    // knocks them around and bounces off the table, and passes through bodies
    // like they do: hits on players are tested by the weapon. Reused rather
    // than instantiated at each shot.
    public class ShooterMiniProjectile : MonoBehaviour
    {
        // A last bounce may be on the body it's hitting: alive long enough for
        // the weapon's next test.
        private const float LastBounceGrace = 0.05f;

        private Rigidbody rb;
        private Renderer shown;
        private float dieAt;
        private int bouncesLeft;
        private int owner;

        // Where the weapon last tested it for a hit.
        public Vector3 LastTested { get; set; }

        // A freezing shot: how long a player it hits stays frozen (0 = a plain
        // shot), and a ball.
        public float FreezePlayerTime { get; private set; }
        private float freezeBallTime;

        public static ShooterMiniProjectile Create(ShooterMiniSettings cfg, Material material, PhysicsMaterial bounce, Collider ownerBody, int owner)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ShooterMini Shot";
            sphere.layer = LayerMask.NameToLayer("Poolball");
            sphere.transform.localScale = Vector3.one * (cfg.shotRadius * 2f);
            sphere.GetComponent<Renderer>().sharedMaterial = material;

            Collider body = sphere.GetComponent<Collider>();
            body.sharedMaterial = bounce;
            if (ownerBody != null) Physics.IgnoreCollision(body, ownerBody);

            ShooterMiniProjectile shot = sphere.AddComponent<ShooterMiniProjectile>();
            shot.owner = owner;
            shot.shown = sphere.GetComponent<Renderer>();
            shot.rb = sphere.AddComponent<Rigidbody>();
            shot.rb.mass = cfg.shotMass;
            shot.rb.useGravity = false;
            shot.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            sphere.SetActive(false);
            return shot;
        }

        public void Launch(Vector3 position, Vector3 velocity, float lifetime, int bounces, Material look, float freezePlayerTime = 0f, float freezeBallTime = 0f)
        {
            shown.sharedMaterial = look;
            FreezePlayerTime = freezePlayerTime;
            this.freezeBallTime = freezeBallTime;
            transform.position = position;
            LastTested = position;
            dieAt = Time.time + lifetime;
            bouncesLeft = bounces;
            gameObject.SetActive(true);
            rb.position = position;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (Time.time >= dieAt) gameObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out PoolBall ball))
            {
                ball.MarkHitBy(owner);
                if (FreezePlayerTime > 0f)
                {
                    ShooterMiniRuleSet.Current?.FreezeBall(ball, freezeBallTime);
                    gameObject.SetActive(false);
                    return;
                }
            }
            if (--bouncesLeft < 0) dieAt = Mathf.Min(dieAt, Time.time + LastBounceGrace);
        }
    }
}
