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

        [Header("Body positioning while aiming")]
        [SerializeField] private float standDistance = 1f;
        [SerializeField] private float tableClearanceMargin = 0.5f;

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
        private CharacterController characterController;
        private InputAction lookAction;
        private InputAction moveAction;
        private InputAction interactAction;
        private InputAction attackAction;

        private Rigidbody currentCueBall;
        private bool isAiming;
        // Read by CharacterAnimationController for an optional "IsAiming"
        // animator bool — same public accessor LocalPoolAimController
        // already has for the same reason.
        public bool IsAiming => isAiming;
        private float aimYaw;
        private float chargedPower;
        private Vector2 contactOffset; // -1..1 on each axis, x=right, y=up
        private Vector3 cameraRestLocalPosition;
        private Quaternion cameraRestLocalRotation;

        private void Awake()
        {
            fpsController = GetComponent<FpsPlayerController>();
            handController = GetComponent<PlayerHandController>();
            characterController = GetComponent<CharacterController>();

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
            if (IsCallingPocketActive()) return true;
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

        // 8-ball call-shot — same lack of per-client turn restriction as
        // ball-in-hand above and for the same reason (no networked player
        // identity yet, see TODO.md): gates only on the shared match state
        // (group cleared, no call made yet), not on whose client this is.
        private static bool IsCallingPocketActive()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules != null && rules.CalledEightBallPocket == null && rules.IsShootingForEightBall(rules.CurrentPlayer);
        }

        private PoolBall placingCueBall;
        // Own flag per top-down interaction (ball placement vs. pocket call
        // below) — see LocalPoolAimController for why sharing one flag
        // between them was a bug (each handler's cleanup would tear down
        // the OTHER one's active view every frame, resetting the pocket
        // selector back to table center before its movement could
        // accumulate).
        private bool ballPlacementActive;
        private bool callPocketActive;
        private Vector3 placementCameraRestLocalPosition;
        private Quaternion placementCameraRestLocalRotation;

        // Top-down view of the table, cue ball slid around with Move,
        // confirmed with Interact — see LocalPoolAimController for why (a
        // first-person look-ray at a small target was too fiddly to use).
        private bool HandleBallInHand()
        {
            if (!IsBallInHandActive())
            {
                if (ballPlacementActive) { EndPlacementView(); ballPlacementActive = false; }
                placingCueBall = null;
                return false;
            }

            if (!ballPlacementActive) { StartPlacementView(); ballPlacementActive = true; }

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
                Vector3 clamped = surface.ClampToPlayArea(rawTarget, placingCueBall.Radius);
                clamped = PoolPocket.AvoidAllPockets(clamped, placingCueBall.Radius);
                placingCueBall.PlaceAt(clamped + Vector3.up * placingCueBall.Radius);
            }

            if (InteractPressedThisFrame())
            {
                placingCueBall.EndBallInHand();
                PoolMatchRules.Instance.ConfirmBallPlaced();
                placingCueBall = null;
                EndPlacementView();
                ballPlacementActive = false;
            }

            return true;
        }

        private PoolPocket highlightedPocket;
        // Edge-detected, not held-down — see LocalPoolAimController for the
        // full reasoning (the continuous drag-a-selector version still felt
        // sluggish even scaled to table size). One tap of a direction jumps
        // straight to the next pocket; true while the stick/keys are past
        // the threshold, so one physical press only fires one jump no
        // matter how long it's held.
        private bool directionHeldLastFrame;
        private const float DirectionPressThreshold = 0.5f;

        // 8-ball call-shot: same top-down table view as ball-in-hand above
        // (reuses StartPlacementView/EndPlacementView as-is). Each tap of
        // Move jumps the highlight to whichever OTHER pocket best matches
        // that on-screen direction from the currently highlighted one — see
        // LocalPoolAimController for the full reasoning (not a fixed cycle
        // order, so "right" always means "the pocket that's actually to the
        // right on screen"). Confirmed with Interact. Consumes the frame so
        // normal aim-entry below doesn't also run.
        private bool HandleCallPocket()
        {
            if (!IsCallingPocketActive())
            {
                if (callPocketActive) EndCallPocketView();
                return false;
            }

            if (!callPocketActive) StartCallPocketView();

            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            bool directionHeldNow = moveInput.magnitude > DirectionPressThreshold;
            if (directionHeldNow && !directionHeldLastFrame)
            {
                Vector3 screenDirection = cameraTransform.right * moveInput.x + cameraTransform.up * moveInput.y;
                StepHighlightedPocketTowards(screenDirection);
            }
            directionHeldLastFrame = directionHeldNow;

            if (InteractPressedThisFrame() && highlightedPocket != null)
            {
                PoolMatchRules.Instance.CallEightBallPocket(highlightedPocket);
                EndCallPocketView();
            }

            return true;
        }

        // A pocket only "counts" as being in the pressed direction once its
        // offset is at least this well aligned with it (1 = dead-on, 0 =
        // perpendicular) — see LocalPoolAimController for the full reasoning.
        private const float MinDirectionAlignment = 0.3f;

        // Picks whichever OTHER pocket best matches screenDirection from the
        // current one — see LocalPoolAimController for why this scores by
        // alignment/distance rather than alignment alone (a corner, its
        // same-side middle pocket, and the opposite corner sit exactly on
        // one line on this table's layout, so direction alone can't tell a
        // near one from a far one that's just as aligned).
        private void StepHighlightedPocketTowards(Vector3 screenDirection)
        {
            if (highlightedPocket == null) return;

            screenDirection.y = 0f;
            if (screenDirection.sqrMagnitude < 0.0001f) return;
            screenDirection.Normalize();

            PoolPocket best = null;
            float bestScore = float.NegativeInfinity;
            foreach (PoolPocket pocket in PoolPocket.Active)
            {
                if (pocket == highlightedPocket) continue;

                Vector3 offset = pocket.transform.position - highlightedPocket.transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < 0.0001f) continue;

                float alignment = Vector3.Dot(offset / distance, screenDirection);
                if (alignment < MinDirectionAlignment) continue;

                float score = alignment / distance;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pocket;
                }
            }

            if (best != null) SetHighlightedPocket(best);
        }

        private void SetHighlightedPocket(PoolPocket pocket)
        {
            if (pocket == highlightedPocket) return;
            highlightedPocket?.SetSelectionHighlight(false);
            highlightedPocket = pocket;
            highlightedPocket?.SetSelectionHighlight(true);
        }

        private static PoolPocket FindNearestPocket(Vector3 position)
        {
            PoolPocket nearest = null;
            float nearestDistanceSqr = float.MaxValue;
            foreach (PoolPocket pocket in PoolPocket.Active)
            {
                float distanceSqr = (pocket.transform.position - position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = pocket;
                }
            }
            return nearest;
        }

        private void StartCallPocketView()
        {
            StartPlacementView();
            callPocketActive = true;
            directionHeldLastFrame = false;

            SetHighlightedPocket(FindNearestPocket(transform.position));
        }

        private void EndCallPocketView()
        {
            highlightedPocket?.SetSelectionHighlight(false);
            highlightedPocket = null;
            EndPlacementView();
            callPocketActive = false;
        }

        // Pure camera plumbing shared by both top-down interactions above —
        // doesn't track which one is active itself (see ballPlacementActive/
        // callPocketActive), just moves the camera and hands FPS control
        // back and forth.
        private void StartPlacementView()
        {
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
            fpsController.enabled = true;
            cameraTransform.localPosition = placementCameraRestLocalPosition;
            cameraTransform.localRotation = placementCameraRestLocalRotation;
        }

        private void Update()
        {
            if (HandleBallInHand()) return;
            if (HandleCallPocket()) return;

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
            // highlightedPocket is only ever non-null while the call-pocket
            // top-down view (HandleCallPocket) is active and has already
            // found a nearest pocket.
            if (highlightedPocket != null)
            {
                GUI.Box(new Rect(Screen.width / 2f - 160f, 20f, 320f, 40f),
                    $"Bille 8 — {highlightedPocket.DescribeLocation()} (Interact pour valider)");
            }

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

        // CharacterController resolves movement incrementally through Move();
        // reassigning transform.position directly while it's enabled fights
        // that internal state on a jump this large (it can report a bogus
        // collision against whatever the capsule swept through on the way),
        // so it's briefly disabled for the teleport — the standard trick for
        // relocating a CharacterController outside of Move(). Only the owner
        // ever runs this (see OnNetworkSpawn), so the resulting position is
        // picked up and replicated by NetworkTransform exactly like normal
        // owner-driven movement.
        private void TeleportBody(Vector3 position, float yaw)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            characterController.enabled = true;
        }

        // Where the body should stand for a shot along aimDirection (which
        // points from the standing spot THROUGH the ball, same convention as
        // UpdateAim's local aimDirection): standDistance behind the ball,
        // pushed back further if that's not already clear of the table
        // itself (DistanceToClearPlayArea) — otherwise a cue ball resting
        // well inside the rails (small standDistance relative to the table)
        // would put the player on top of/inside the table rather than beside
        // it.
        private Vector3 ComputeStandPosition(Vector3 ballPos, Vector3 aimDirection)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            float clearDistance = surface != null
                ? surface.DistanceToClearPlayArea(ballPos, -aimDirection, tableClearanceMargin)
                : 0f;
            float distance = Mathf.Max(standDistance, clearDistance + tableClearanceMargin);

            Vector3 position = ballPos - aimDirection * distance;
            position.y = transform.position.y;
            return position;
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

            // Stand behind the cue ball on the shooting side, facing it —
            // previously only the camera moved to orbit the ball while the
            // body stayed wherever it happened to be within interactRange
            // when Interact was pressed, leaving the visible body completely
            // detached from the shot being lined up (see TODO.md). Re-applied
            // every frame in UpdateAim() too, so the body keeps following
            // the cue around as the aim direction is adjusted, instead of
            // freezing at the angle it happened to have on entry.
            Vector3 aimDirectionOnEnter = toBall.sqrMagnitude > 0.001f ? toBall.normalized : transform.forward;
            TeleportBody(ComputeStandPosition(currentCueBall.position, aimDirectionOnEnter), aimYaw);
        }

        private void ExitAim()
        {
            isAiming = false;
            fpsController.enabled = true;

            // Keep facing the direction of the shot just taken (or backed out
            // of) instead of snapping back to whatever the body happened to
            // be facing before aiming started — aimYaw already tracks
            // wherever the player was last looking while aiming.
            transform.rotation = Quaternion.Euler(0f, aimYaw, 0f);

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

            // Keeps the body standing behind the cue (and facing it) as the
            // aim direction is adjusted — a plain direct set rather than
            // TeleportBody's disable/enable dance, since the per-frame delta
            // here is small (driven by look input) rather than the one big
            // jump EnterAim() makes.
            transform.SetPositionAndRotation(ComputeStandPosition(ballPos, aimDirection), Quaternion.Euler(0f, aimYaw, 0f));

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
