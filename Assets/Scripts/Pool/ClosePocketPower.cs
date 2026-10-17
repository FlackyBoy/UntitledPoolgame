using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Attack: closes the picked pocket for the opponent's whole next turn.
    // Queued so it never costs the launcher a shot
    // (PoolMatchRules.QueueClosePocket).
    [CreateAssetMenu(fileName = "ClosePocketPower", menuName = "Pool/Powers/Close Pocket (Attack)")]
    public class ClosePocketPower : PocketTargetPower
    {
        public override PowerType Type => PowerType.Attack;

        public override void Activate(PoolMatchRules match, int activatingPlayer, PoolPocket target)
        {
            match.QueueClosePocket(activatingPlayer, target);
        }
    }
}
