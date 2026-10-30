using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Seau de boue" (Power Wash Pool): the player points at the felt and a
    // bucket of grime lands there, over the opponent's paint only. Level 2:
    // a bigger splash. Does nothing outside Power Wash Pool.
    [CreateAssetMenu(fileName = "MudBucketPower", menuName = "Pool/Powers/Power Wash/Mud Bucket (Attack)")]
    public class MudBucketPower : TablePointPower
    {
        [Tooltip("Portée de la boue (mètres) autour du point visé, au niveau 1. Seul le tapis lavé par l'adversaire est resali.")]
        [Min(0.05f)] [SerializeField] private float radius = 0.35f;
        [Tooltip("Portée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(0.05f)] [SerializeField] private float level2Radius = 0.5f;

        public override PowerType Type => PowerType.Attack;

        private float Radius(int level) => level >= 2 ? level2Radius : radius;

        public override float Margin(int level) => 0.03f;
        public override float PreviewRadius(int level) => Radius(level);

        public override void Activate(PoolMatchRules match, int activatingPlayer, Vector3 point)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (dirt == null) { Debug.Log($"[PowerWash] {PowerName} : sans effet hors Power Wash Pool.", this); return; }
            dirt.MudSplash(point, Radius(match.ActivatingPowerLevel), 1 - activatingPlayer);
        }
    }
}
