namespace UntitledPoolGame.Pool
{
    // How the object balls are shared out between the players of the modes
    // where each one has their own (Speed Pool and the modes built on it).
    //   SolidsStripes  two players, or two teams at four (J1 + J3, J2 + J4);
    //   Thirds         three players: 1-5, 6-11 without the 8, 12-15;
    //   TwoRacks       four players each for themselves: a second rack, J3
    //                  the copied solids, J4 the copied stripes.
    // A "side" is who a ball belongs to: a player, or a team in team play
    // (its index is the team's first player, J1 or J2). The 8 is nobody's:
    // it ends the match (SpeedPoolRuleSet).
    public enum GroupSplit { SolidsStripes, Thirds, TwoRacks }

    public static class PlayerBallGroups
    {
        public static GroupSplit Split { get; private set; } = GroupSplit.SolidsStripes;
        public static bool Teams { get; private set; }
        public static int Players { get; private set; } = 2;

        public static int Sides => Split == GroupSplit.Thirds ? 3 : Split == GroupSplit.TwoRacks ? 4 : 2;

        // At the start of every match (PoolMatchRules.StartMatch). Team play
        // only means something at four.
        public static void Configure(int players, bool teams)
        {
            Players = players;
            Teams = players == 4 && teams;
            Split = players == 3 ? GroupSplit.Thirds
                : players == 4 && !teams ? GroupSplit.TwoRacks
                : GroupSplit.SolidsStripes;
        }

        public static int SideOf(int player) => player < 0 ? -1 : Teams ? player % 2 : player;

        // Whose colour a player wears (cue ball, halos): in 2 v 2, the team's
        // (J1's for J1 and J3, J2's for J2 and J4); otherwise their own.
        public static int ColourOf(int player) => Teams ? SideOf(player) : player;

        // The teammate in team play, -1 otherwise.
        public static int TeammateOf(int player) => Teams && player >= 0 ? (player + 2) % 4 : -1;

        // The numbers of each side's balls, at three. Five, five and four:
        // fourteen balls once the 8 is left out.
        private static readonly int[][] ThirdsNumbers =
        {
            new[] { 1, 2, 3, 4, 5 },
            new[] { 6, 7, 9, 10, 11 },
            new[] { 12, 13, 14, 15 },
        };
        private static readonly int[] SolidNumbers = { 1, 2, 3, 4, 5, 6, 7 };
        private static readonly int[] StripeNumbers = { 9, 10, 11, 12, 13, 14, 15 };

        // Side the ball belongs to, -1 for none (a cue ball, the 8).
        public static int OwnerOf(PoolBall ball)
        {
            if (ball == null || ball.IsCueBall) return -1;
            switch (Split)
            {
                case GroupSplit.Thirds:
                    int n = ball.Number;
                    return n >= 1 && n <= 5 ? 0 : n >= 6 && n <= 11 && n != 8 ? 1 : n >= 12 && n <= 15 ? 2 : -1;
                case GroupSplit.TwoRacks:
                    int half = PairOwner(ball);
                    return half < 0 ? -1 : ball.InSecondRack ? half + 2 : half;
                default:
                    return PairOwner(ball);
            }
        }

        // The player is after this ball (their side's, their teammate's too).
        public static bool Owns(int player, PoolBall ball)
        {
            int owner = OwnerOf(ball);
            return owner >= 0 && owner == SideOf(player);
        }

        // The numbers on a side's balls (the HUD's plate). Not to be changed.
        public static int[] Numbers(int side) =>
            Split == GroupSplit.Thirds ? ThirdsNumbers[UnityEngine.Mathf.Clamp(side, 0, 2)]
            : side % 2 == 0 ? SolidNumbers : StripeNumbers;

        // Solid or Stripe, Cue when a side mixes both (Thirds).
        public static BallGroup GroupOfSide(int side) =>
            side < 0 || Split == GroupSplit.Thirds ? BallGroup.Cue : side % 2 == 0 ? BallGroup.Solid : BallGroup.Stripe;

        private static int PairOwner(PoolBall ball) =>
            ball.Group == BallGroup.Solid ? 0 : ball.Group == BallGroup.Stripe ? 1 : -1;
    }
}
