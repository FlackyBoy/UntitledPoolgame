using System.Collections.Generic;

namespace UntitledPoolGame.Pool
{
    // Casual 14.1 for 2 players: no groups, no re-rack. Every legal pot is a
    // point; first to the target score wins. A pot keeps the turn, a miss or
    // a scratch passes it.
    public class FourteenOneRuleSet : PoolRuleSet
    {
        private readonly int[] score = { 0, 0 };
        private readonly int targetScore;

        public FourteenOneRuleSet(int targetScore)
        {
            this.targetScore = targetScore;
        }

        public override void Setup(PoolMatchRules match) { }

        public override void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
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

        public override string DescribePlayer(int player) => $"Score : {score[player]} / {targetScore}";

        // Read by the HUD through PoolMatchRules.TryGetScore.
        public int ScoreOf(int player) => score[player];
        public int TargetScore => targetScore;

        public override bool TryGetScore(int player, out int score, out int target)
        {
            score = ScoreOf(player);
            target = TargetScore;
            return true;
        }
    }
}
