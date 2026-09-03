namespace UntitledPoolGame.Pool
{
    public enum PoolGameMode
    {
        EightBall,
        NineBall,
        FourteenOne,
        // Which PARTY variant is actually played is a separate choice, made
        // on a second screen after picking this — see PoolPartyMode and
        // PoolMatchRules.DrawPartySubmenuGUI/CreatePartyRuleSet. Only one
        // sub-mode exists today (Classic: 8-ball rules + powers).
        Party,
    }
}
