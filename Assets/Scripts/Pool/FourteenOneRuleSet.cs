using System.Collections.Generic;

namespace UntitledPoolGame.Pool
{
    // Casual 14.1 (straight pool), 2 players: no group restriction, and no
    // re-rack when the table runs low on balls (deliberately out of scope —
    // see TODO.md). Every legally pocketed ball (any number, not the cue) is
    // worth 1 point for whoever's shooting. First to reach the target score
    // (announced at match start) wins. A legal pot keeps your turn; a miss or
    // a scratch passes it.
    public class FourteenOneRuleSet : IPoolRuleSet
    {
        private readonly int[] score = { 0, 0 };
        private readonly int targetScore;

        public FourteenOneRuleSet(int targetScore)
        {
            this.targetScore = targetScore;
        }

        public void Setup(PoolMatchRules match) { }

        public void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
        {
            int player = match.CurrentPlayer;
            bool foul = firstContact == null || cueBallPocketed;

            if (foul)
            {
                // No credit for balls pocketed on a fouled shot.
                match.RegisterFoul();
                match.SwitchTurn();
                return;
            }

            int pocketedCount = pocketedThisShot.Count;
            score[player] += pocketedCount;

            if (score[player] >= targetScore)
            {
                match.Win(player);
                return;
            }

            if (pocketedCount == 0) match.SwitchTurn();
        }

        public string DescribePlayer(int player) => $"Score : {score[player]} / {targetScore}";
    }
}
