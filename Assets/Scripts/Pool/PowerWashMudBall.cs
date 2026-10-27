using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Power Wash Pool, on the 8: who touched it last (a player's cue ball or
    // one of their balls), who gets the splash when it goes in. Added by
    // PowerWashPoolRuleSet.Setup, cleared each match.
    public class PowerWashMudBall : MonoBehaviour
    {
        public int LastToucher { get; private set; } = -1;

        public void Clear() => LastToucher = -1;

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.TryGetComponent(out PoolBall other)) return;
            int player = other.IsCueBall ? other.CueOwner : SpeedPoolRuleSet.OwnerOf(other);
            if (player >= 0) LastToucher = player;
        }
    }
}
