namespace UntitledPoolGame.Pool
{
    // Category of a power, which sets its colour on crates and balls.
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
