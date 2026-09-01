using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Everything that happens at the TABLE when a ball is pocketed — the
    // pocket's own light halo and rising CFXR aura, plus the global
    // slow-motion dip. One shared asset instead of a private copy on each of
    // the 6 independently-generated PoolPocket instances (unlike the player
    // prefabs, pockets aren't prefab instances of each other — editing one
    // today doesn't touch the other five) and a further copy of the slowmo
    // values on PoolMatchRules. Loaded from Resources, same pattern as
    // PoolPhysicsSettings.
    [CreateAssetMenu(fileName = "PoolPotEffectSettings", menuName = "Pool/Pot Effect Settings")]
    public class PoolPotEffectSettings : ScriptableObject
    {
        [Header("Halo light")]
        public Color haloColor = new Color(1f, 0.85f, 0.35f); // warm gold
        // Point light intensity is on a project-specific scale (depends on
        // whether URP's Physical Light Units is on) — this default assumes
        // it's off.
        public float haloIntensity = 8f;
        public float haloRange = 0.6f;
        public float haloDuration = 0.35f;

        [Header("Rising aura (CFXR3 Magic Aura A (Runic))")]
        public GameObject risingAuraPrefab;
        // How long the aura actively emits new particles before winding
        // down — after this, emission stops but particles already in
        // flight keep playing out their own lifetime for a soft finish.
        public float risingAuraActiveDuration = 1.2f;
        // Extra time kept alive after emission stops, for the last emitted
        // particles to finish fading before the GameObject is destroyed.
        public float risingAuraFadeOutBuffer = 1f;
        // The prefab's own scale/pace was built for a much bigger, slower
        // effect than a pool pocket needs.
        public float risingAuraScale = 0.4f;
        public float risingAuraSpeedMultiplier = 1.8f;

        [Header("Slow motion")]
        public float potSlowMotionScale = 0.3f;
        // Real-world (unscaled) seconds — how long the dip itself lasts,
        // independent of how slow gameplay appears to move during it.
        public float potSlowMotionDuration = 0.12f;
    }
}
