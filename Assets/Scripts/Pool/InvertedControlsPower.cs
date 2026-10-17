using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Attack: inverts the opponent's look for their next turn (look only;
    // inverting movement could walk them off somewhere). Same lifecycle as
    // Vision Impair. Sensitivity is also boosted and the trajectory preview
    // hidden, or it was too easy to compensate.
    [CreateAssetMenu(fileName = "InvertedControlsPower", menuName = "Pool/Powers/Inverted Controls (Attack)")]
    public class InvertedControlsPower : PoolPower
    {
        [SerializeField] private float sensitivityMultiplier = 1.8f;

        public override PowerType Type => PowerType.Attack;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            int opponent = 1 - activatingPlayer;
            match.QueueInvertedControls(opponent, sensitivityMultiplier);
        }
    }
}
