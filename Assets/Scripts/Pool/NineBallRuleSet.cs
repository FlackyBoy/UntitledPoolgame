using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Casual 9-ball for 2 players: balls 1-9 only. The cue ball must hit the
    // lowest ball first. Potting it keeps the turn, anything else passes it.
    // The 9 wins, except with a scratch (loss) or a bad first contact
    // (doesn't count, a known simplification).
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
