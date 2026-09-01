using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Two things live here, sharing the same camera-state-aware plumbing
    // (cameraRestLocalPosition, the IsAiming/IsPlacementViewActive guards)
    // rather than as separate components, to avoid a second independent
    // writer fighting over the same camera transform every frame — exactly
    // the class of bug already hit twice this project (vision-impair shake
    // vs. the aim orbit, then vs. the ball-in-hand placement view):
    //
    // 1) The turn-bound VisionImpairPower debuff against THIS player — a
    //    blinking white overlay, a constant shake, reduced look sensitivity.
    // 2) Short one-shot "impact" juice (shake + flash pulse, both decaying
    //    over a fixed duration) for shots fired, balls pocketed, fouls, and
    //    power pickups — see RequestShake/RequestFlash/PlayShotFeedback.
    //
    // Offline only for now: this needs to know "am I player 0 or player 1"
    // to look up the right slot, which PlayerInput.playerIndex gives locally
    // but has no online equivalent yet (see TODO.md — same limitation as
    // ball-in-hand placement and power activation).
    [RequireComponent(typeof(LocalFpsPlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolPowerEffectReceiver : MonoBehaviour
    {
        [Header("Vision Impair — screen flash")]
        [SerializeField] private Color overlayColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float maxOverlayAlpha = 0.6f;
        [SerializeField] private float flickerFrequency = 8f; // blinks per second

        [Header("Vision Impair — camera shake")]
        [SerializeField] private float shakeMagnitude = 0.03f; // meters, world space

        [Header("Impact juice — shot fired")]
        [SerializeField] private float shotShakeMagnitude = 0.015f;
        [SerializeField] private float shotShakeDuration = 0.1f;

        [Header("Impact juice — ball pocketed")]
        [SerializeField] private float potShakeMagnitude = 0.02f;
        [SerializeField] private float potShakeDuration = 0.15f;
        [SerializeField] private Color potFlashColor = new Color(1f, 0.85f, 0.35f); // warm gold
        [SerializeField, Range(0f, 1f)] private float potFlashMaxAlpha = 0.15f;
        [SerializeField] private float potFlashDuration = 0.2f;

        [Header("Impact juice — foul")]
        [SerializeField] private float foulShakeMagnitude = 0.04f;
        [SerializeField] private float foulShakeDuration = 0.25f;
        [SerializeField] private Color foulFlashColor = Color.red;
        [SerializeField, Range(0f, 1f)] private float foulFlashMaxAlpha = 0.25f;
        [SerializeField] private float foulFlashDuration = 0.3f;

        [Header("Impact juice — power pickup")]
        [SerializeField] private float pickupShakeMagnitude = 0.02f;
        [SerializeField] private float pickupShakeDuration = 0.15f;
        [SerializeField] private Color pickupFlashColor = new Color(0.4f, 0.85f, 1f); // cool cyan
        [SerializeField, Range(0f, 1f)] private float pickupFlashMaxAlpha = 0.15f;
        [SerializeField] private float pickupFlashDuration = 0.2f;

        private LocalFpsPlayerController fpsController;
        private LocalPoolAimController aimController;
        private PlayerInput playerInput;
        private Camera playerCamera;
        // The camera's local position at rest, relative to its parent
        // (cameraPivot) — captured once, before any shaking, so the
        // not-aiming shake branch below always has a stable zero to jitter
        // around instead of drifting further from center every frame.
        private Vector3 cameraRestLocalPosition;

        // One-shot decaying shake pulse — a new request overwrites whatever
        // was still running rather than stacking. Two impact events landing
        // the very same frame (e.g. a power-ball being pocketed, which fires
        // both the pot juice and the pickup juice) means only the later one
        // is felt; acceptable for now, not worth a queue for how rarely it
        // actually coincides.
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
            playerInput = GetComponent<PlayerInput>();
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera != null) cameraRestLocalPosition = playerCamera.transform.localPosition;
        }

        private void OnEnable()
        {
            PoolBall.Pocketed += HandleBallPocketed;
            PoolMatchRules.Fouled += HandleFoul;
            PoolMatchRules.PowerGranted += HandlePowerGranted;
        }

        private void OnDisable()
        {
            PoolBall.Pocketed -= HandleBallPocketed;
            PoolMatchRules.Fouled -= HandleFoul;
            PoolMatchRules.PowerGranted -= HandlePowerGranted;
        }

        // Shared by every ball pocketed on the table — no per-player
        // filtering, both screens feel the same satisfying pot.
        private void HandleBallPocketed(PoolBall ball)
        {
            RequestShake(potShakeMagnitude, potShakeDuration);
            RequestFlash(potFlashColor, potFlashMaxAlpha, potFlashDuration);
        }

        // Same reasoning as pocketed — a foul is a shared-table moment,
        // both players feel it, not just whoever committed it.
        private void HandleFoul()
        {
            RequestShake(foulShakeMagnitude, foulShakeDuration);
            RequestFlash(foulFlashColor, foulFlashMaxAlpha, foulFlashDuration);
        }

        // Unlike pocketed/foul, only the player who actually picked up the
        // power should feel this — GetEffectivePlayerIndex so hot-seat solo
        // resolves to whichever side is currently up.
        private void HandlePowerGranted(int player)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || player != rules.GetEffectivePlayerIndex(playerInput.playerIndex)) return;
            RequestShake(pickupShakeMagnitude, pickupShakeDuration);
            RequestFlash(pickupFlashColor, pickupFlashMaxAlpha, pickupFlashDuration);
        }

        // Called directly by LocalPoolAimController.Shoot() on this same
        // player's GameObject right after the shot fires — not event-driven
        // like the others above, since a shot's recoil kick only ever
        // belongs to the player who took it.
        public void PlayShotFeedback() => RequestShake(shotShakeMagnitude, shotShakeDuration);

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
            fpsController.SensitivityMultiplier = rules != null
                ? rules.GetVisionImpairmentSensitivityMultiplier(rules.GetEffectivePlayerIndex(playerInput.playerIndex))
                : 1f;

            // Ticked down here, once a frame — never inside OnGUI, which can
            // run its method body more than once per frame (Layout then
            // Repaint passes) and would double-decay these otherwise. Same
            // lesson as the mode-select screen's earlier GUILayout bug.
            if (impactShakeRemaining > 0f) impactShakeRemaining = Mathf.Max(0f, impactShakeRemaining - Time.unscaledDeltaTime);
            if (impactFlashRemaining > 0f) impactFlashRemaining = Mathf.Max(0f, impactFlashRemaining - Time.unscaledDeltaTime);
        }

        // LateUpdate, not Update: guarantees this runs AFTER whichever system
        // positioned the camera this frame — LocalFpsPlayerController (only
        // ever rotates cameraPivot, never touches the camera's own position)
        // or LocalPoolAimController while aiming (sets cameraTransform's
        // WORLD position/rotation directly, fresh, every frame — see
        // IsAiming). Two different shake strategies below because of that:
        // while aiming, the orbit recomputes the camera's world position
        // from scratch every frame regardless of what we do to it here, so
        // an ADDITIVE world-space nudge on top is safe and never
        // accumulates. Outside of aiming, nothing else ever resets the
        // camera's own local position, so we always rebuild it fresh from
        // the known rest value instead — an additive nudge there WOULD
        // drift further off-center every single frame.
        private void LateUpdate()
        {
            if (playerCamera == null) return;

            // A third camera state besides aiming/normal FPS — the ball-in-hand
            // top-down view (after a foul) ALSO drives cameraTransform's world
            // position/rotation directly, every frame, same as the aim orbit.
            // Leaving it alone entirely here avoids fighting it the same way
            // aiming's branch below avoids fighting the orbit — touching either
            // localPosition or world position from this script while it's
            // active would fight/override that positioning.
            if (aimController != null && aimController.IsPlacementViewActive) return;

            PoolMatchRules rules = PoolMatchRules.Instance;
            float strength = rules != null
                ? rules.VisionImpairmentStrength(rules.GetEffectivePlayerIndex(playerInput.playerIndex))
                : 0f;

            // Linear decay from full magnitude to zero over the pulse's
            // duration — same additive combination as the Vision Impair
            // shake below, so a shot/pot/foul/pickup landing while the
            // player also happens to be vision-impaired just adds on top
            // instead of one silently overriding the other.
            float impactT = impactShakeDuration > 0f ? impactShakeRemaining / impactShakeDuration : 0f;
            float totalMagnitude = shakeMagnitude * strength + impactShakeMagnitude * impactT;

            bool isAiming = aimController != null && aimController.IsAiming;

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

            // GetEffectivePlayerIndex: in hot-seat solo (one PlayerInput
            // playing both sides), this one screen represents whichever side
            // is currently up, not permanently slot 0 — without this, a
            // debuff queued for "player 1" would never find a receiver to
            // show it on when there's no second PlayerInput at all.
            float strength = rules.VisionImpairmentStrength(rules.GetEffectivePlayerIndex(playerInput.playerIndex));

            // A true on/off blink (not a smooth pulse) — unscaledTime so the
            // blink rate doesn't slow down along with any hit-stop/pause
            // effects added later.
            bool visionOverlayVisible = strength > 0f &&
                Mathf.Sin(Time.unscaledTime * flickerFrequency * Mathf.PI * 2f) > 0f;

            float impactAlpha = impactFlashDuration > 0f
                ? impactFlashMaxAlpha * (impactFlashRemaining / impactFlashDuration)
                : 0f;

            if (!visionOverlayVisible && impactAlpha <= 0f) return;

            // OnGUI draws across the ENTIRE window by default, not just this
            // player's split-screen half — a full Screen.width/height rect
            // covered both players regardless of who the effect targeted.
            // Camera.rect is the normalized viewport this player's camera
            // actually renders to; converting it to a screen-space Rect
            // (and flipping Y — viewport space is bottom-up, GUI space is
            // top-down) confines the overlay to just their half.
            Rect viewport = playerCamera.rect;
            Rect screenRect = new Rect(
                viewport.x * Screen.width,
                (1f - viewport.y - viewport.height) * Screen.height,
                viewport.width * Screen.width,
                viewport.height * Screen.height);

            if (visionOverlayVisible)
            {
                GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, strength * maxOverlayAlpha);
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            }

            // A fade, not a blink — drawn as its own layer on top so it
            // still reads clearly even while the Vision Impair overlay is
            // also up (e.g. a foul landing mid-impaired-turn).
            if (impactAlpha > 0f)
            {
                GUI.color = new Color(impactFlashColor.r, impactFlashColor.g, impactFlashColor.b, impactAlpha);
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }
    }
}
