using UnityEngine;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // "Bagarre": the holder may hit for a while, in a mode where nobody can
    // (CombatSettings, Melee Modes). Pointless where everyone already can, so
    // it isn't dealt there.
    [CreateAssetMenu(fileName = "BrawlPower", menuName = "Pool/Powers/Brawl")]
    public class BrawlPower : PoolPower
    {
        [Tooltip("Durée pendant laquelle le joueur peut frapper, en secondes.")]
        [Min(0.5f)] [SerializeField] private float duration = 20f;
        [Tooltip("Durée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.5f)] [SerializeField] private float level2Duration = 40f;

        public override PowerType Type => PowerType.Effect;

        public override bool AvailableIn(PoolGameMode mode) =>
            base.AvailableIn(mode) && !CombatSettings.Instance.AllowsMelee(mode);

        public override void Activate(PoolMatchRules match, int activatingPlayer) =>
            match.AllowMelee(activatingPlayer, match.ActivatingPowerLevel >= 2 ? level2Duration : duration);
    }
}
