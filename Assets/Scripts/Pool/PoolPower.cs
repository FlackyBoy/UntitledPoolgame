using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Base for a storable, player-activated power — picked up by pocketing a
    // PowerBall or rolling the cue ball through a PoolPowerCrate, held (one
    // at a time, see PoolMatchRules.GrantPower), and triggered later with the
    // "use power" input. A ScriptableObject asset per power (same pattern as
    // PoolPhysicsSettings/PoolTableAssetSettings) so adding a new power is
    // just a new subclass + a new asset — nothing else needs to change.
    public abstract class PoolPower : ScriptableObject
    {
        [SerializeField] private string powerName = "Power";
        public string PowerName => powerName;

        public abstract PowerType Type { get; }

        // Whether this power can only be activated on the holder's own turn
        // — see LocalPoolPowerController, which is the only place that
        // actually enforces it (only meaningful with two separate physical
        // inputs, i.e. split-screen; online can't gate on this at all yet,
        // see TODO.md). Set per power asset rather than derived from Type —
        // a power's category (Attack/Defense/Effect) doesn't reliably
        // predict this on its own (e.g. a Defense power that reacts to the
        // opponent's shot needs it off, one that buffs the holder's own next
        // shot needs it on), so this is a deliberate per-power author
        // decision instead of an inferred rule. Defaults to true (the
        // stricter, previously-hardcoded behavior) — existing power assets
        // need this unchecked by hand in the Inspector for whichever ones
        // are meant to target/react to the opponent (see TODO.md for which).
        [SerializeField] private bool requiresOwnTurn = true;
        public bool RequiresOwnTurn => requiresOwnTurn;

        // Called the moment the holding player activates it.
        // activatingPlayer is 0 or 1 — PoolMatchRules.CurrentPlayer indexing.
        public abstract void Activate(PoolMatchRules match, int activatingPlayer);
    }
}
