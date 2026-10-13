using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Attack power: closes the pocket the activating player picked (solid
    // blocker, no ball can fall in) for the entirety of their opponent's
    // next turn. Queued rather than applied immediately — the activator can
    // still be mid-turn when they use this, and closing a pocket right away
    // would eat into their OWN remaining shots instead of only hindering the
    // opponent about to receive it — see PoolMatchRules.QueueClosePocket/
    // SwitchTurn for where it actually takes effect.
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
