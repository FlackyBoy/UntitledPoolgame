using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Pocket Size, giant: a second cue ball for a while, next to the first.
    [CreateAssetMenu(fileName = "DoubleCueBallPower", menuName = "Pool/Powers/Pocket Size/Double Cue Ball")]
    public class DoubleCueBallPower : PocketSizePower
    {
        [Tooltip("Durée, en secondes. À la fin, la deuxième blanche disparaît.")]
        [SerializeField, Min(0.5f)] private float duration = 15f;

        public override PocketSizeRole Role => PocketSizeRole.Giant;
        public override PowerType Type => PowerType.Effect;

        protected override void Apply(PocketSizeRuleSet rules, int player) => rules.AddSecondCueBall(duration);
    }
}
