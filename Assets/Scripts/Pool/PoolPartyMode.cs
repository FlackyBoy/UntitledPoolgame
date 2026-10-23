namespace UntitledPoolGame.Pool
{
    // Party sub-modes, picked on a second screen. A new one = a value here,
    // a case in PoolMatchRules.CreatePartyRuleSet and a button.
    public enum PoolPartyMode
    {
        // 8-ball rules with powers active on top (PoolPowerCrate/PowerBall).
        Classic,
        // Speed Pool (SpeedPoolRuleSet) with the crates; no power ball.
        SpeedPool,
    }
}
