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
        // Both players shoot at the same time, each with their own cue ball
        // (SpeedPoolRuleSet). Last on purpose: the value is serialized in
        // menu assets and scenes.
        SpeedPool,
        // 8-ball with living holes wandering the felt (CatchTheHole).
        CatchTheHole,
        // The cue ball is a hole that swallows smaller balls and grows
        // (HungryHoleRuleSet).
        HungryHole,
    }
}
