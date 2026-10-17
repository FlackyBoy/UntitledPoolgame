using System.Collections.Generic;

namespace UntitledPoolGame.Pool
{
    // The rules of one PoolGameMode (win condition, turns, groups, order,
    // score). PoolMatchRules runs the shot loop and calls these.
    public interface IPoolRuleSet
    {
        // Called once when the match starts, after the table is already
        // racked. Use it to strip balls that don't belong in this mode, etc.
        void Setup(PoolMatchRules match);

        // Called once every ball has stopped. firstContact: the first ball the
        // cue ball touched (null = none), for the contact fouls. Call
        // match.RegisterFoul on a foul.
        void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact);

        // One status line per player for the on-screen HUD (group, next ball,
        // score — whatever's relevant to this mode).
        string DescribePlayer(int player);
    }
}
