using System.Collections.Generic;

namespace UntitledPoolGame.Pool
{
    // Casual 8-ball, 2 players: no called shots/pockets — any legally
    // pocketed ball from your own group keeps your turn. Also used as-is by
    // Party's "Classic" sub-mode (see PoolMatchRules.CreatePartyRuleSet) —
    // the powers active in that mode come from PoolPowerCrate/PowerBall on
    // top, not from a different ruleset.
    public class EightBallRuleSet : IPoolRuleSet
    {
        // Cue means "not assigned yet".
        private readonly BallGroup[] playerGroup = { BallGroup.Cue, BallGroup.Cue };

        public void Setup(PoolMatchRules match) { }

        public void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
        {
            int player = match.CurrentPlayer;
            // Captured before the loop below can reassign it — legality of the
            // shot's first contact has to be judged against the group as it
            // stood BEFORE this shot (e.g. on the break, nothing is assigned
            // yet, so any first contact is legal), not after. Pocketing balls
            // from both groups in the same shot (very possible on a break)
            // used to reassign the group mid-loop and then judge the contact
            // against that NEW group — flagging a perfectly legal break as a
            // foul depending on which ball happened to be processed first.
            BallGroup groupAtShotStart = playerGroup[player];
            bool pocketedOwnBall = false;
            bool pocketedEightBall = false;
            BallGroup myGroup = groupAtShotStart;

            foreach (PoolBall ball in pocketedThisShot)
            {
                if (ball.Group == BallGroup.Eight)
                {
                    pocketedEightBall = true;
                    continue;
                }

                if (myGroup == BallGroup.Cue)
                {
                    // First group ball pocketed in the match — assign groups now.
                    myGroup = ball.Group;
                    playerGroup[player] = myGroup;
                    playerGroup[1 - player] = myGroup == BallGroup.Solid ? BallGroup.Stripe : BallGroup.Solid;
                    pocketedOwnBall = true;
                }
                else if (ball.Group == myGroup)
                {
                    pocketedOwnBall = true;
                }
            }

            bool groupCleared = myGroup != BallGroup.Cue && IsGroupCleared(myGroup);
            // No call-shot, so the only contact rule left is: hit your own
            // group first (or anything, before groups are assigned), or the
            // 8-ball once your group is fully cleared.
            bool legalContact = firstContact != null && (groupAtShotStart == BallGroup.Cue
                || firstContact.Group == groupAtShotStart
                || (groupCleared && firstContact.Group == BallGroup.Eight));
            bool foul = !legalContact || cueBallPocketed;

            if (pocketedEightBall)
            {
                match.Win(groupCleared && !foul ? player : 1 - player);
                return;
            }

            if (foul)
            {
                match.RegisterFoul();
                match.SwitchTurn();
                return;
            }

            if (!pocketedOwnBall) match.SwitchTurn();
        }

        private static bool IsGroupCleared(BallGroup group)
        {
            foreach (PoolBall ball in PoolBall.Active)
                if (ball.Group == group) return false;
            return true;
        }

        public string DescribePlayer(int player) => playerGroup[player] switch
        {
            BallGroup.Solid => "Pleines (1-7)",
            BallGroup.Stripe => "Rayées (9-15)",
            _ => "Groupe pas encore décidé",
        };
    }
}
