using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Gros rouleau" (Power Wash Pool): for a while, the player's balls leave
    // much wider trails. Level 2: wider and longer. Does nothing outside
    // Power Wash Pool.
    [CreateAssetMenu(fileName = "BigRollerPower", menuName = "Pool/Powers/Power Wash/Big Roller (Effect)")]
    public class BigRollerPower : PoolPower
    {
        [Tooltip("Largeur des traces de ses billes, multipliée (niveau 1).")]
        [Min(1f)] [SerializeField] private float widthMultiplier = 2f;
        [Tooltip("Durée (secondes de jeu, arrêtées par la pause) au niveau 1.")]
        [Min(1f)] [SerializeField] private float duration = 15f;
        [Tooltip("Largeur multipliée au niveau 2 (pouvoir ramassé deux fois).")]
        [Min(1f)] [SerializeField] private float level2WidthMultiplier = 3f;
        [Tooltip("Durée au niveau 2.")]
        [Min(1f)] [SerializeField] private float level2Duration = 20f;

        public override PowerType Type => PowerType.Effect;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (dirt == null) { Debug.Log($"[PowerWash] {PowerName} : sans effet hors Power Wash Pool.", this); return; }
            bool level2 = match.ActivatingPowerLevel >= 2;
            dirt.BigRoller(activatingPlayer, level2 ? level2WidthMultiplier : widthMultiplier, level2 ? level2Duration : duration);
        }
    }
}
