using System.Collections.Generic;

namespace UntitledPoolGame.Pool
{
    // One implementation per PoolGameMode. PoolMatchRules hosts the shared
    // shot-detection loop (wait for balls to stop, then resolve) and the
    // mode-select screen; everything mode-specific (win condition, turn
    // continuation, groups/order/score) lives here instead — adding a new
    // mode means adding a new IPoolRuleSet, not touching PoolMatchRules.
    public interface IPoolRuleSet
    {
        // Called once when the match starts, after the table is already
        // racked. Use it to strip balls that don't belong in this mode, etc.
        void Setup(PoolMatchRules match);

        // Called once all balls have come to rest after a shot. firstContact is
        // the ball the cue ball first touched during the shot (null if it
        // touched nothing at all) — implementations use it to decide whether
        // the shot was a foul (wrong ball hit first / no contact), on top of
        // the usual pocketed-balls/scratch checks. Call match.RegisterFoul()
        // on any foul so the fouled-against player gets ball-in-hand.
        void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact);

        // One status line per player for the on-screen HUD (group, next ball,
        // score — whatever's relevant to this mode).
        string DescribePlayer(int player);
    }
}
