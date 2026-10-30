using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Roomba" (Power Wash Pool): the player points at the felt and a little
    // robot vacuum is dropped there. It roams the table for a while, washing
    // in their colour and pushing the balls it runs into (PowerWashRoomba).
    // Level 2: two of them. Does nothing outside Power Wash Pool.
    [CreateAssetMenu(fileName = "RoombaPower", menuName = "Pool/Powers/Power Wash/Roomba (Effect)")]
    public class RoombaPower : TablePointPower
    {
        [Tooltip("Rayon du robot (mètres). Une bille fait ~3 cm de rayon.")]
        [Min(0.02f)] [SerializeField] private float radius = 0.06f;
        [Tooltip("Vitesse (m/s) quand il roule tout droit.")]
        [Min(0.05f)] [SerializeField] private float speed = 0.35f;
        [Tooltip("Vitesse (degrés/s) à laquelle il tourne sur place quand une bande ou une poche le bloque.")]
        [Min(10f)] [SerializeField] private float turnSpeed = 240f;
        [Tooltip("Largeur de la trace qu'il lave, en part de son diamètre (1 = toute sa largeur).")]
        [Min(0.1f)] [SerializeField] private float washWidth = 1f;
        [Tooltip("Durée (secondes de jeu, arrêtées par la pause).")]
        [Min(1f)] [SerializeField] private float duration = 20f;
        [Tooltip("Nombre de robots au niveau 2 (pouvoir ramassé deux fois), posés côte à côte.")]
        [Min(1)] [SerializeField] private int level2Count = 2;

        public override PowerType Type => PowerType.Effect;

        public override float Margin(int level) => radius + 0.01f;
        public override float PreviewRadius(int level) => radius;

        public override void Activate(PoolMatchRules match, int activatingPlayer, Vector3 point)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (dirt == null) { Debug.Log($"[PowerWash] {PowerName} : sans effet hors Power Wash Pool.", this); return; }
            int count = match.ActivatingPowerLevel >= 2 ? level2Count : 1;
            for (int i = 0; i < count; i++)
            {
                // Side by side across the felt, a robot's width apart.
                Vector3 at = point + Vector3.right * ((i - (count - 1) * 0.5f) * radius * 2.5f);
                PowerWashRoomba.Spawn(dirt, activatingPlayer, at, dirt.PlayerColour(activatingPlayer), radius, speed, turnSpeed, washWidth, duration);
            }
        }
    }
}
