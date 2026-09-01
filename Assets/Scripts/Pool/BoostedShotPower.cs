using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // First power, built to validate the plumbing end to end: the next shot
    // fired by the activating player gets its impulse multiplied — a
    // straightforward PowerUp with an obvious, easy-to-verify effect (the cue
    // ball flies noticeably faster/further).
    [CreateAssetMenu(fileName = "BoostedShotPower", menuName = "Pool/Powers/Boosted Shot")]
    public class BoostedShotPower : PoolPower
    {
        [SerializeField] private float shotPowerMultiplier = 1.6f;

        public override PowerType Type => PowerType.PowerUp;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            match.SetShotPowerMultiplier(activatingPlayer, shotPowerMultiplier);
        }
    }
}
