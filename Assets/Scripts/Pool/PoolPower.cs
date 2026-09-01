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

        // Called the moment the holding player activates it.
        // activatingPlayer is 0 or 1 — PoolMatchRules.CurrentPlayer indexing.
        public abstract void Activate(PoolMatchRules match, int activatingPlayer);
    }
}
