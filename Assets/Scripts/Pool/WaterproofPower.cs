using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Imperméable" (Power Wash Pool): for a while, nothing takes the
    // player's paint: not the opponent's trails or splashes, not the mud
    // (Seau de boue, the 8). Level 2: longer. Does nothing outside Power
    // Wash Pool.
    [CreateAssetMenu(fileName = "WaterproofPower", menuName = "Pool/Powers/Power Wash/Waterproof (Defense)")]
    public class WaterproofPower : PoolPower
    {
        [Tooltip("Durée (secondes de jeu, arrêtées par la pause) au niveau 1.")]
        [Min(1f)] [SerializeField] private float duration = 12f;
        [Tooltip("Durée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(1f)] [SerializeField] private float level2Duration = 20f;

        public override PowerType Type => PowerType.Defense;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (dirt == null) { Debug.Log($"[PowerWash] {PowerName} : sans effet hors Power Wash Pool.", this); return; }
            dirt.Waterproof(activatingPlayer, match.ActivatingPowerLevel >= 2 ? level2Duration : duration);
        }
    }
}
