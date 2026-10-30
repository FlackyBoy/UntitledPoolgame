namespace UntitledPoolGame.Pool
{
    // The rules of a match. Whether it's played in turns or in Speed Pool,
    // and with or without powers, is chosen apart (PoolMatchSetup). The
    // values are serialized in menu assets and scenes: never reorder, add
    // new ones last.
    public enum PoolGameMode
    {
        // 8-ball (EightBallRuleSet; SpeedPoolRuleSet in Speed Pool).
        EightBall,
        NineBall,
        FourteenOne,
        // Former mode, now 8-ball with the powers on (PoolMatchSetup.Normalized).
        Party,
        // Former mode, now 8-ball in Speed Pool (PoolMatchSetup.Normalized).
        SpeedPool,
        // 8-ball with living holes wandering the felt (CatchTheHole).
        CatchTheHole,
        // The cue ball is a hole that swallows smaller balls and grows
        // (HungryHoleRuleSet).
        HungryHole,
        // Balls collected join a chain trailing behind your cue ball, the
        // opponent cuts it (SnakePoolRuleSet).
        SnakePool,
        // The felt is caked in grime, your balls wash it as they roll
        // (PowerWashPoolRuleSet).
        PowerWashPool,
        // Golf in the décor: each player sinks their own cue ball in a hole
        // set somewhere in the room, in as few strokes as possible
        // (PlayAnywhereRuleSet, PlayAnywhereHole).
        PlayAnywhere,
        // One player shrunk on the felt, the other hunts them with the cue
        // ball; roles swap after the first round (PocketSizeRuleSet).
        PocketSize,
    }
}
