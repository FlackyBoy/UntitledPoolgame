using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Everything about HOW powers get randomly handed out — the pool to draw
    // from, the color code per PowerType (crates and the ball glow both read
    // this, so they always agree), and the random timing for crate respawns
    // and ball power rotation. One shared asset, same Resources-loaded
    // pattern as PoolPhysicsSettings — this is the "config file" the powers'
    // random distribution is meant to be tunable from.
    [CreateAssetMenu(fileName = "PoolPowerSpawnSettings", menuName = "Pool/Power Spawn Settings")]
    public class PoolPowerSpawnSettings : ScriptableObject
    {
        [Header("Power pool")]
        // Drawn from at random for both crates and the ball rotation below —
        // an empty/missing pool just means nothing spawns (checked by both
        // callers) rather than a hard error, so this can be filled in
        // gradually as more powers exist.
        public PoolPower[] availablePowers;

        [Header("Color code")]
        public Color attackColor = new Color(0.9f, 0.2f, 0.2f);
        public Color defenseColor = new Color(0.25f, 0.55f, 1f);
        public Color effectColor = new Color(0.3f, 0.9f, 0.4f);

        public Color GetColor(PowerType type) => type switch
        {
            PowerType.Attack => attackColor,
            PowerType.Defense => defenseColor,
            PowerType.Effect => effectColor,
            _ => Color.white,
        };

        [Header("Power crates")]
        // How many PoolPowerCrate instances are alive/spawned on the table
        // at once — PoolPowerCrateManager keeps this many active, drawing
        // spawn locations from PoolPowerSpawnPoint markers in the scene.
        public int activeCrateCount = 3;
        // Random real-world delay range before a picked-up crate respawns
        // (new location, freshly rolled power).
        public float crateRespawnMinDelay = 15f;
        public float crateRespawnMaxDelay = 30f;

        [Header("Power ball rotation")]
        // How often the ball currently carrying a power changes — a new
        // random active (not yet pocketed) ball is chosen, with a freshly
        // rolled power, on a random interval in this range.
        public float ballRotationMinInterval = 20f;
        public float ballRotationMaxInterval = 40f;
        // Point light intensity on the glowing ball — same caveat as
        // PoolPotEffectSettings.haloIntensity (depends on URP Physical
        // Light Units).
        public float ballGlowIntensity = 4f;
        public float ballGlowRange = 0.3f;
    }
}
