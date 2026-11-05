using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Grappin": a line to whatever the crosshair is on, a ball, a cushion, the
    // other player, and the shooter is pulled there.
    [CreateAssetMenu(fileName = "ShooterGrapplePower", menuName = "Pool/Powers/Shooter/Grapple")]
    public class ShooterGrapplePower : ShooterPower
    {
        [Tooltip("Portée, en mètres.")]
        [Min(0.01f)] [SerializeField] private float range = 2f;
        [Tooltip("Portée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.01f)] [SerializeField] private float level2Range = 3f;
        [Tooltip("Vitesse à laquelle le tireur est tiré, en m/s.")]
        [Min(0.01f)] [SerializeField] private float pullSpeed = 1.6f;
        [Tooltip("Durée maximale de la traction, en secondes.")]
        [Min(0.01f)] [SerializeField] private float maxTime = 1.5f;
        [Tooltip("Dégâts à l'adversaire quand on arrive sur lui.")]
        [Min(0f)] [SerializeField] private float opponentDamage = 30f;
        [Tooltip("Vitesse à laquelle l'adversaire est projeté quand on arrive sur lui, en m/s : il tombe en ragdoll.")]
        [Min(0.01f)] [SerializeField] private float opponentKnockSpeed = 0.4f;
        [Tooltip("Vitesse donnée à une bille quand on arrive dessus, en m/s, où le joueur regarde, comme un coup de pied (Spartan kick : 1,6).")]
        [Min(0f)] [SerializeField] private float ballHitSpeed = 1.6f;

        public override PowerType Type => PowerType.Effect;

        // Nothing in range: the line would catch nothing.
        public override bool ReadyToFire(ShooterMiniRuleSet rules, int player, bool level2)
        {
            LocalShooterWeapon weapon = rules.WeaponOf(player);
            return weapon != null && weapon.HasGrappleTarget(level2 ? level2Range : range);
        }

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.WeaponOf(player)?.FireGrapple(level2 ? level2Range : range, pullSpeed, maxTime, opponentDamage, opponentKnockSpeed, ballHitSpeed);
        }
    }
}
