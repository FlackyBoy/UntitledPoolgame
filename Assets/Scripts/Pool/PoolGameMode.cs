namespace UntitledPoolGame.Pool
{
    public enum PoolGameMode
    {
        EightBall,
        NineBall,
        FourteenOne,
        // Powers/special balls aren't designed yet (see TODO.md backlog) —
        // Party currently just plays 8-ball rules, it exists so the mode-select
        // screen already has a slot reserved for it.
        Party,
    }
}
