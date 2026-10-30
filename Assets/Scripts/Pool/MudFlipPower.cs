using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Boue retournée" (Power Wash Pool): for a while, the opponent's balls
    // wash the felt in the player's colour (their trails, and the splash when
    // one goes in). Level 2: longer. Does nothing outside Power Wash Pool.
    [CreateAssetMenu(fileName = "MudFlipPower", menuName = "Pool/Powers/Power Wash/Mud Flip (Effect)")]
    public class MudFlipPower : PoolPower
    {
        [Tooltip("Durée (secondes de jeu, arrêtées par la pause) au niveau 1.")]
        [Min(1f)] [SerializeField] private float duration = 10f;
        [Tooltip("Durée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(1f)] [SerializeField] private float level2Duration = 15f;

        public override PowerType Type => PowerType.Effect;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (dirt == null) { Debug.Log($"[PowerWash] {PowerName} : sans effet hors Power Wash Pool.", this); return; }
            dirt.MudFlip(activatingPlayer, match.ActivatingPowerLevel >= 2 ? level2Duration : duration);
        }
    }
}
