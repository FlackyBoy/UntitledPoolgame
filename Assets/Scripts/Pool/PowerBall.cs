using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Marker for a numbered ball that also grants a power when legally
    // pocketed — same pattern as Cue.cs (a plain marker component; base rules
    // never need to know about it). PoolMatchRules listens for this via the
    // existing PoolBall.Pocketed hook, independently of group/order rules —
    // pocketing a power ball still counts normally for 8-ball/9-ball/14.1.
    //
    // Every non-cue ball gets one of these at rack time (see
    // PoolTableBuilder), starting with Power == null — PoolPowerBallRotator
    // is what actually assigns/clears Power and the glow as it rotates which
    // ball currently carries one, rather than a fixed manual assignment.
    public class PowerBall : MonoBehaviour
    {
        private PoolPower power;
        public PoolPower Power => power;

        private Light glowLight;
        private Renderer ballRenderer;
        // Whatever material the ball normally shows (its number/color
        // texture) — cached once so ClearGlow() can restore it after a
        // custom PowerType material was swapped in.
        private Material originalMaterial;

        private void Awake()
        {
            ballRenderer = GetComponent<Renderer>();
            if (ballRenderer != null) originalMaterial = ballRenderer.sharedMaterial;
        }

        // Called by PoolPowerBallRotator. color/intensity/range come from
        // PoolPowerSpawnSettings so the glow always matches the crates'
        // color code for the same PowerType. material is optional (see
        // PoolPowerSpawnSettings.GetBallMaterial) — a custom shader/material
        // to swap the ball's own renderer to for as long as it carries this
        // power, on top of the light. Null just skips the swap and leaves
        // the ball's normal material alone.
        public void SetGlow(PoolPower assignedPower, Color color, float intensity, float range, Material material)
        {
            power = assignedPower;

            if (glowLight == null)
            {
                GameObject lightObject = new GameObject("PowerGlow");
                lightObject.transform.SetParent(transform, worldPositionStays: false);
                lightObject.transform.localPosition = Vector3.zero;
                glowLight = lightObject.AddComponent<Light>();
                glowLight.type = LightType.Point;
            }

            glowLight.color = color;
            glowLight.range = range;
            glowLight.intensity = intensity;
            glowLight.enabled = true;

            // sharedMaterial, not material: this is meant to be the SAME
            // asset across every ball currently carrying this PowerType, not
            // a per-ball instance — nothing here ever edits its properties.
            if (ballRenderer != null && material != null)
                ballRenderer.sharedMaterial = material;
        }

        // Called by PoolPowerBallRotator when it's this ball's turn to stop
        // glowing (rotated onto another ball, or this one got pocketed).
        public void ClearGlow()
        {
            power = null;
            if (glowLight != null) glowLight.enabled = false;
            if (ballRenderer != null && originalMaterial != null)
                ballRenderer.sharedMaterial = originalMaterial;
        }
    }
}
