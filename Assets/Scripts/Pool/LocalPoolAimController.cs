using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Offline counterpart to PoolAimController — identical aiming/shooting
    // logic (camera orbit, strike-point spin, charge/release power, trajectory
    // preview, held-cue requirement), but a plain MonoBehaviour reading its
    // actions from this player's own PlayerInput instance. No networking.
    [RequireComponent(typeof(LocalFpsPlayerController))]
    [RequireComponent(typeof(LocalPlayerHandController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPoolAimController : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float interactRange = 3.5f;

        [Header("Aim camera (reuses the player's own camera)")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float orbitDistance = 0.6f;
        [SerializeField] private float orbitHeight = 0.35f;
        // Mouse delta and gamepad stick values are on completely different
        // scales (a few pixels vs. a normalized -1..1), same reason
        // LocalFpsPlayerController splits its own look sensitivity — reusing
        // one constant for both made gamepad orbiting painfully slow.
        [SerializeField] private float mouseAimTurnSpeed = 0.1f;
        [SerializeField] private float gamepadAimTurnSpeed = 150f;

        [Header("Shot")]
        [SerializeField] private float minPower = 0.1f;
        [SerializeField] private float maxPower = 1f;
        [SerializeField] private float chargeSpeed = 0.85f;
        // Charging zoom distance lives in PoolScreenJuiceSettings (shared
        // with LocalPoolPowerEffectReceiver's charge shake — same "how does
        // charging a shot feel" moment), not a local field here.

        [Header("Spin (strike point on the cue ball)")]
        [SerializeField] private float offsetAdjustSpeed = 1.2f;
        [SerializeField] private float maxOffsetFraction = 0.7f;

        [Header("Trajectory preview")]
        [SerializeField] private LineRenderer cueBallPreview;
        [SerializeField] private LineRenderer objectBallPreview;
        [SerializeField] private float previewMaxDistance = 3f;
        [SerializeField] private float objectBallPreviewLength = 0.4f;
        [SerializeField] private Color cueBallPreviewColor = Color.white;
        [SerializeField] private Color objectBallPreviewColor = Color.yellow;

        [Header("Held cue positioning while aiming")]
        [SerializeField] private float cueTipGap = 0.05f;
        [SerializeField] private float cuePullbackPerPower = 0.25f;

        [Header("Ball-in-hand placement (top-down view)")]
        // Plain manual height above the table — set this directly in the
        // Inspector until the framing looks right for your setup (an
        // auto-computed height from FOV/aspect was tried here and didn't
        // actually give predictable control over the shot).
        [SerializeField] private float placementCameraHeight = 3.5f;
        [SerializeField] private float placementMoveSpeed = 1.2f;

        [Header("Input")]
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string attackActionName = "Attack";

        private static PoolScreenJuiceSettings juiceSettings;

        private LocalFpsPlayerController fpsController;
        private LocalPlayerHandController handController;
        private PlayerInput playerInput;
        private InputAction lookAction;
        private InputAction moveAction;
        private InputAction interactAction;
        private InputAction attackAction;

        private Rigidbody currentCueBall;
        private bool isAiming;
        // While aiming, cameraTransform's WORLD position/rotation are driven
        // directly here (camera orbit) every frame — anything else that also
        // wants to nudge the camera (e.g. a power's screen shake) needs to
        // know this, so it can add to the current position instead of
        // fighting/overwriting the orbit.
        public bool IsAiming => isAiming;
        // 0 while not charging, up to 1 at maxPower — read by
        // LocalPoolPowerEffectReceiver to grow the charge shake in step with
        // how loaded the shot is, and used locally for the charging zoom.
        public float ChargeFraction => maxPower > 0f ? Mathf.Clamp01(chargedPower / maxPower) : 0f;
        private float aimYaw;
        private float chargedPower;
        private Vector2 contactOffset;
        private Vector3 cameraRestLocalPosition;
        private Quaternion cameraRestLocalRotation;

        private void Awake()
        {
            fpsController = GetComponent<LocalFpsPlayerController>();
            handController = GetComponent<LocalPlayerHandController>();
            playerInput = GetComponent<PlayerInput>();

            if (juiceSettings == null)
            {
                juiceSettings = Resources.Load<PoolScreenJuiceSettings>("PoolScreenJuiceSettings");
                if (juiceSettings == null)
                {
                    Debug.LogWarning("PoolScreenJuiceSettings asset not found in Assets/Resources — using fallback defaults. Run Tools > Pool > Ensure Config Assets Exist to create it.");
                    juiceSettings = ScriptableObject.CreateInstance<PoolScreenJuiceSettings>();
                }
            }

            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            lookAction = map.FindAction(lookActionName, throwIfNotFound: true);
            moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
            attackAction = map.FindAction(attackActionName, throwIfNotFound: true);

            if (cueBallPreview != null)
            {
                cueBallPreview.enabled = false;
                cueBallPreview.startColor = cueBallPreview.endColor = cueBallPreviewColor;
            }
            if (objectBallPreview != null)
            {
                objectBallPreview.enabled = false;
                objectBallPreview.startColor = objectBallPreview.endColor = objectBallPreviewColor;
            }
        }

        public bool WantsInteractThisFrame(LocalGrabbable heldObject)
        {
            if (isAiming) return true;
            if (IsMyTurnToPlaceBall()) return true;
            return IsCue(heldObject) && FindNearbyCueBall() != null;
        }

        private static bool IsCue(LocalGrabbable heldObject)
        {
            return heldObject != null && heldObject.TryGetComponent(out Cue _);
        }

        // Whether it's this player's turn to shoot at all — used to stop the
        // player who isn't up from entering aim mode. Cue pickup itself is
        // gated the same way in LocalPlayerHandController.
        private bool CanShootNow()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules == null || rules.CanPlayerShoot(playerInput.playerIndex);
        }

        private bool IsMyTurnToPlaceBall()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules != null && rules.BallInHand && rules.CanPlayerShoot(playerInput.playerIndex);
        }

        private PoolBall placingCueBall;
        private bool placementViewActive;
        // Same reason as IsAiming above — the ball-in-hand top-down view also
        // drives cameraTransform's WORLD position/rotation directly every
        // frame, a THIRD state distinct from both aiming and normal FPS view.
        public bool IsPlacementViewActive => placementViewActive;
        private Vector3 placementCameraRestLocalPosition;
        private Quaternion placementCameraRestLocalRotation;

        // Ball-in-hand: after a foul, the player who now has the turn switches
        // to a top-down view of the whole table and slides the cue ball around
        // with Move, confirming with Interact. (A first-person look-ray to pick
        // a spot on the felt was tried first — too fiddly to aim precisely at a
        // small target while also just trying to look around normally.)
        // Consumes the frame (returns true) whenever in progress, so normal
        // aim-entry/exit below doesn't also run.
        private bool HandleBallInHand()
        {
            if (!IsMyTurnToPlaceBall())
            {
                if (placementViewActive) EndPlacementView();
                placingCueBall = null;
                return false;
            }

            if (!placementViewActive) StartPlacementView();

            if (placingCueBall == null)
            {
                placingCueBall = PoolBall.FindCueBall();
                if (placingCueBall == null) return true;
            }

            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface != null)
            {
                // The camera's own right/up (not the table's — the table can
                // be rotated to line up with a custom asset, but the top-down
                // camera itself always looks straight down the same way), so
                // Move always matches what's shown on screen regardless of
                // how the table itself is oriented.
                Vector2 moveInput = moveAction.ReadValue<Vector2>();
                Vector3 delta = (cameraTransform.right * moveInput.x + cameraTransform.up * moveInput.y)
                    * placementMoveSpeed * Time.deltaTime;
                Vector3 rawTarget = placingCueBall.transform.position + delta;
                Vector3 clamped = surface.ClampToPlayArea(rawTarget, placingCueBall.Radius) + Vector3.up * placingCueBall.Radius;
                placingCueBall.PlaceAt(clamped);
            }

            if (InteractPressedThisFrame())
            {
                placingCueBall.EndBallInHand();
                PoolMatchRules.Instance.ConfirmBallPlaced();
                placingCueBall = null;
                EndPlacementView();
            }

            return true;
        }

        private void StartPlacementView()
        {
            placementViewActive = true;
            fpsController.enabled = false;

            placementCameraRestLocalPosition = cameraTransform.localPosition;
            placementCameraRestLocalRotation = cameraTransform.localRotation;

            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 center = surface != null ? surface.transform.position : transform.position;
            cameraTransform.position = center + Vector3.up * PlacementHeightForCurrentViewport();
            cameraTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Placement Camera Height is tuned by hand for full-screen (solo) —
        // split-screen narrows each player's viewport (typically side-by-side,
        // so a narrower width/height ratio), which shrinks the horizontal FOV
        // and makes the SAME height show less of the table lengthwise, i.e.
        // "too close" even though nothing changed except the viewport shape.
        // Scaling by how much the current viewport's aspect differs from the
        // full window's keeps the hand-tuned solo value correct everywhere,
        // instead of needing a separate number per screen layout.
        private float PlacementHeightForCurrentViewport()
        {
            Camera cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (cam == null || cam.aspect <= 0f || Screen.height <= 0) return placementCameraHeight;

            float fullScreenAspect = Screen.width / (float)Screen.height;
            return placementCameraHeight * (fullScreenAspect / cam.aspect);
        }

        private void EndPlacementView()
        {
            placementViewActive = false;
            fpsController.enabled = true;
            cameraTransform.localPosition = placementCameraRestLocalPosition;
            cameraTransform.localRotation = placementCameraRestLocalRotation;
        }

        private void Update()
        {
            if (HandleBallInHand()) return;

            if (!isAiming)
            {
                currentCueBall = FindNearbyCueBall();
                if (currentCueBall != null && IsCue(handController.HeldObject) && CanShootNow() && InteractPressedThisFrame())
                    EnterAim();
                return;
            }

            if (currentCueBall == null || !IsCue(handController.HeldObject))
            {
                ExitAim();
                return;
            }

            if (InteractPressedThisFrame())
            {
                ExitAim();
                return;
            }

            UpdateAim();
        }

        private void OnGUI()
        {
            if (!isAiming) return;

            const int size = 90;
            int x = Screen.width - size - 20;
            int y = Screen.height - size - 20;

            GUI.Box(new Rect(x, y, size, size), "Strike point");

            float dotX = x + size / 2f + contactOffset.x * (size / 2f - 8f);
            float dotY = y + size / 2f - contactOffset.y * (size / 2f - 8f);
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(dotX - 4, dotY - 4, 8, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private bool InteractPressedThisFrame()
        {
            return interactAction.WasPressedThisFrame() || interactAction.WasPerformedThisFrame();
        }

        private const float MaxSpeedToAim = 0.05f;

        private Rigidbody FindNearbyCueBall()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out PoolBall ball) && ball.IsCueBall && ball.Rigidbody.linearVelocity.magnitude < MaxSpeedToAim)
                    return ball.Rigidbody;
            }
            return null;
        }

        private void EnterAim()
        {
            isAiming = true;
            chargedPower = 0f;
            contactOffset = Vector2.zero;
            fpsController.enabled = false;

            // Entering aim mode already only happens on this player's own
            // turn (gated by CanShootNow() before EnterAim() is ever called)
            // — exactly the moment a queued VisionImpairPower against them
            // should actually start counting down, not whenever it happened
            // to be activated. GetEffectivePlayerIndex so hot-seat solo (one
            // PlayerInput playing both sides) resolves to whichever side is
            // actually up right now, not permanently slot 0.
            PoolMatchRules rulesForPower = PoolMatchRules.Instance;
            if (rulesForPower != null)
            {
                int effectivePlayer = rulesForPower.GetEffectivePlayerIndex(playerInput.playerIndex);
                rulesForPower.ConsumePendingVisionImpair(effectivePlayer);
                rulesForPower.ConsumePendingInvertedControls(effectivePlayer);
            }

            cameraRestLocalPosition = cameraTransform.localPosition;
            cameraRestLocalRotation = cameraTransform.localRotation;

            Vector3 toBall = currentCueBall.position - transform.position;
            toBall.y = 0f;
            aimYaw = toBall.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toBall).eulerAngles.y
                : transform.eulerAngles.y;
        }

        private void ExitAim()
        {
            isAiming = false;
            fpsController.enabled = true;

            // Mirrors the ConsumePending* calls in EnterAim() — any debuff
            // that started when this player entered aim mode ends here when
            // they leave it, whether that's from taking the shot or backing
            // out.
            PoolMatchRules rulesForPower = PoolMatchRules.Instance;
            if (rulesForPower != null)
            {
                int effectivePlayer = rulesForPower.GetEffectivePlayerIndex(playerInput.playerIndex);
                rulesForPower.EndVisionImpair(effectivePlayer);
                rulesForPower.EndInvertedControls(effectivePlayer);
            }

            cameraTransform.localPosition = cameraRestLocalPosition;
            cameraTransform.localRotation = cameraRestLocalRotation;

            if (cueBallPreview != null) cueBallPreview.enabled = false;
            if (objectBallPreview != null) objectBallPreview.enabled = false;

            LocalGrabbable cue = handController.HeldObject;
            if (cue != null)
                cue.transform.SetLocalPositionAndRotation(cue.HoldLocalPosition, cue.HoldLocalRotation);
        }

        private void UpdateAim()
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            bool isGamepad = lookAction.activeControl?.device is Gamepad;
            float turnSpeed = isGamepad ? gamepadAimTurnSpeed * Time.deltaTime : mouseAimTurnSpeed;

            // InvertedControlsPower — same look inversion as the normal FPS
            // view (LocalFpsPlayerController.InvertLook), applied directly
            // here since the aim orbit reads its own look input rather than
            // going through that controller (disabled while aiming).
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null && rules.IsControlsInverted(rules.GetEffectivePlayerIndex(playerInput.playerIndex)))
                look.x = -look.x;

            aimYaw += look.x * turnSpeed;

            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            contactOffset += moveInput * offsetAdjustSpeed * Time.deltaTime;
            if (contactOffset.magnitude > 1f) contactOffset = contactOffset.normalized;

            Vector3 aimDirection = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
            Vector3 ballPos = currentCueBall.position;

            // Creeps the camera in toward the cue tip as the shot charges —
            // a "charging zoom", on top of the shake LocalPoolPowerEffectReceiver
            // layers on separately (reads ChargeFraction).
            float currentOrbitDistance = orbitDistance - juiceSettings.chargeZoomDistance * ChargeFraction;
            cameraTransform.position = ballPos - aimDirection * currentOrbitDistance + Vector3.up * orbitHeight;
            cameraTransform.LookAt(ballPos + Vector3.up * (orbitHeight * 0.3f));

            UpdatePreview(ballPos, aimDirection);
            UpdateCueVisual(ballPos, aimDirection);

            if (attackAction.IsPressed())
                chargedPower = Mathf.Min(chargedPower + chargeSpeed * Time.deltaTime, maxPower);
            else if (chargedPower > 0f)
                Shoot(aimDirection);
        }

        private void UpdateCueVisual(Vector3 ballPos, Vector3 direction)
        {
            LocalGrabbable cue = handController.HeldObject;
            if (cue == null) return;

            float ballRadius = currentCueBall.GetComponent<SphereCollider>().radius * currentCueBall.transform.lossyScale.x;
            float pullback = chargedPower * cuePullbackPerPower;
            Vector3 tip = ballPos - direction * (ballRadius + cueTipGap + pullback);

            float cueWorldLength = cue.transform.lossyScale.y * 2f;
            cue.transform.position = tip - direction * (cueWorldLength / 2f);
            cue.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        }

        private void UpdatePreview(Vector3 ballPos, Vector3 direction)
        {
            if (cueBallPreview == null) return;

            float cueRadiusWorld = currentCueBall.GetComponent<SphereCollider>().radius * currentCueBall.transform.lossyScale.x;

            // Physics.SphereCast never reports a hit against a collider the
            // sphere already overlaps AT THE START of the sweep (documented
            // Unity behaviour) — starting exactly at the cue ball's own center
            // with its own radius meant any ball or rail already touching it
            // got silently skipped, making the line vanish depending on aim
            // direction whenever the cue ball was resting right up against
            // something. Starting just past its own surface avoids that.
            Vector3 castOrigin = ballPos + direction * (cueRadiusWorld + 0.001f);
            float castDistance = Mathf.Max(0f, previewMaxDistance - cueRadiusWorld);

            // Always show the cue ball's line, all the way to previewMaxDistance
            // if nothing was hit (aiming through a pocket gap, or any other case
            // that isn't a real obstacle) — hiding it entirely whenever nothing
            // was hit within range made it disappear in totally normal aiming
            // situations, which read as "broken" rather than "nothing there".
            bool didHit = Physics.SphereCast(castOrigin, cueRadiusWorld, direction, out RaycastHit hit, castDistance);
            Vector3 cueBallCenterAtContact = didHit
                ? castOrigin + direction * hit.distance
                : castOrigin + direction * castDistance;

            cueBallPreview.enabled = true;
            cueBallPreview.positionCount = 2;
            cueBallPreview.SetPosition(0, ballPos);
            cueBallPreview.SetPosition(1, cueBallCenterAtContact);

            if (didHit && objectBallPreview != null && hit.rigidbody != null &&
                hit.rigidbody.TryGetComponent(out PoolBall objectBall) && !objectBall.IsCueBall)
            {
                Vector3 objectBallCenter = hit.rigidbody.position;
                Vector3 objectDirection = (objectBallCenter - cueBallCenterAtContact).normalized;

                objectBallPreview.enabled = true;
                objectBallPreview.positionCount = 2;
                objectBallPreview.SetPosition(0, objectBallCenter);
                objectBallPreview.SetPosition(1, objectBallCenter + objectDirection * objectBallPreviewLength);
            }
            else if (objectBallPreview != null)
            {
                objectBallPreview.enabled = false;
            }
        }

        private void Shoot(Vector3 direction)
        {
            float power = Mathf.Max(chargedPower, minPower);

            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null) power *= rules.ConsumeShotPowerMultiplier(playerInput.playerIndex);

            Vector3 impulse = direction * power;

            if (currentCueBall.TryGetComponent(out PoolBall cueBallComponent))
                cueBallComponent.ArmContactTracking();
            rules?.NotifyShotFired();

            currentCueBall.AddForce(impulse, ForceMode.Impulse);

            if (contactOffset.sqrMagnitude > 0.0001f)
            {
                float radius = currentCueBall.GetComponent<SphereCollider>().radius * currentCueBall.transform.lossyScale.x;
                Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 strikeOffset = (right * contactOffset.x + Vector3.up * contactOffset.y) * (maxOffsetFraction * radius);

                Vector3 angularImpulse = Vector3.Cross(strikeOffset, impulse);
                currentCueBall.AddTorque(angularImpulse, ForceMode.Impulse);
            }

            chargedPower = 0f;
            ExitAim();

            // Called after ExitAim(), not before: by then the camera is back
            // to its normal FPS local-position control (ExitAim already
            // reset it), so the recoil kick doesn't have to fight the aim
            // orbit's own per-frame world-position override.
            GetComponent<LocalPoolPowerEffectReceiver>()?.PlayShotFeedback();
        }
    }
}
