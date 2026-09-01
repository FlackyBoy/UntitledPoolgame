#if UNITY_EDITOR
using UnityEngine;

namespace UntitledPoolGame.PoolEditor
{
    // Assign your custom table model's prefab here, and tune these dimensions
    // to match it — Tools > Pool > Select Custom Table Settings to open this
    // asset. Every "Build Table"/"Attach Physics" command in PoolTableBuilder
    // reads these instead of hardcoded constants, so fitting a new table
    // model is just a matter of editing numbers in the Inspector, no code
    // editing needed. Defaults match the original placeholder table
    // (approximate regulation size, in meters).
    public class PoolTableAssetSettings : ScriptableObject
    {
        [Header("Custom table model")]
        public GameObject customTablePrefab;

        [Header("Table dimensions (meters)")]
        [Tooltip("Length of the playable felt area (the long side).")]
        public float playLength = 2.24f;
        [Tooltip("Width of the playable felt area (the short side).")]
        public float playWidth = 1.12f;
        [Tooltip("World-space height (Y) of the top of the felt.")]
        public float tableSurfaceY = 1.0f;
        [Tooltip("Thickness of the generated felt collider — cosmetic, any small value works.")]
        public float surfaceThickness = 0.05f;
        [Tooltip("Height of the rail/cushion colliders above the felt.")]
        public float railHeight = 0.045f;
        [Tooltip("How far the rail colliders extend past the play area's edge.")]
        public float railThickness = 0.06f;

        [Header("Balls & pockets (meters)")]
        public float ballDiameter = 0.057f;
        [Tooltip("Radius of each pocket's trigger collider.")]
        public float pocketRadius = 0.097f;

        public float BallRadius => ballDiameter / 2f;
    }
}
#endif
