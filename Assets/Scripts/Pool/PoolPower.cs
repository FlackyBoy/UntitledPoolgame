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
        // — see PoolMatchRules.CheckPowerUse, the only place that
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

        // Only for a power usable off-turn (Requires Own Turn unchecked):
        // launched during the holder's own turn, it waits for the opponent's
        // turn (each power queues itself, see PoolMatchRules.Queue*), or is
        // refused so it can only be sent while the opponent plays — the
        // author picks per power which surprise they want.
        [Tooltip("Pouvoir utilisable hors de son tour (Requires Own Turn décoché) : coché = lancé pendant son propre tour, il attend le début du tour adverse ; décoché = refusé pendant son tour, il ne part que pendant le tour de l'adversaire.")]
        [SerializeField] private bool allowDuringOwnTurn = true;
        public bool AllowDuringOwnTurn => allowDuringOwnTurn;

        // Closing a pocket right before a ball drops in is part of the chaos;
        // a power for which that would be unfair unchecks this.
        [Tooltip("Peut être lancé pendant que les billes roulent (tir en cours). Décoché = refusé jusqu'à l'arrêt des billes.")]
        [SerializeField] private bool allowWhileBallsMoving = true;
        public bool AllowWhileBallsMoving => allowWhileBallsMoving;

        // Who feels it: the opponent for an Attack, the holder otherwise.
        // Drives the HUD announcement (PoolMatchRules.PowerActivated).
        public bool TargetsOpponent => Type == PowerType.Attack;

        // Called the moment the holding player activates it.
        // activatingPlayer is 0 or 1 — PoolMatchRules.CurrentPlayer indexing.
        public abstract void Activate(PoolMatchRules match, int activatingPlayer);
    }
}
