using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Casual 9-ball, 2 players: only balls 1-9 + the cue ball are in play
    // (10-15 are hidden at Setup). The cue ball must hit the lowest-numbered
    // ball still on the table first (real 9-ball's core rule — everything
    // else, call-shot included, stays out of scope). Pocketing that same
    // ball keeps your turn; pocketing anything else (legally), or nothing,
    // passes it. Sinking the 9-ball wins immediately — unless it happens on
    // the same shot as a scratch, which is a loss instead, or on the same
    // shot as a bad first contact, which just doesn't count (the 9 is simply
    // gone from the table without your legit having to sink it, a known
    // simplification — see TODO.md).
    public class NineBallRuleSet : IPoolRuleSet
    {
        private int nextBall = 1;

        public void Setup(PoolMatchRules match)
        {
            foreach (PoolBall ball in new List<PoolBall>(PoolBall.Active))
            {
                if (!ball.IsCueBall && ball.Number > 9)
                    ball.gameObject.SetActive(false);
            }
        }

        public void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
        {
            // Captured before the loop can advance it — same bug class as
            // EightBallRuleSet's group reassignment: pocketing balls that
            // advance nextBall mid-shot must not retroactively change which
            // contact was legal for THIS shot.
            int targetBallAtShotStart = nextBall;
            bool wonNine = false;
            bool pocketedInOrder = false;

            foreach (PoolBall ball in pocketedThisShot)
            {
                if (ball.Number == 9) wonNine = true;
                if (ball.Number == nextBall)
                {
                    nextBall++;
                    pocketedInOrder = true;
                }
            }

            if (wonNine && cueBallPocketed)
            {
                match.Win(1 - match.CurrentPlayer);
                return;
            }

            bool legalContact = firstContact != null && firstContact.Number == targetBallAtShotStart;
            bool foul = !legalContact || cueBallPocketed;

            if (wonNine && !foul)
            {
                match.Win(match.CurrentPlayer);
                return;
            }

            if (foul)
            {
                match.RegisterFoul();
                match.SwitchTurn();
                return;
            }

            if (!pocketedInOrder) match.SwitchTurn();
        }

        public string DescribePlayer(int player) => $"Prochaine bille : {Mathf.Min(nextBall, 9)}";
    }
}
