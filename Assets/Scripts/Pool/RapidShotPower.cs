using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Pocket Size, giant: every shot is stronger for a while (the cue ball
    // flies faster).
    [CreateAssetMenu(fileName = "RapidShotPower", menuName = "Pool/Powers/Pocket Size/Rapid Shot")]
    public class RapidShotPower : PocketSizePower
    {
        [Tooltip("Durée, en secondes.")]
        [SerializeField, Min(0.5f)] private float duration = 10f;
        [Tooltip("Force des tirs multipliée par.")]
        [SerializeField, Min(1f)] private float multiplier = 1.8f;

        public override PocketSizeRole Role => PocketSizeRole.Giant;
        public override PowerType Type => PowerType.Effect;

        protected override void Apply(PocketSizeRuleSet rules, int player) => rules.BoostShots(duration, multiplier);
    }
}
