using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // An object ball that also grants a power when potted (it still counts
    // normally for the rules). Every object ball has one; PoolPowerBallRotator
    // decides which one carries a power and sets its glow.
    public class PowerBall : MonoBehaviour
    {
        private PoolPower power;
        public PoolPower Power => power;

        private Light glowLight;
        private Renderer ballRenderer;
        // The ball's own material, restored by ClearGlow.
        private Material originalMaterial;
        public Material OriginalMaterial => originalMaterial;

        private void Awake()
        {
            ballRenderer = GetComponent<Renderer>();
            if (ballRenderer != null) originalMaterial = ballRenderer.sharedMaterial;
        }

        // Called by PoolPowerBallRotator. Colours match the crates of the same
        // type. material (optional) replaces the ball's material while it
        // carries the power.
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

            // sharedMaterial: one asset for every ball of that type. Through
            // BallDisguise, so it waits while all balls look alike.
            if (ballRenderer != null && material != null)
                BallDisguise.SetBaseMaterial(ballRenderer, material);
        }

        // Called by PoolPowerBallRotator when it's this ball's turn to stop
        // glowing (rotated onto another ball, or this one got pocketed).
        public void ClearGlow()
        {
            power = null;
            if (glowLight != null) glowLight.enabled = false;
            if (ballRenderer != null && originalMaterial != null)
                BallDisguise.SetBaseMaterial(ballRenderer, originalMaterial);
        }
    }
}
