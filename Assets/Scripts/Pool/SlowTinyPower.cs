using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Pocket Size, giant: the tiny player is slowed down for a while.
    [CreateAssetMenu(fileName = "SlowTinyPower", menuName = "Pool/Powers/Pocket Size/Slow Tiny")]
    public class SlowTinyPower : PocketSizePower
    {
        [Tooltip("Durée, en secondes.")]
        [SerializeField, Min(0.5f)] private float duration = 6f;
        [Tooltip("Vitesse du petit multipliée par.")]
        [SerializeField, Range(0.1f, 1f)] private float multiplier = 0.5f;

        public override PocketSizeRole Role => PocketSizeRole.Giant;
        public override PowerType Type => PowerType.Attack;

        protected override void Apply(PocketSizeRuleSet rules, int player) => rules.SlowTiny(duration, multiplier);
    }
}
