using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Bombe à eau" (Power Wash Pool): the player points at the felt and a
    // water bomb bursts there, a splash in their colour all around the point
    // (the opponent's paint included). Level 2: a bigger one. Does nothing
    // outside Power Wash Pool.
    [CreateAssetMenu(fileName = "WaterBombPower", menuName = "Pool/Powers/Power Wash/Water Bomb (Effect)")]
    public class WaterBombPower : TablePointPower
    {
        [Tooltip("Portée de l'éclaboussure (mètres) autour du point visé, au niveau 1 (celle d'une bille rentrée : Pocket Splash Radius de PowerWashPoolSettings).")]
        [Min(0.05f)] [SerializeField] private float radius = 0.45f;
        [Tooltip("Portée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.05f)] [SerializeField] private float level2Radius = 0.65f;

        public override PowerType Type => PowerType.Effect;

        private float Radius(int level) => level >= 2 ? level2Radius : radius;

        public override float Margin(int level) => 0.03f;
        public override float PreviewRadius(int level) => Radius(level);

        public override void Activate(PoolMatchRules match, int activatingPlayer, Vector3 point)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (dirt == null) { Debug.Log($"[PowerWash] {PowerName} : sans effet hors Power Wash Pool.", this); return; }
            dirt.Splash(point, Radius(match.ActivatingPowerLevel), activatingPlayer, fromPocket: false);
        }
    }
}
