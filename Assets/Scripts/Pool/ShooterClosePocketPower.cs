using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Poche fermée": the open pocket nearest the crosshair closes for a while.
    // In Shooter Billard, what keeps the other player from finishing; in the
    // Shooter, a pocket nobody can fall into.
    [CreateAssetMenu(fileName = "ShooterClosePocketPower", menuName = "Pool/Powers/Shooter/Close Pocket")]
    public class ShooterClosePocketPower : ShooterPower
    {
        [Tooltip("Durée, en secondes.")]
        [Min(0.1f)] [SerializeField] private float duration = 10f;
        [Tooltip("Durée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.1f)] [SerializeField] private float level2Duration = 18f;

        public override PowerType Type => PowerType.Attack;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.ClosePocketInSight(player, level2 ? level2Duration : duration);
        }
    }
}
