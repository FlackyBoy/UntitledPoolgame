using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Grosse bille": a big ball rolls off where the player is looking. It
    // knocks down whoever it reaches and scatters the table's balls like a
    // bowling ball. Slow enough to dodge, or to hide from.
    [CreateAssetMenu(fileName = "ShooterBigBallPower", menuName = "Pool/Powers/Shooter/Big Ball")]
    public class ShooterBigBallPower : ShooterPower
    {
        [Tooltip("Rayon de la bille, en mètres (une bille de la table : 0,09 m).")]
        [Min(0.01f)] [SerializeField] private float radius = 0.12f;
        [Tooltip("Rayon au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.01f)] [SerializeField] private float level2Radius = 0.16f;
        [Tooltip("Masse, en kg. Plus haut = elle pousse plus les billes de la table.")]
        [Min(0.01f)] [SerializeField] private float mass = 6f;
        [Tooltip("Vitesse de départ, en m/s.")]
        [Min(0.01f)] [SerializeField] private float speed = 1.3f;
        [Tooltip("Dégâts à un joueur touché.")]
        [Min(0f)] [SerializeField] private float damage = 40f;
        [Tooltip("Durée avant de disparaître, en secondes.")]
        [Min(0.1f)] [SerializeField] private float lifetime = 8f;
        [SerializeField] private Color color = new Color(0.45f, 0.15f, 0.75f);

        public override PowerType Type => PowerType.Attack;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.LaunchRoller(player, new ShooterRollingBall.Spec
            {
                radius = level2 ? level2Radius : radius,
                mass = mass,
                speed = speed,
                damage = damage,
                lifetime = lifetime,
                color = color,
                label = PowerName,
            }, false);
        }
    }
}
