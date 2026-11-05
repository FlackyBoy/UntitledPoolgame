using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Aimant à billes": planted where the crosshair is, it draws the table's
    // balls in for a while: a wall to hide behind, or one's balls next to a
    // pocket.
    [CreateAssetMenu(fileName = "ShooterMagnetPower", menuName = "Pool/Powers/Shooter/Magnet")]
    public class ShooterMagnetPower : ShooterPower
    {
        [Tooltip("Durée, en secondes.")]
        [Min(0.1f)] [SerializeField] private float duration = 4f;
        [Tooltip("Durée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.1f)] [SerializeField] private float level2Duration = 7f;
        [Tooltip("Distance à laquelle il attire les billes, en mètres.")]
        [Min(0.01f)] [SerializeField] private float radius = 0.7f;
        [Tooltip("Accélération des billes vers lui, en m/s².")]
        [Min(0f)] [SerializeField] private float acceleration = 1.5f;
        [Tooltip("Portée de la visée, en mètres : au-delà, il se pose à cette distance.")]
        [Min(0.01f)] [SerializeField] private float range = 2f;
        [Tooltip("Effet en boucle tant qu'il est actif. Vide = aucun.")]
        [SerializeField] private GameObject vfxPrefab;
        [Tooltip("Échelle de l'effet par rapport à son prefab.")]
        [Min(0.001f)] [SerializeField] private float vfxScale = 0.144f;

        public override PowerType Type => PowerType.Effect;

        protected override void Apply(ShooterMiniRuleSet rules, int player, bool level2)
        {
            rules.PlantMagnet(player, level2 ? level2Duration : duration, radius, acceleration, range, vfxPrefab, vfxScale);
        }
    }
}
