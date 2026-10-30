namespace UntitledPoolGame.Pool
{
    public enum PocketSizeRole { Giant, Tiny }

    // A Pocket Size power: only ever in that mode, and only for one role.
    // Its crate is tinted with the role's colour; the other role running
    // over it destroys it (PocketSizeRuleSet.TryTakeCrate).
    public abstract class PocketSizePower : PoolPower
    {
        public abstract PocketSizeRole Role { get; }

        public override bool AvailableIn(PoolGameMode mode) => mode == PoolGameMode.PocketSize;

        public sealed override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            PocketSizeRuleSet rules = PocketSizeRuleSet.Current;
            // Held over from the other role (the stock is cleared between
            // rounds, this is a safety net): used up for nothing.
            if (rules == null || rules.RoleOf(activatingPlayer) != Role) return;
            Apply(rules, activatingPlayer);
        }

        protected abstract void Apply(PocketSizeRuleSet rules, int player);
    }
}
