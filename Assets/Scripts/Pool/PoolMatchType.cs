namespace UntitledPoolGame.Pool
{
    // How the players take their shots, picked apart from the mode: a mode
    // is played either way unless PoolMatchSetup.Supports says otherwise.
    public enum PoolMatchType
    {
        // One shot at a time, the rule set decides whose turn it is.
        TurnBased,
        // Both players at once, each with their own cue ball, no turns: the
        // rule set judges each pot as it happens.
        SpeedPool,
    }
}
