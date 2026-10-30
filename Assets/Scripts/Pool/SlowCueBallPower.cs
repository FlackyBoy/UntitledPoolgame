using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Pocket Size, tiny player: the giant's cue balls can't go faster than
    // Max Speed for a while (shots included).
    [CreateAssetMenu(fileName = "SlowCueBallPower", menuName = "Pool/Powers/Pocket Size/Slow Cue Ball")]
    public class SlowCueBallPower : PocketSizePower
    {
        [Tooltip("Durée, en secondes.")]
        [SerializeField, Min(0.5f)] private float duration = 6f;
        [Tooltip("Vitesse maximale des blanches du grand pendant ce temps, en m/s.")]
        [SerializeField, Min(0.05f)] private float maxSpeed = 0.5f;

        public override PocketSizeRole Role => PocketSizeRole.Tiny;
        public override PowerType Type => PowerType.Attack;

        protected override void Apply(PocketSizeRuleSet rules, int player) => rules.SlowCueBalls(duration, maxSpeed);
    }
}
