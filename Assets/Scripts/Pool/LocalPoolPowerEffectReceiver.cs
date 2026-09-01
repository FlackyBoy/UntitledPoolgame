using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Applies whatever debuff PoolMatchRules currently has active against
    // THIS player (see VisionImpairPower/ImpairVision) — a fast-blinking
    // white screen overlay, a screen shake, and reduced look sensitivity for
    // a few seconds. Offline only for now: this needs to know "am I player 0
    // or player 1" to look up the right slot, which PlayerInput.playerIndex
    // gives locally but has no online equivalent yet (see TODO.md — same
    // limitation as ball-in-hand placement and power activation).
    [RequireComponent(typeof(LocalFpsPlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolPowerEffectReceiver : MonoBehaviour
    {
        [Header("Screen flash")]
        [SerializeField] private Color overlayColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float maxOverlayAlpha = 0.6f;
        [SerializeField] private float flickerFrequency = 8f; // blinks per second

        [Header("Camera shake")]
        [SerializeField] private float shakeMagnitude = 0.03f; // meters, world space

        private LocalFpsPlayerController fpsController;
        private LocalPoolAimController aimController;
        private PlayerInput playerInput;
        private Camera playerCamera;
        // The camera's local position at rest, relative to its parent
        // (cameraPivot) — captured once, before any shaking, so the
        // not-aiming shake branch below always has a stable zero to jitter
        // around instead of drifting further from center every frame.
        private Vector3 cameraRestLocalPosition;

        private void Awake()
        {
            fpsController = GetComponent<LocalFpsPlayerController>();
            aimController = GetComponent<LocalPoolAimController>();
            playerInput = GetComponent<PlayerInput>();
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera != null) cameraRestLocalPosition = playerCamera.transform.localPosition;
        }

        private void Update()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            fpsController.SensitivityMultiplier = rules != null
                ? rules.GetVisionImpairmentSensitivityMultiplier(rules.GetEffectivePlayerIndex(playerInput.playerIndex))
                : 1f;
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

            bool isAiming = aimController != null && aimController.IsAiming;

            if (strength <= 0f)
            {
                if (!isAiming) playerCamera.transform.localPosition = cameraRestLocalPosition;
                return;
            }

            Vector3 shakeOffset = Random.insideUnitSphere * (shakeMagnitude * strength);
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
            if (strength <= 0f) return;

            // A true on/off blink (not a smooth pulse) — unscaledTime so the
            // blink rate doesn't slow down along with any hit-stop/pause
            // effects added later.
            bool visible = Mathf.Sin(Time.unscaledTime * flickerFrequency * Mathf.PI * 2f) > 0f;
            if (!visible) return;

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

            GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, strength * maxOverlayAlpha);
            GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
