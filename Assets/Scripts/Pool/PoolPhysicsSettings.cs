using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Shared, hand-tunable friction settings for every ball — one asset instead
    // of a value baked into each of the 16 ball instances, so tuning it doesn't
    // require regenerating the table. Loaded from Resources by PoolBall.
    [CreateAssetMenu(fileName = "PoolPhysicsSettings", menuName = "Pool/Physics Settings")]
    public class PoolPhysicsSettings : ScriptableObject
    {
        [Tooltip("Grip/slide friction (m/s^2) — how fast spin and linear motion pull each " +
                 "other into a matching \"rolling without slipping\" state. This is what " +
                 "converts a cue strike's leftover spin into renewed motion after a stun " +
                 "(follow/draw). Higher = grabs faster.")]
        public float slideFriction = 6f;

        [Tooltip("Constant deceleration (m/s^2) once a ball is genuinely rolling (no more " +
                 "slip) — like real rolling resistance. A ball at speed v comes to rest in " +
                 "exactly v/friction seconds, smoothly, with no long trailing crawl and no " +
                 "abrupt snap.")]
        public float friction = 2f;

        [Tooltip("Tiny safety cutoff (m/s) to avoid floating-point jitter right at zero — " +
                 "not meant to be a noticeable stop point, the friction value above handles that.")]
        public float sleepVelocityThreshold = 0.01f;

        [Tooltip("How far (meters) below its own resting/felt height a ball has to fall before " +
                 "it's treated as having gone off the table entirely (jumped a rail on a hard " +
                 "shot, etc.) — resolved as a foul, same as scratching, instead of falling " +
                 "through the floor forever. Comfortably bigger than a normal rail-bounce hop.")]
        public float offTableDropThreshold = 0.15f;
    }
}
