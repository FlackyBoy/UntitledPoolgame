using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Base of a power aimed at a pocket (Magnet, Close Pocket). The power
    // button starts a pocket pick (LocalPoolPowerController), then
    // TryActivatePower gets the pocket.
    public abstract class PocketTargetPower : PoolPower
    {
        // Fallback when activated without a pick (the frozen online
        // PoolPowerController, which only knows TryActivatePower(player)):
        // a random open pocket, ClosePocketPower's original behaviour.
        public sealed override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            PoolPocket pocket = RandomOpenPocket();
            if (pocket != null) Activate(match, activatingPlayer, pocket);
        }

        public abstract void Activate(PoolMatchRules match, int activatingPlayer, PoolPocket target);

        // A closed pocket (ClosePocketPower) can't be picked: nothing can go
        // in it, and closing it again would do nothing.
        public static bool CanTarget(PoolPocket pocket) => pocket != null && !pocket.IsClosed;

        private static readonly List<PoolPocket> candidates = new List<PoolPocket>();

        private static PoolPocket RandomOpenPocket()
        {
            candidates.Clear();
            foreach (PoolPocket pocket in PoolPocket.Active)
                if (CanTarget(pocket)) candidates.Add(pocket);
            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }
    }
}
