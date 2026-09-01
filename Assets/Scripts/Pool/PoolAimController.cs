using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Pool aiming/shooting: requires holding a Cue (via PlayerHandController) and
    // being near a stopped cue ball. Press Interact to enter aim mode (locks
    // normal movement, camera orbits the ball), Look aims the shot direction,
    // Move shifts the strike point on the cue ball's face (spin — above/below
    // center for topspin/backspin, left/right for side english). Hold Attack to
    // charge power, release to shoot.
    [RequireComponent(typeof(FpsPlayerController))]
    [RequireComponent(typeof(PlayerHandController))]
    public class PoolAimController : NetworkBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float interactRange = 3.5f;

        [Header("Aim camera (reuses the player's own camera)")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float orbitDistance = 0.6f;
        [SerializeField] private float orbitHeight = 0.35f;
        // Mouse delta and gamepad stick values are on completely different
        // scales (a few pixels vs. a normalized -1..1), same reason
        // FpsPlayerController splits its own look sensitivity — reusing one
        // constant for both made gamepad orbiting painfully slow.
        [SerializeField] private float mouseAimTurnSpeed = 0.1f;
        [SerializeField] private float gamepadAimTurnSpeed = 150f;

        [Header("Shot")]
        // These are impulse values (kg*m/s) applied via ForceMode.Impulse, so the
        // resulting cue ball speed = power / ball mass (0.17 kg). Calibrated for a
        // ~0.5-6 m/s speed range, not arbitrary — check PoolBall's mass before
        // retuning these.
        [SerializeField] private float minPower = 0.1f;
        [SerializeField] private float maxPower = 1f;
        [SerializeField] private float chargeSpeed = 0.85f;

        [Header("Spin (strike point on the cue ball)")]
        [SerializeField] private float offsetAdjustSpeed = 1.2f;
        // Fraction of the ball's radius the strike point can be moved off-center.
        // Real cues miscue past ~70-80% of the radius, so this stays under 1.
        [SerializeField] private float maxOffsetFraction = 0.7f;

        [Header("Trajectory preview")]
        [SerializeField] private LineRenderer cueBallPreview;
        [SerializeField] private LineRenderer objectBallPreview;
        // Beyond this range, nothing is shown at all — a truncated line pointing
        // into empty space is more confusing than no line.
        [SerializeField] private float previewMaxDistance = 3f;
        [SerializeField] private float objectBallPreviewLength = 0.4f;
        [SerializeField] private Color cueBallPreviewColor = Color.white;
        [SerializeField] private Color objectBallPreviewColor = Color.yellow;

        [Header("Held cue positioning while aiming")]
        [SerializeField] private float cueTipGap = 0.05f; // resting distance from the ball surface
        [SerializeField] private float cuePullbackPerPower = 0.25f; // visual charge feedback

        [Header("Ball-in-hand placement (top-down view)")]
        // Plain manual height above the table — set this directly in the
        // Inspector until the framing looks right for your setup (an
        // auto-computed height from FOV/aspect was tried here and didn't
        // actually give predictable control over the shot).
        [SerializeField] private float placementCameraHeight = 3.5f;
        [SerializeField] private float placementMoveSpeed = 1.2f;

        [Header("Input (whole asset — actions looked up by name)")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string attackActionName = "Attack";

        private FpsPlayerController fpsController;
        private PlayerHandController handController;
        private InputAction lookAction;
        private InputAction moveAction;
        private InputAction interactAction;
        private InputAction attackAction;

        private Rigidbody currentCueBall;
        private bool isAiming;
        private float aimYaw;
        private float chargedPower;
        private Vector2 contactOffset; // -1..1 on each axis, x=right, y=up
        private Vector3 cameraRestLocalPosition;
        private Quaternion cameraRestLocalRotation;

        private void Awake()
        {
            fpsController = GetComponent<FpsPlayerController>();
            handController = GetComponent<PlayerHandController>();

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
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

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
            if (!IsOwner) return;

            lookAction.Enable();
            moveAction.Enable();
            interactAction.Enable();
            attackAction.Enable();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            interactAction.Disable();
            attackAction.Disable();
        }

        // Called by PlayerHandController: while true, it leaves Interact alone
        // instead of using it for pickup/drop.
        public bool WantsInteractThisFrame(Grabbable heldObject)
        {
            if (isAiming) return true;
            if (IsBallInHandActive()) return true;
            return IsCue(heldObject) && FindNearbyCueBall() != null;
        }

        private static bool IsCue(Grabbable heldObject)
        {
            return heldObject != null && heldObject.TryGetComponent(out Cue _);
        }

        // Ball physics (and PoolMatchRules with it) aren't networked yet — see
        // TODO.md — so unlike the offline controller this can't check whose
        // turn it actually is (no stable per-client player index yet). It only
        // gates on whether a foul happened at all, same as everyone else
        // running their own local copy of the match.
        private static bool IsBallInHandActive()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules != null && rules.BallInHand;
        }

        private PoolBall placingCueBall;
        private bool placementViewActive;
        private Vector3 placementCameraRestLocalPosition;
        private Quaternion placementCameraRestLocalRotation;

        // Top-down view of the table, cue ball slid around with Move,
        // confirmed with Interact — see LocalPoolAimController for why (a
        // first-person look-ray at a small target was too fiddly to use).
        private bool HandleBallInHand()
        {
            if (!IsBallInHandActive())
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

        // Placement Camera Height is tuned by hand for a normal full-window
        // viewport — a narrower viewport (split-screen, if this ever runs
        // that way) shrinks the horizontal FOV and makes the SAME height show
        // less of the table lengthwise. Scaling by how much the current
        // viewport's aspect differs from the full window's keeps the
        // hand-tuned value correct regardless of viewport shape.
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
                if (currentCueBall != null && IsCue(handController.HeldObject) && InteractPressedThisFrame())
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
            float dotY = y + size / 2f - contactOffset.y * (size / 2f - 8f); // screen Y is inverted
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(dotX - 4, dotY - 4, 8, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // "Interact" has a Hold interaction configured at the action level in the
        // default asset — depending on how that resolves, either the Started or the
        // Performed phase transition might be what actually fires on a tap, so we
        // don't rely on a single WasXThisFrame() call to catch it reliably.
        private bool InteractPressedThisFrame()
        {
            return interactAction.WasPressedThisFrame() || interactAction.WasPerformedThisFrame();
        }

        // Balls in motion aren't a valid aim target — matches the sleep threshold
        // used by PoolBall's own friction model, so "stopped" means the same
        // thing here as it does there.
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

            cameraRestLocalPosition = cameraTransform.localPosition;
            cameraRestLocalRotation = cameraTransform.localRotation;

            // Start aiming in the direction from the player THROUGH the ball
            // (not the reverse) — the camera is then placed behind the ball on
            // the player's side, looking the way the shot will actually travel.
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

            cameraTransform.localPosition = cameraRestLocalPosition;
            cameraTransform.localRotation = cameraRestLocalRotation;

            if (cueBallPreview != null) cueBallPreview.enabled = false;
            if (objectBallPreview != null) objectBallPreview.enabled = false;

            // Snap the held cue back to its normal carried pose.
            Grabbable cue = handController.HeldObject;
            if (cue != null)
                cue.transform.SetLocalPositionAndRotation(cue.HoldLocalPosition, cue.HoldLocalRotation);
        }

        private void UpdateAim()
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            bool isGamepad = lookAction.activeControl?.device is Gamepad;
            float turnSpeed = isGamepad ? gamepadAimTurnSpeed * Time.deltaTime : mouseAimTurnSpeed;
            aimYaw += look.x * turnSpeed;

            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            contactOffset += moveInput * offsetAdjustSpeed * Time.deltaTime;
            if (contactOffset.magnitude > 1f) contactOffset = contactOffset.normalized;

            Vector3 aimDirection = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
            Vector3 ballPos = currentCueBall.position;

            cameraTransform.position = ballPos - aimDirection * orbitDistance + Vector3.up * orbitHeight;
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
            Grabbable cue = handController.HeldObject;
            if (cue == null) return;

            float ballRadius = currentCueBall.GetComponent<SphereCollider>().radius * currentCueBall.transform.lossyScale.x;
            float pullback = chargedPower * cuePullbackPerPower;
            Vector3 tip = ballPos - direction * (ballRadius + cueTipGap + pullback);

            // transform.position is the cue's PIVOT, which sits at its center —
            // not its tip. To make the tip actually touch that point, the pivot
            // has to sit half the cue's length further back along the aim
            // direction. Unity's cylinder primitive is 2 units tall in local
            // space, hence *2 to get the real world-space length from scale.
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

            // For an equal-mass elastic collision, the struck ball's initial
            // direction is along the line connecting the two ball centers at the
            // moment of contact — not the cue ball's incoming direction.
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
            // No stable per-client player index online yet (see TODO.md) —
            // consumes whichever player's multiplier CurrentPlayer says is up,
            // same limitation already accepted for ball-in-hand/power activation.
            if (rules != null) power *= rules.ConsumeShotPowerMultiplier(rules.CurrentPlayer);

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

                // Torque from an impulse applied off-center at the strike point —
                // above center (+y) gives topspin/follow, below gives backspin/draw,
                // left/right gives side spin (english). Unity's own inertia tensor
                // handles the mass/shape math, we just supply the torque impulse.
                Vector3 angularImpulse = Vector3.Cross(strikeOffset, impulse);
                currentCueBall.AddTorque(angularImpulse, ForceMode.Impulse);
            }

            chargedPower = 0f;
            ExitAim();
        }
    }
}
