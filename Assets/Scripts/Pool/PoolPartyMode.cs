namespace UntitledPoolGame.Pool
{
    // Sub-modes under PoolGameMode.Party, picked from a second screen (see
    // PoolMatchRules.DrawPartySubmenuGUI) rather than PoolGameMode itself —
    // Party is meant to grow into several distinct party variants over time
    // without the top-level mode list growing with it. Add a new value here
    // + a case in PoolMatchRules' party ruleset switch + a button in
    // DrawPartySubmenuGUI when a new one is ready.
    public enum PoolPartyMode
    {
        // 8-ball rules with powers active on top (PoolPowerCrate/PowerBall) —
        // the only party mode so far.
        Classic,
    }
}
