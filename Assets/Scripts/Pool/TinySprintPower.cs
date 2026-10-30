using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Pocket Size, tiny player: runs faster for a while.
    [CreateAssetMenu(fileName = "TinySprintPower", menuName = "Pool/Powers/Pocket Size/Tiny Sprint")]
    public class TinySprintPower : PocketSizePower
    {
        [Tooltip("Durée, en secondes.")]
        [SerializeField, Min(0.5f)] private float duration = 6f;
        [Tooltip("Vitesse du petit multipliée par.")]
        [SerializeField, Min(1f)] private float multiplier = 1.8f;

        public override PocketSizeRole Role => PocketSizeRole.Tiny;
        public override PowerType Type => PowerType.Effect;

        protected override void Apply(PocketSizeRuleSet rules, int player) => rules.BoostTiny(duration, multiplier);
    }
}
