using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A mine on the felt (ShooterMinePower): waits to be armed, then blows up
    // when the rule set says something touched it, or by itself at the end of
    // its lifetime.
    public class ShooterMine : MonoBehaviour
    {
        private const float Radius = 0.022f;
        private const float Height = 0.004f;
        private const float BlinkPerSecond = 2f;
        private static readonly Color Off = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color On = new Color(1f, 0.1f, 0.05f);

        public struct Spec
        {
            public float armDelay, triggerRadius, blastRadius, damage, knockSpeed, liftSpeed, ballSpeed, lifetime, explosionScale;
            public GameObject explosionPrefab;
            public string label;
        }

        private ShooterMiniRuleSet rules;
        private int owner;
        private Spec spec;
        private float armedAt, dieAt;
        private Material lamp;

        public static ShooterMine Lay(ShooterMiniRuleSet rules, int owner, Spec spec, Vector3 onFelt)
        {
            Material material = ShooterPowerFx.Solid(Off, spec.label);
            GameObject disc = ShooterPowerFx.Shape(PrimitiveType.Cylinder, material, "Shooter " + spec.label);
            disc.transform.position = onFelt + Vector3.up * Height;
            disc.transform.localScale = new Vector3(Radius * 2f, Height, Radius * 2f);
            ShooterMine mine = disc.AddComponent<ShooterMine>();
            mine.rules = rules;
            mine.owner = owner;
            mine.spec = spec;
            mine.armedAt = Time.time + spec.armDelay;
            mine.dieAt = Time.time + spec.lifetime;
            mine.lamp = material;
            return mine;
        }

        private void Update()
        {
            if (Time.time >= dieAt)
            {
                Explode();
                return;
            }
            if (Time.time < armedAt) return;
            lamp.SetColor("_BaseColor", Mathf.Repeat(Time.time * BlinkPerSecond, 1f) < 0.5f ? On : Off);
            if (rules.IsMineTouched(transform.position, spec.triggerRadius)) Explode();
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
