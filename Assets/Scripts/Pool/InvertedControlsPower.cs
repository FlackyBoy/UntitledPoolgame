using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Attack power: inverts the opponent's look controls for the entirety
    // of their next turn — meant to be activated right before their turn to
    // throw off their aim. Same turn-bound lifecycle as VisionImpairPower
    // (starts the moment they enter aim mode, ends when they leave it or
    // their turn ends) — see PoolMatchRules.QueueInvertedControls. Look
    // only, not movement: inverting WASD as well would risk the affected
    // player wandering off the table/into hazards rather than just fumbling
    // their aim, which felt like a step too far for a first pass.
    [CreateAssetMenu(fileName = "InvertedControlsPower", menuName = "Pool/Powers/Inverted Controls (Attack)")]
    public class InvertedControlsPower : PoolPower
    {
        public override PowerType Type => PowerType.Attack;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            int opponent = 1 - activatingPlayer;
            match.QueueInvertedControls(opponent);
        }
    }
}
