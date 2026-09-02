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

        [Header("Crate visuals — optional, one prefab per PowerType")]
        // Assign your own crate/chest model here to use instead of the
        // auto-generated placeholder cube — PoolPowerCrate swaps to whichever
        // one matches the power it's currently carrying (and swaps again on
        // respawn if the newly rolled power is a different type). Leaving
        // one unassigned falls back to the tinted placeholder cube for that
        // type specifically, so this can be filled in gradually.
        public GameObject attackCratePrefab;
        public GameObject defenseCratePrefab;
        public GameObject effectCratePrefab;

        public GameObject GetCratePrefab(PowerType type) => type switch
        {
            PowerType.Attack => attackCratePrefab,
            PowerType.Defense => defenseCratePrefab,
            PowerType.Effect => effectCratePrefab,
            _ => null,
        };

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

        [Header("Power ball visuals — optional, one material per PowerType")]
        // Assign your own shader/material here to swap the ball's own
        // renderer to for as long as it carries this power (on top of the
        // glow light above) — PowerBall restores its normal material via
        // ClearGlow() once it stops. Leaving one unassigned just skips the
        // swap for that type, so this can be filled in gradually; the light
        // alone still marks which ball currently carries a power either way.
        public Material attackBallMaterial;
        public Material defenseBallMaterial;
        public Material effectBallMaterial;

        public Material GetBallMaterial(PowerType type) => type switch
        {
            PowerType.Attack => attackBallMaterial,
            PowerType.Defense => defenseBallMaterial,
            PowerType.Effect => effectBallMaterial,
            _ => null,
        };
    }
}
