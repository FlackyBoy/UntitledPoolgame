namespace UntitledPoolGame.Pool
{
    // Broad category for a power — drives both its color code (crates/ball
    // glow, see PoolPowerSpawnSettings) and, eventually, gameplay decisions
    // like whether it can be activated outside your own turn (see TODO.md).
    // Simplified from an earlier 5-value version (Attack/Trap/PowerUp/
    // Destruction/OpponentImpact) down to 3 — a 5-way split didn't map
    // cleanly onto "one glance, one color" the way this was meant to.
    public enum PowerType
    {
        Attack,
        Defense,
        Effect,
    }

    // Why a press of the power button didn't launch anything — see
    // PoolMatchRules.CheckPowerUse, shown by the HUD (PowerRefused).
    public enum PowerRefusal
    {
        None,
        NoPower,
        OwnTurnOnly,       // RequiresOwnTurn, pressed during the opponent's turn
        OpponentTurnOnly,  // AllowDuringOwnTurn unchecked, pressed during one's own turn
        BallsMoving,       // AllowWhileBallsMoving unchecked, shot in progress
        PointerBusy,       // ball in hand or the 8-ball call holds the pointer
    }
}
