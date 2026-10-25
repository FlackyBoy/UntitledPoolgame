using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Casual 8-ball for 2 players. Group balls aren't called: potting one of
    // yours keeps the turn; potting only the opponent's is a foul. The 8 has
    // to be called. Also used by Party's Classic sub-mode, the powers come
    // on top.
    public class EightBallRuleSet : IPoolRuleSet
    {
        // Cue means "not assigned yet".
        private readonly BallGroup[] playerGroup = { BallGroup.Cue, BallGroup.Cue };

        public void Setup(PoolMatchRules match) { }

        public void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
        {
            int player = match.CurrentPlayer;
            // Taken before the loop can assign the group: the first contact is
            // judged against the groups as they were before the shot (potting
            // both groups on the break flagged a legal break as a foul).
            BallGroup groupAtShotStart = playerGroup[player];
            bool pocketedOwnBall = false;
            bool pocketedEightBall = false;
            // A ball of the other group: fine along with one of yours, a foul
            // on its own.
            bool pocketedOpponentBall = false;
            PoolBall eightBall = null;
            BallGroup myGroup = groupAtShotStart;

            // Consumed on every shot, so a call never carries over.
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
                else
                {
                    pocketedOpponentBall = true;
                }
            }

            bool groupCleared = myGroup != BallGroup.Cue && IsGroupCleared(myGroup);
            // Hit your own group first (anything before groups are set), or the
            // 8 once your group is cleared.
            bool legalContact = firstContact != null && (groupAtShotStart == BallGroup.Cue
                || firstContact.Group == groupAtShotStart
                || (groupCleared && firstContact.Group == BallGroup.Eight));
            bool onlyOpponentBall = pocketedOpponentBall && !pocketedOwnBall;
            bool foul = !legalContact || cueBallPocketed || onlyOpponentBall;

            if (match.TraceShots)
                Debug.Log($"[ShotTrace] 8-ball, J{player + 1} ({Describe(groupAtShotStart)}) : 1re bille touchée {(firstContact != null ? $"la {firstContact.Number} ({Describe(firstContact.Group)})" : "AUCUNE (contact non détecté)")}, " +
                          $"rentrées [{DescribeBalls(pocketedThisShot)}]{(cueBallPocketed ? ", blanche rentrée" : "")} → " +
                          (pocketedEightBall ? "bille 8 rentrée (fin de partie)"
                           : foul ? $"FAUTE : {(cueBallPocketed ? "blanche rentrée" : firstContact == null ? "la blanche n'a touché aucune bille" : !legalContact ? "1re bille touchée hors de son groupe" : "seule une bille de l'adversaire est rentrée")}"
                           : pocketedOwnBall ? "pas de faute, il garde la main" : "pas de faute, au tour de l'adversaire"));

            if (pocketedEightBall)
            {
                // The 8 must go in the called pocket; no call counts as a wrong one.
                // A living hole (Catch the Hole) can't be called: it counts as is.
                bool wonOnPocket = (calledPocket != null && match.GetPocketFor(eightBall) == calledPocket)
                    || match.WasCaughtByLivingHole(eightBall);
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

        private static string Describe(BallGroup group) => group switch
        {
            BallGroup.Solid => "pleines",
            BallGroup.Stripe => "rayées",
            BallGroup.Eight => "la 8",
            _ => "groupe pas décidé",
        };

        private static string DescribeBalls(IReadOnlyList<PoolBall> balls)
        {
            if (balls.Count == 0) return "aucune";
            var names = new string[balls.Count];
            for (int i = 0; i < balls.Count; i++) names[i] = balls[i].Number.ToString();
            return string.Join(", ", names);
        }

        private static bool IsGroupCleared(BallGroup group)
        {
            foreach (PoolBall ball in PoolBall.Active)
                if (ball.Group == group) return false;
            return true;
        }

        // Group assigned and cleared. False while groups are undecided.
        public bool IsShootingForEightBall(int player)
        {
            BallGroup group = playerGroup[player];
            return group != BallGroup.Cue && IsGroupCleared(group);
        }

        // Test (C+W cheat): sets the group directly.
        public void DebugAssignGroup(int player, BallGroup group)
        {
            playerGroup[player] = group;
            playerGroup[1 - player] = group == BallGroup.Solid ? BallGroup.Stripe : BallGroup.Solid;
        }

        // Cue = not decided yet (read by the HUD through PoolMatchRules.GetGroup).
        public BallGroup GroupOf(int player) => playerGroup[player];

        public string DescribePlayer(int player) => playerGroup[player] switch
        {
            BallGroup.Solid => "Pleines (1-7)",
            BallGroup.Stripe => "Rayées (9-15)",
            _ => "Groupe pas encore décidé",
        };
    }
}
