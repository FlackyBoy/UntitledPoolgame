using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // What happens at the table when a ball is potted: the pocket's halo and
    // rising aura, and the slow-motion dip. Shared by the six pockets
    // (they aren't prefab instances of one another).
    [CreateAssetMenu(fileName = "PoolPotEffectSettings", menuName = "Pool/Pot Effect Settings")]
    public class PoolPotEffectSettings : ScriptableObject
    {
        [Header("Halo light")]
        public Color haloColor = new Color(1f, 0.85f, 0.35f); // warm gold
        // Depends on URP's Physical Light Units; this assumes they're off.
        public float haloIntensity = 8f;
        public float haloRange = 0.6f;
        public float haloDuration = 0.35f;

        [Header("Rising aura (CFXR3 Magic Aura A (Runic))")]
        public GameObject risingAuraPrefab;
        // Emission time; particles in flight then finish on their own.
        public float risingAuraActiveDuration = 1.2f;
        // A ceiling, not a wait: the aura is destroyed as soon as no particle
        // is left (PoolPocket), this only catches a system that never ends.
        public float risingAuraFadeOutBuffer = 4f;
        // The prefab's own scale/pace was built for a much bigger, slower
        // effect than a pool pocket needs.
        public float risingAuraScale = 0.4f;
        public float risingAuraSpeedMultiplier = 1.8f;

        [Header("Pocket pick: halo (8-ball call, powers aimed at a pocket)")]
        // Steady intensity on the pocket being pointed at (same light, colour
        // and range as the pot halo).
        public float selectionHighlightIntensity = 6f;

        [Header("Pocket pick: ring (8-ball call, powers aimed at a pocket)")]
        [Tooltip("Couleur du cercle autour du trou visé pendant le choix.")]
        public Color targetRingColor = Color.white;
        [Tooltip("Rayon du cercle, en multiple du rayon de la poche.")]
        public float targetRingRadiusScale = 1.3f;
        [Tooltip("Épaisseur du trait (mètres).")]
        public float targetRingWidth = 0.008f;
        [Tooltip("Hauteur (mètres) du cercle au-dessus du centre de la poche, pour qu'il ne passe pas sous le tapis.")]
        public float targetRingHeight = 0.004f;
        [Tooltip("Battements par seconde du cercle (il grossit et rétrécit).")]
        public float targetRingPulseRate = 2.5f;
        [Tooltip("Amplitude du battement, en fraction du rayon (0 = fixe).")]
        public float targetRingPulseAmount = 0.12f;

        [Header("Closed pocket (ClosePocketPower)")]
        // Optional cap shown on a closed pocket, used as is; empty = a flat
        // red cylinder.
        public GameObject closedPocketCapPrefab;
        // Scale of that prefab (the placeholder is already sized to the pocket).
        public float closedPocketCapScale = 1f;

        [Header("Slow motion")]
        public float potSlowMotionScale = 0.3f;
        // Real-world (unscaled) seconds — how long the dip itself lasts,
        // independent of how slow gameplay appears to move during it.
        public float potSlowMotionDuration = 0.12f;
    }
}
