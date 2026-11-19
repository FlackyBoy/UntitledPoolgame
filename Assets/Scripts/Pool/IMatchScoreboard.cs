namespace UntitledPoolGame.Pool
{
    // Where a match played in rounds is at, for the screen of nobody (the
    // free quarter at three players, MatchHud.FreeQuarter).
    public enum ScoreboardMoment { Play, Countdown, RoundEnd, Over }

    // A mode with points and rounds, read by the free quarter's totals and
    // its panel between rounds. Implemented by the modes that go past two
    // players (Sumo Pool for now).
    public interface IMatchScoreboard
    {
        // Players with a score (0 to Count - 1).
        int ScoreboardPlayers { get; }
        int Points(int player);
        int PointsToWin { get; }
        // Out of the round under way (fallen, eliminated).
        bool IsOut(int player);
        ScoreboardMoment Moment { get; }
        // Whole seconds left while Moment is Countdown.
        int CountdownLeft { get; }
        // Who scored last, -1 if nobody yet.
        int LastScorer { get; }
    }
}
