using System.Collections.Generic;

namespace UntitledPoolGame.Pool
{
    // Casual 8-ball, 2 players: no called shots/pockets for the group balls —
    // any legally pocketed ball from your own group keeps your turn. The 8
    // itself IS called (see IsShootingForEightBall/PoolMatchRules'
    // call-shot prompt) — real-world call-shot is almost always enforced at
    // least for the money ball, even in otherwise casual rule sets. Also
    // used as-is by Party's "Classic" sub-mode (see
    // PoolMatchRules.CreatePartyRuleSet) — the powers active in that mode
    // come from PoolPowerCrate/PowerBall on top, not from a different ruleset.
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
            PoolBall eightBall = null;
            BallGroup myGroup = groupAtShotStart;

            // Consumed here regardless of what this shot actually pocketed —
            // a call made ahead of some other shot (not an 8-ball attempt) is
            // simply discarded rather than lingering into a later attempt.
            PoolPocket calledPocket = match.ConsumeCalledEightBallPocket();

            foreach (PoolBall ball in pocketedThisShot)
            {
                if (ball.Group == BallGroup.Eight)
                {
                    pocketedEightBall = true;
                    eightBall = ball;
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
                // Call-shot, but only for the 8 (the rest of the game stays
                // uncalled — see TODO.md): the 8 has to go in the pocket the
                // player actually declared before shooting, or it doesn't
                // count as a win — same severity as the other 8-ball mistakes
                // already enforced above (wrong group first, scratch on the
                // 8). No call at all is treated the same as a wrong one.
                bool wonOnPocket = calledPocket != null && match.GetPocketFor(eightBall) == calledPocket;
                match.Win(groupCleared && !foul && wonOnPocket ? player : 1 - player);
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

        // Whether player is currently eligible to shoot at the 8 (group
        // assigned and fully cleared) — drives PoolMatchRules' call-shot
        // prompt. False before groups are assigned (playerGroup starts at
        // BallGroup.Cue, which IsGroupCleared would otherwise check against
        // the actual cue ball and never return true for anyway, but this
        // reads clearer than relying on that coincidence).
        public bool IsShootingForEightBall(int player)
        {
            BallGroup group = playerGroup[player];
            return group != BallGroup.Cue && IsGroupCleared(group);
        }

        // Test-only: assigns player's group directly rather than waiting for
        // it to happen naturally off a pocketed ball — see
        // PoolMatchRules.DebugForceEightBallEndgame (the C+W cheat), which
        // pairs this with deactivating every Solid/Stripe ball so
        // IsGroupCleared(group) is immediately true too.
        public void DebugAssignGroup(int player, BallGroup group)
        {
            playerGroup[player] = group;
            playerGroup[1 - player] = group == BallGroup.Solid ? BallGroup.Stripe : BallGroup.Solid;
        }

        public string DescribePlayer(int player) => playerGroup[player] switch
        {
            BallGroup.Solid => "Pleines (1-7)",
            BallGroup.Stripe => "Rayées (9-15)",
            _ => "Groupe pas encore décidé",
        };
    }
}
