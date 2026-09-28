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
        // A SAFETY CEILING, not a fixed wait: PoolPocket polls
        // ParticleSystem.IsAlive() every frame after emission stops and
        // destroys the instance the moment every system genuinely has no
        // particles left — this only kicks in if that never happens (a
        // looping system, etc.), so it can be generous without cutting
        // anything short.
        public float risingAuraFadeOutBuffer = 4f;
        // The prefab's own scale/pace was built for a much bigger, slower
        // effect than a pool pocket needs.
        public float risingAuraScale = 0.4f;
        public float risingAuraSpeedMultiplier = 1.8f;

        [Header("8-ball call-shot selection (top-down pocket picker)")]
        // Steady (no decay) intensity for whichever pocket is currently
        // nearest the selector while calling a shot on the 8 — reuses
        // haloColor/haloRange above rather than a separate color/range pair,
        // since it's the same light and the same "this pocket matters right
        // now" visual language as the pot halo, just held steady instead of
        // fired-and-decaying.
        public float selectionHighlightIntensity = 6f;

        [Header("Closed pocket (ClosePocketPower)")]
        // Optional — a prefab to show over a pocket while ClosePocketPower
        // has it blocked, instead of the auto-generated flat red cylinder
        // placeholder (same "prefab optional, falls back to a placeholder"
        // convention as PoolPowerSpawnSettings' crate prefabs). Instantiated
        // as-is (not tinted) — positioned by the prefab itself, since it's
        // expected to already look like a cap/plug.
        public GameObject closedPocketCapPrefab;
        // Multiplies the prefab's own scale (same idea as risingAuraScale
        // above) — a model authored at some arbitrary real-world size won't
        // generally already match a given pocket's radius, so this is the
        // knob to fit it without needing a rescaled duplicate prefab. Not
        // applied to the auto-generated placeholder, which is already sized
        // off the pocket's own radius directly.
        public float closedPocketCapScale = 1f;

        [Header("Slow motion")]
        public float potSlowMotionScale = 0.3f;
        // Real-world (unscaled) seconds — how long the dip itself lasts,
        // independent of how slow gameplay appears to move during it.
        public float potSlowMotionDuration = 0.12f;
    }
}
