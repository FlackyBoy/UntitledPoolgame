using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Everything this player's camera feels: the Vision Impair debuff
    // (blinking overlay, shake, lower sensitivity) and short impact pulses
    // (shot, pot, foul, power pickup). One component so the camera has a
    // single writer. Numbers in PoolScreenJuiceSettings. Offline only (needs
    // PlayerInput.playerIndex).
    [RequireComponent(typeof(LocalFpsPlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolPowerEffectReceiver : MonoBehaviour
    {
        private static PoolScreenJuiceSettings settings;

        private LocalFpsPlayerController fpsController;
        private LocalPoolAimController aimController;
        private LocalPlayerRagdollController ragdollController;
        private PlayerInput playerInput;
        private Camera playerCamera;
        // Camera local position at rest, the zero the shake jitters around.
        private Vector3 cameraRestLocalPosition;

        // A new pulse replaces the running one rather than stacking.
        private float impactShakeMagnitude;
        private float impactShakeDuration;
        private float impactShakeRemaining;

        // Same one-shot/overwrite deal as the shake above, but a fade
        // instead of a blink (unlike the Vision Impair overlay).
        private Color impactFlashColor;
        private float impactFlashMaxAlpha;
        private float impactFlashDuration;
        private float impactFlashRemaining;

        private void Awake()
        {
            fpsController = GetComponent<LocalFpsPlayerController>();
            aimController = GetComponent<LocalPoolAimController>();
            ragdollController = GetComponent<LocalPlayerRagdollController>();
            playerInput = GetComponent<PlayerInput>();
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera != null) cameraRestLocalPosition = playerCamera.transform.localPosition;

            if (settings == null)
                settings = PoolSettingsLoader.LoadOrDefault<PoolScreenJuiceSettings>("PoolScreenJuiceSettings");
        }

        private static readonly System.Collections.Generic.List<LocalPoolPowerEffectReceiver> all =
            new System.Collections.Generic.List<LocalPoolPowerEffectReceiver>();

        // From a Feel sequence (MMF_PoolScreenJuice): shake and/or flash on
        // that player's camera (-1 = everyone). Zero = that part skipped.
        public static void PlayImpact(int player, float shakeMagnitude, float shakeDuration, Color flashColor, float flashAlpha, float flashDuration)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            foreach (LocalPoolPowerEffectReceiver r in all)
            {
                int index = rules != null ? rules.GetEffectivePlayerIndex(r.playerInput.playerIndex) : r.playerInput.playerIndex;
                if (player >= 0 && index != player) continue;
                if (shakeMagnitude > 0f && shakeDuration > 0f) r.RequestShake(shakeMagnitude, shakeDuration);
                if (flashAlpha > 0f && flashDuration > 0f) r.RequestFlash(flashColor, flashAlpha, flashDuration);
            }
        }

        private void OnEnable()
        {
            all.Add(this);
            PoolBall.Pocketed += HandleBallPocketed;
            PoolMatchRules.Fouled += HandleFoul;
            PoolMatchRules.PowerGranted += HandlePowerGranted;
        }

        private void OnDisable()
        {
            all.Remove(this);
            PoolBall.Pocketed -= HandleBallPocketed;
            PoolMatchRules.Fouled -= HandleFoul;
            PoolMatchRules.PowerGranted -= HandlePowerGranted;
        }

        // Every pot shakes both screens (the flash is the halo at the pocket).
        private void HandleBallPocketed(PoolBall ball, PoolPocket pocket)
        {
            if (!PoolPocket.DefaultPotEffects) return;
            RequestShake(settings.potShakeMagnitude, settings.potShakeDuration);
        }

        // Same reasoning as pocketed — a foul is a shared-table moment,
        // both players feel it, not just whoever committed it.
        private void HandleFoul()
        {
            RequestShake(settings.foulShakeMagnitude, settings.foulShakeDuration);
            RequestFlash(settings.foulFlashColor, settings.foulFlashMaxAlpha, settings.foulFlashDuration);
        }

        // Only the player who picked the power up.
        private void HandlePowerGranted(int player)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || player != rules.GetEffectivePlayerIndex(playerInput.playerIndex)) return;
            RequestShake(settings.pickupShakeMagnitude, settings.pickupShakeDuration);
            RequestFlash(settings.pickupFlashColor, settings.pickupFlashMaxAlpha, settings.pickupFlashDuration);
        }

        // Called by Shoot on this player.
        public void PlayShotFeedback() => RequestShake(settings.shotShakeMagnitude, settings.shotShakeDuration);

        private void RequestShake(float magnitude, float duration)
        {
            impactShakeMagnitude = magnitude;
            impactShakeDuration = duration;
            impactShakeRemaining = duration;
        }

        private void RequestFlash(Color color, float maxAlpha, float duration)
        {
            impactFlashColor = color;
            impactFlashMaxAlpha = maxAlpha;
            impactFlashDuration = duration;
            impactFlashRemaining = duration;
        }

        private void Update()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            int effectivePlayer = rules != null ? rules.GetEffectivePlayerIndex(playerInput.playerIndex) : 0;
            // Multiplied, so both effects apply if they ever overlap.
            fpsController.SensitivityMultiplier = rules != null
                ? rules.GetVisionImpairmentSensitivityMultiplier(effectivePlayer) * rules.GetInvertedControlsSensitivityMultiplier(effectivePlayer)
                : 1f;
            fpsController.InvertLook = rules != null && rules.IsControlsInverted(effectivePlayer);

            // Here, not in OnGUI, which runs several times per frame.
            if (impactShakeRemaining > 0f) impactShakeRemaining = Mathf.Max(0f, impactShakeRemaining - Time.unscaledDeltaTime);
            if (impactFlashRemaining > 0f) impactFlashRemaining = Mathf.Max(0f, impactFlashRemaining - Time.unscaledDeltaTime);
        }

        // LateUpdate, after whatever placed the camera. While aiming the orbit
        // sets the world pose fresh every frame, so the shake is added on top.
        // Otherwise the local position is rebuilt from the rest value, or it
        // would drift.
        private void LateUpdate()
        {
            if (playerCamera == null) return;

            // The top-down view sets the camera itself: left alone.
            if (aimController != null && aimController.IsPlacementViewActive) return;

            // Same for the ragdoll camera.
            if (ragdollController != null && ragdollController.IsRagdollCameraActive) return;

            PoolMatchRules rules = PoolMatchRules.Instance;
            float strength = rules != null
                ? rules.VisionImpairmentStrength(rules.GetEffectivePlayerIndex(playerInput.playerIndex))
                : 0f;

            // Added to the Vision Impair shake rather than replacing it.
            float impactT = impactShakeDuration > 0f ? impactShakeRemaining / impactShakeDuration : 0f;

            bool isAiming = aimController != null && aimController.IsAiming;

            // Follows the charge directly, no decay.
            float chargeMagnitude = isAiming ? settings.chargeShakeMaxMagnitude * aimController.ChargeFraction : 0f;

            float totalMagnitude = settings.visionImpairShakeMagnitude * strength + impactShakeMagnitude * impactT + chargeMagnitude;

            if (totalMagnitude <= 0f)
            {
                if (!isAiming) playerCamera.transform.localPosition = cameraRestLocalPosition;
                return;
            }

            Vector3 shakeOffset = Random.insideUnitSphere * totalMagnitude;
            if (isAiming)
                playerCamera.transform.position += shakeOffset;
            else
                playerCamera.transform.localPosition = cameraRestLocalPosition + shakeOffset;
        }

        private void OnGUI()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || playerCamera == null) return;

            // Effective index, so hot-seat solo shows the side being played.
            float strength = rules.VisionImpairmentStrength(rules.GetEffectivePlayerIndex(playerInput.playerIndex));

            // A hard blink, on unscaled time.
            bool visionOverlayVisible = strength > 0f &&
                Mathf.Sin(Time.unscaledTime * settings.visionImpairFlickerFrequency * Mathf.PI * 2f) > 0f;

            float impactAlpha = impactFlashDuration > 0f
                ? impactFlashMaxAlpha * (impactFlashRemaining / impactFlashDuration)
                : 0f;

            if (!visionOverlayVisible && impactAlpha <= 0f) return;

            // OnGUI covers the whole window: limit it to this player's
            // viewport (Y flipped, GUI space is top-down).
            Rect viewport = playerCamera.rect;
            Rect screenRect = new Rect(
                viewport.x * Screen.width,
                (1f - viewport.y - viewport.height) * Screen.height,
                viewport.width * Screen.width,
                viewport.height * Screen.height);

            if (visionOverlayVisible)
            {
                Color overlayColor = settings.visionImpairOverlayColor;
                GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, strength * settings.visionImpairMaxOverlayAlpha);
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            }

            // A fade on its own layer, readable over the Vision Impair overlay.
            if (impactAlpha > 0f)
            {
                GUI.color = new Color(impactFlashColor.r, impactFlashColor.g, impactFlashColor.b, impactAlpha);
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }
    }
}
