namespace UntitledPoolGame.Pool
{
    // Broad categories for powers, mostly for UI/description purposes — the
    // actual behavior lives in each PoolPower subclass, not in this enum.
    public enum PowerType
    {
        Attack,
        Trap,
        PowerUp,
        Destruction,
        OpponentImpact,
    }
}
