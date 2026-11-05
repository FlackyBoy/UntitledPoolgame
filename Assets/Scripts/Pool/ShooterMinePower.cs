using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Mine": laid at the player's feet. Set off by a player, whoever laid it
    // once it's armed, or by a ball rolling over it: a blast that throws both.
    [CreateAssetMenu(fileName = "ShooterMinePower", menuName = "Pool/Powers/Shooter/Mine")]
    public class ShooterMinePower : ShooterPower
    {
        [Tooltip("Délai avant qu'elle soit active, en secondes : le temps de partir.")]
        [Min(0f)] [SerializeField] private float armDelay = 0.8f;
        [Tooltip("Distance à laquelle elle se déclenche, en mètres.")]
        [Min(0.001f)] [SerializeField] private float triggerRadius = 0.05f;
        [Tooltip("Rayon de l'explosion, en mètres.")]
        [Min(0.01f)] [SerializeField] private float blastRadius = 0.2f;
        [Tooltip("Rayon de l'explosion au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.01f)] [SerializeField] private float level2BlastRadius = 0.3f;
        [Tooltip("Dégâts à un joueur dans le rayon.")]
        [Min(0f)] [SerializeField] private float damage = 50f;
        [Tooltip("Vitesse à laquelle un joueur est projeté à l'horizontale, en m/s, à bout portant (la moitié au bord du rayon).")]
        [Min(0f)] [SerializeField] private float knockSpeed = 0.7f;
        [Tooltip("Vitesse à laquelle un joueur est projeté vers le haut, en m/s, à bout portant (la moitié au bord du rayon). C'est elle qui le fait voler : à 1 m/s, environ 25 cm de haut.")]
        [Min(0f)] [SerializeField] private float liftSpeed = 1f;
        [Tooltip("Vitesse donnée aux billes collées à l'explosion, en m/s, diminuant jusqu'à 0 au bord du rayon.")]
        [Min(0f)] [SerializeField] private float ballSpeed = 1.2f;
        [Tooltip("Durée avant qu'une mine jamais déclenchée explose toute seule, en secondes.")]
        [Min(1f)] [SerializeField] private float lifetime = 15f;
        [Tooltip("Effet de l'explosion. Vide = aucun.")]
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Échelle de l'effet par rapport à son prefab.")]
        [Min(0.001f)] [SerializeField] private float explosionScale = 0.2f;

        public override PowerType Type => PowerType.Effect;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.LayMine(player, new ShooterMine.Spec
            {
                armDelay = armDelay,
                triggerRadius = triggerRadius,
                blastRadius = level2 ? level2BlastRadius : blastRadius,
                damage = damage,
                knockSpeed = knockSpeed,
                liftSpeed = liftSpeed,
                ballSpeed = ballSpeed,
                lifetime = lifetime,
                explosionPrefab = explosionPrefab,
                explosionScale = explosionScale,
                label = PowerName,
            });
        }
    }
}
