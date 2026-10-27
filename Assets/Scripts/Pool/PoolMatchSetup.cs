namespace UntitledPoolGame.Pool
{
    // What a match is made of, three independent choices: the type (turn
    // based or Speed Pool), the mode (the rules) and the powers (on/off).
    // Built by the menus, carried by GameSession, started by
    // PoolMatchRules.RequestStart.
    [System.Serializable]
    public struct PoolMatchSetup
    {
        public PoolGameMode mode;
        public PoolMatchType type;
        public bool powers;
        // 14.1 only.
        public int targetScore;

        public PoolMatchSetup(PoolGameMode mode, PoolMatchType type, bool powers, int targetScore)
        {
            this.mode = mode;
            this.type = type;
            this.powers = powers;
            this.targetScore = targetScore;
        }

        // Party and SpeedPool were modes before the type and the powers were
        // picked apart: still serialized in scenes and menus, read as what
        // they meant. A type the mode can't be played in falls back to turns.
        public PoolMatchSetup Normalized()
        {
            PoolMatchSetup s = this;
            if (s.mode == PoolGameMode.Party)
            {
                s.mode = PoolGameMode.EightBall;
                s.powers = true;
            }
            else if (s.mode == PoolGameMode.SpeedPool)
            {
                s.mode = PoolGameMode.EightBall;
                s.type = PoolMatchType.SpeedPool;
            }
            if (!Supports(s.mode, s.type)) s.type = OtherType(s.type);
            // Neither ticked in PoolModeSettings: turns, rather than no match.
            if (!Supports(s.mode, s.type)) s.type = PoolMatchType.TurnBased;
            if (s.targetScore <= 0) s.targetScore = 150;
            return s;
        }

        // Ticked for the mode in PoolModeSettings, and the mode has rules for
        // it: 9-ball and 14.1 have no Speed Pool rules yet (who gets a pot
        // when both cue balls roll is left for later).
        public static bool Supports(PoolGameMode mode, PoolMatchType type)
        {
            mode = Current(mode);
            if (type == PoolMatchType.SpeedPool && (mode == PoolGameMode.NineBall || mode == PoolGameMode.FourteenOne)) return false;
            PoolModeSettings.ModeEntry entry = PoolModeSettings.Instance.Find(mode);
            return entry == null ? type == PoolMatchType.TurnBased : entry.Allows(type);
        }

        // The type a mode starts on in the menus (PoolModeSettings' Default
        // Type, or the other one if that one isn't allowed).
        public static PoolMatchType NativeType(PoolGameMode mode)
        {
            // The former Speed Pool mode was 8-ball at once.
            if (mode == PoolGameMode.SpeedPool && Supports(mode, PoolMatchType.SpeedPool)) return PoolMatchType.SpeedPool;
            PoolModeSettings.ModeEntry entry = PoolModeSettings.Instance.Find(Current(mode));
            PoolMatchType type = entry != null ? entry.defaultType : PoolMatchType.TurnBased;
            return Supports(mode, type) ? type : OtherType(type);
        }

        public static PoolMatchType OtherType(PoolMatchType type) =>
            type == PoolMatchType.SpeedPool ? PoolMatchType.TurnBased : PoolMatchType.SpeedPool;

        // The former Party and SpeedPool modes are 8-ball.
        private static PoolGameMode Current(PoolGameMode mode) =>
            mode == PoolGameMode.Party || mode == PoolGameMode.SpeedPool ? PoolGameMode.EightBall : mode;

        public static string ModeName(PoolGameMode mode) => mode switch
        {
            PoolGameMode.NineBall => "9-ball",
            PoolGameMode.FourteenOne => "14.1",
            PoolGameMode.Party => "Pouvoirs",
            PoolGameMode.SpeedPool => "Speed Pool",
            PoolGameMode.CatchTheHole => "Catch the Hole",
            PoolGameMode.HungryHole => "Hungry Hole",
            PoolGameMode.SnakePool => "Snake Pool",
            PoolGameMode.PowerWashPool => "Power Wash Pool",
            _ => "Classique",
        };

        public static string TypeName(PoolMatchType type) => type == PoolMatchType.SpeedPool ? "Speed Pool" : "Tour par tour";

        // "Classique · Speed Pool · pouvoirs" (loading screen, end card).
        public string Describe() =>
            $"{ModeName(mode)} · {TypeName(type)}{(powers ? " · pouvoirs" : "")}";
    }
}
