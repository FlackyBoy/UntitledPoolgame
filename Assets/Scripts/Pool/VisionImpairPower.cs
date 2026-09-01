using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Attack power: blurs/whites-out the opponent's screen and cuts their
    // look sensitivity for the entirety of their next turn — meant to be
    // activated right before their turn to throw off their aim.
    [CreateAssetMenu(fileName = "VisionImpairPower", menuName = "Pool/Powers/Vision Impair (Attack)")]
    public class VisionImpairPower : PoolPower
    {
        [SerializeField, Range(0f, 1f)] private float sensitivityMultiplier = 0.3f;

        public override PowerType Type => PowerType.Attack;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            int opponent = 1 - activatingPlayer;
            // Queued, not applied immediately — see PoolMatchRules.
            // QueueVisionImpair for why: the effect only starts once the
            // opponent actually enters aim mode on their own turn, and lasts
            // until PoolMatchRules.SwitchTurn() clears it at the end of that
            // same turn.
            match.QueueVisionImpair(opponent, sensitivityMultiplier);
        }
    }
}
