using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Grenade": thrown on a curve towards the crosshair, it bounces around
    // and blows up at the end of its fuse. The blast is the mine's, so it
    // throws whoever is in reach, the thrower too.
    [CreateAssetMenu(fileName = "ShooterGrenadePower", menuName = "Pool/Powers/Shooter/Grenade")]
    public class ShooterGrenadePower : ShooterPower
    {
        [Tooltip("Vitesse du lancer vers le viseur, en m/s.")]
        [Min(0.01f)] [SerializeField] private float throwSpeed = 2.5f;
        [Tooltip("Vitesse ajoutée vers le haut au lancer, en m/s. Plus haut = plus en cloche.")]
        [Min(0f)] [SerializeField] private float throwLift = 1.2f;
        [Tooltip("Rayon de la grenade, en mètres (un tir : 0,006 m).")]
        [Min(0.001f)] [SerializeField] private float radius = 0.018f;
        [Tooltip("Masse, en kg. Plus haut = elle pousse plus les billes en les touchant.")]
        [Min(0.001f)] [SerializeField] private float mass = 0.2f;
        [Tooltip("Rebond (0 = aucun, 1 = sans perte).")]
        [Range(0f, 1f)] [SerializeField] private float bounciness = 0.5f;
        [Tooltip("Temps avant l'explosion, en secondes.")]
        [Min(0.1f)] [SerializeField] private float fuse = 1.5f;
        [Tooltip("Rayon de l'explosion, en mètres.")]
        [Min(0.01f)] [SerializeField] private float blastRadius = 0.25f;
        [Tooltip("Rayon de l'explosion au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.01f)] [SerializeField] private float level2BlastRadius = 0.35f;
        [Tooltip("Dégâts à un joueur dans le rayon, le lanceur compris.")]
        [Min(0f)] [SerializeField] private float damage = 50f;
        [Tooltip("Vitesse à laquelle un joueur est projeté à l'horizontale, en m/s, à bout portant (la moitié au bord du rayon).")]
        [Min(0f)] [SerializeField] private float knockSpeed = 0.7f;
        [Tooltip("Vitesse à laquelle un joueur est projeté vers le haut, en m/s, à bout portant (la moitié au bord du rayon).")]
        [Min(0f)] [SerializeField] private float liftSpeed = 1f;
        [Tooltip("Vitesse donnée aux billes collées à l'explosion, en m/s, diminuant jusqu'à 0 au bord du rayon.")]
        [Min(0f)] [SerializeField] private float ballSpeed = 1.2f;
        [Tooltip("Effet de l'explosion. Vide = aucun.")]
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Échelle de l'effet par rapport à son prefab.")]
        [Min(0.001f)] [SerializeField] private float explosionScale = 0.2f;

        public override PowerType Type => PowerType.Attack;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.ThrowGrenade(player, throwSpeed, throwLift, new ShooterGrenade.Spec
            {
                radius = radius,
                mass = mass,
                fuse = fuse,
                bounciness = bounciness,
                blastRadius = level2 ? level2BlastRadius : blastRadius,
                damage = damage,
                knockSpeed = knockSpeed,
                liftSpeed = liftSpeed,
                ballSpeed = ballSpeed,
                explosionPrefab = explosionPrefab,
                explosionScale = explosionScale,
                label = PowerName,
            });
        }
    }
}
