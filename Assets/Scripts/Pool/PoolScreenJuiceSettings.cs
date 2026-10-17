using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Screen effects of the local players: Vision Impair's overlay and shake,
    // the charge shake and zoom, the shake and flash pulses (shot, pot, foul,
    // power pickup).
    [CreateAssetMenu(fileName = "PoolScreenJuiceSettings", menuName = "Pool/Screen Juice Settings")]
    public class PoolScreenJuiceSettings : ScriptableObject
    {
        [Header("Vision Impair — screen flash")]
        public Color visionImpairOverlayColor = Color.white;
        [Range(0f, 1f)] public float visionImpairMaxOverlayAlpha = 0.6f;
        public float visionImpairFlickerFrequency = 8f; // blinks per second

        [Header("Vision Impair — camera shake")]
        public float visionImpairShakeMagnitude = 0.03f; // meters, world space

        [Header("Charging a shot")]
        // How much closer the camera creeps toward the cue tip as the shot
        // charges up — read by LocalPoolAimController.UpdateAim().
        public float chargeZoomDistance = 0.15f;
        // Grows with the charge, read every frame.
        public float chargeShakeMaxMagnitude = 0.012f;

        [Header("Shot fired")]
        public float shotShakeMagnitude = 0.015f;
        public float shotShakeDuration = 0.1f;

        [Header("Ball pocketed")]
        // No flash: a pot lights up the pocket (PoolPotEffectSettings).
        public float potShakeMagnitude = 0.02f;
        public float potShakeDuration = 0.15f;

        [Header("Foul")]
        public float foulShakeMagnitude = 0.04f;
        public float foulShakeDuration = 0.25f;
        public Color foulFlashColor = Color.red;
        [Range(0f, 1f)] public float foulFlashMaxAlpha = 0.25f;
        public float foulFlashDuration = 0.3f;

        [Header("Power pickup")]
        public float pickupShakeMagnitude = 0.02f;
        public float pickupShakeDuration = 0.15f;
        public Color pickupFlashColor = new Color(0.4f, 0.85f, 1f); // cool cyan
        [Range(0f, 1f)] public float pickupFlashMaxAlpha = 0.15f;
        public float pickupFlashDuration = 0.2f;
    }
}
