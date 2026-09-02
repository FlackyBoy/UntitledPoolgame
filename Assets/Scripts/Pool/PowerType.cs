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
}
