using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Tête chercheuse": a slow ball that follows the other player. The table's
    // balls stand in its way, which is what they're for.
    [CreateAssetMenu(fileName = "ShooterHomingBallPower", menuName = "Pool/Powers/Shooter/Homing Ball")]
    public class ShooterHomingBallPower : ShooterPower
    {
        [Tooltip("Rayon de la bille, en mètres.")]
        [Min(0.01f)] [SerializeField] private float radius = 0.04f;
        [Tooltip("Masse, en kg.")]
        [Min(0.01f)] [SerializeField] private float mass = 1.5f;
        [Tooltip("Vitesse, en m/s.")]
        [Min(0.01f)] [SerializeField] private float speed = 0.3f;
        [Tooltip("Vitesse au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.01f)] [SerializeField] private float level2Speed = 0.42f;
        [Tooltip("À quel point elle corrige sa route, par seconde. Plus haut = elle tourne plus sec.")]
        [Min(0f)] [SerializeField] private float steering = 3f;
        [Tooltip("Dégâts à un joueur touché.")]
        [Min(0f)] [SerializeField] private float damage = 40f;
        [Tooltip("Durée avant de disparaître, en secondes.")]
        [Min(0.1f)] [SerializeField] private float lifetime = 10f;
        [SerializeField] private Color color = new Color(0.95f, 0.25f, 0.15f);

        public override PowerType Type => PowerType.Attack;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.LaunchRoller(player, new ShooterRollingBall.Spec
            {
                radius = radius,
                mass = mass,
                speed = level2 ? level2Speed : speed,
                damage = damage,
                lifetime = lifetime,
                steering = steering,
                color = color,
                spentOnHit = true,
                label = PowerName,
            }, true);
        }
    }
}
