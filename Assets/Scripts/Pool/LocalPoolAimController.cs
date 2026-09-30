using RootMotion.FinalIK;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Pool aiming/shooting: requires holding the Cue and being near a
    // stopped cue ball. Interact enters/exits aim mode (camera orbits the
    // ball, body placed behind the cue), Look aims, Move shifts the strike
    // point (spin), hold Attack to charge and release to shoot. Also owns
    // the top-down ball-in-hand placement and 8-ball pocket call views.
    // Reads its actions from this player's own PlayerInput instance.
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

        [Header("Held cue and body while aiming")]
        // Moved onto the cue ball every frame while aiming — what the head
        // looks at then (see lookAtIK below).
        [SerializeField] private Transform aimIKTarget;

        // The held cue is never moved or rotated while aiming: its
        // InteractionTargets (the grip points) are children of it, so
        // FBBIK would drag the hands — and through them the whole body —
        // after every change (that's what made the character fall apart
        // whenever the cue was repositioned). Instead the BODY is placed and
        // turned each frame so the cue, exactly as it's held, points along
        // the shot line at the ball (see ComputeAimBodyPose). This is the
        // local axis of the cue that points along its length, tip-first —
        // if the body ends up facing the wrong way, flip its sign or try
        // another axis: one of a handful of choices, not a value to guess.
        [SerializeField] private Vector3 aimHoldAxis = Vector3.forward;

        // Of that extra reach, how much the held cue (grip points, so the
        // hands) is moved forward along its own axis, like arms stretching
        // out over the rail — the rest is left to the mesh sliding through
        // the hands. Kept small: FBBIK pulls the body toward its hand
        // targets, so a big shift is what tore the character apart earlier.
        [SerializeField] private float maxHandShift = 0.4f;

        // Where the cue sits relative to the body ONLY while aiming, added on
        // top of its normal held pose (Pivot/Hold Point, untouched), in the
        // body's own axes: x = right, y = up, z = forward. Re-read every
        // frame, so drag it live while aiming; the body re-places itself
        // around whatever this makes of the cue. Left at zero = same spot as
        // when just carrying it.
        [SerializeField] private Vector3 aimCueOffset;

        public float AimReachExtra { get; private set; }
        public float AimHandShift { get; private set; }

        // True while the ball is further than the arms can reach from outside
        // the table (needs more than maxHandShift): the aiming pose is kept
        // (arms stretched as far as they go, cue tilted at the ball) but the
        // tip doesn't reach it and no shot can be taken until the player
        // orbits to an angle that's within reach.
        public bool IsOutOfReach => isAiming && AimReachExtra > maxHandShift;

        // How far CueChargeSlide should slide the mesh through the hands:
        // whatever reach the hands' own shift didn't cover, or nothing at
        // all while out of reach (the cue isn't extended then).
        public float AimMeshReach => IsOutOfReach ? 0f : Mathf.Max(0f, AimReachExtra - AimHandShift);

        // Distance from the cue's origin (mid-cue pivot) to the ball at which
        // the TIP touches it: measured from the mesh (CueChargeSlide.TipDistance)
        // plus the ball's radius, or plain standDistance if that isn't
        // available. Replaces hand-tuning standDistance for this.
        private float aimBaseDistance;

        // Gap between the cue tip and the ball's surface at rest (metres):
        // 0 = touching, positive = a little way back from it.
        [SerializeField] private float aimTipGap = 0.02f;

        // While aiming, the held cue is tilted (about its own origin, not
        // moved) so its tip points at the ball instead of keeping the
        // level pose it's carried in — otherwise it passes over the ball
        // when the hands are higher than the table. Capped in degrees so
        // the grips (and so the hands) never swing far. Height of the hands
        // themselves is Aim Cue Offset's y.
        [SerializeField] private bool tiltCueToBall = true;
        [SerializeField] private float maxCueTilt = 25f;

        private Vector3 cueRestLocalPosition;
        private Quaternion cueRestLocalRotation;

        private Vector3 cueLocalOrigin;
        private Vector3 cueLocalTipDirection;
        private bool hasCueLine;

        // The head/spine turn toward the ball while aiming, via FinalIK's
        // Look At IK — assign its own Target field to this same aimIKTarget
        // in the Inspector (it already tracks the ball every frame, see
        // UpdateAimIKTarget below) rather than wiring a second target.
        // Doesn't fight InteractionSystem's own internal look-at: that one
        // only runs briefly during the cue's pickup reach, long over by the
        // time aiming (and this) starts.
        [SerializeField] private LookAtIK lookAtIK;

        // Existing bend goal Transforms (already assigned on Full Body Biped
        // IK's own arm chains, with their own weight already tuned there —
        // untouched by this script) — repositioned, not swapped, between an
        // idle local offset and an aiming one. Left empty = not moved.
        [SerializeField] private Transform leftArmBendGoal;
        [SerializeField] private Vector3 leftArmBendGoalAimLocalOffset;
        [SerializeField] private Transform rightArmBendGoal;
        [SerializeField] private Vector3 rightArmBendGoalAimLocalOffset;

        private Vector3 leftArmBendGoalRestLocalPosition;
        private Vector3 rightArmBendGoalRestLocalPosition;

        // What the head follows when NOT aiming: the child of CM_FPS, so it
        // tracks where the camera looks. While aiming, Look At IK's target
        // is swapped to aimIKTarget (on the cue ball) instead, and back
        // again on exit. Leave empty to have no head look outside aiming.
        [SerializeField] private Transform normalLookTarget;

        private void Start()
        {
            EnableHeadLook(false);
        }

        // Extra yaw (degrees) added to the body on top of whatever lines the
        // held cue up with the shot (see ComputeAimBodyPose). The cue stays
        // on the shot line either way — the body is placed around it — so
        // this only changes how the body stands relative to the cue (e.g.
        // side-on, like a real pool stance).
        [SerializeField] private float bodyYawOffsetWhileAiming = 90f;

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

        [Header("Input")]
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string attackActionName = "Attack";

        private static PoolScreenJuiceSettings juiceSettings;

        private LocalFpsPlayerController fpsController;
        private LocalPlayerHandController handController;
        // Optional: aim can't start mid-swing (EnterAim snapshots the cue's
        // held pose, which the swing is temporarily moving).
        private LocalCueMelee cueMelee;
        private LocalUnarmedMelee unarmedMelee;
        private CharacterController characterController;
        private PlayerInput playerInput;
        // CinemachineBrain overwrites the actual camera's Transform every
        // LateUpdate to match whichever vcam (CM_FPS) currently has
        // priority — disabling fpsController alone only stops its look
        // INPUT handling, not this, so without also disabling the Brain
        // itself, cameraTransform's manual orbit position/rotation (set in
        // Update(), i.e. before CinemachineBrain's own LateUpdate) gets
        // silently reverted every single frame during aim.
        private CinemachineBrain cinemachineBrain;
        private InputAction lookAction;
        private InputAction moveAction;
        private InputAction interactAction;
        private InputAction attackAction;

        private PoolBall currentCueBall;
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
            cueMelee = GetComponent<LocalCueMelee>();
            unarmedMelee = GetComponent<LocalUnarmedMelee>();
            cinemachineBrain = GetComponentInChildren<CinemachineBrain>(true);
            characterController = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();

            if (juiceSettings == null)
                juiceSettings = PoolSettingsLoader.LoadOrDefault<PoolScreenJuiceSettings>("PoolScreenJuiceSettings");

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
            if (IsMyTurnToCallPocket()) return true;
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

        // 8-ball call-shot (see EightBallRuleSet/PoolMatchRules): true once
        // this player's group is cleared and they haven't already called a
        // pocket for their next attempt at the 8.
        private bool IsMyTurnToCallPocket()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.CanPlayerShoot(playerInput.playerIndex) || rules.CalledEightBallPocket != null)
                return false;

            int effectivePlayer = rules.GetEffectivePlayerIndex(playerInput.playerIndex);
            return rules.IsShootingForEightBall(effectivePlayer);
        }

        private PoolBall placingCueBall;
        // Own flag per top-down interaction (ball placement vs. pocket call
        // below) rather than one shared bool — they used to share
        // placementViewActive, which meant whichever handler ran second in
        // Update() (HandleCallPocket, after HandleBallInHand) would see the
        // OTHER one's flag already true, conclude "not my turn" (its own
        // eligibility check is unrelated), and call EndPlacementView() on
        // it — torn down and immediately restarted from scratch every
        // single frame, resetting the pocket selector back to table center
        // each time before its movement could ever accumulate.
        private bool ballPlacementActive;
        private bool callPocketActive;
        // Same reason as IsAiming above — either top-down view also drives
        // cameraTransform's WORLD position/rotation directly every frame, a
        // THIRD state distinct from both aiming and normal FPS view.
        public bool IsPlacementViewActive => ballPlacementActive || callPocketActive;
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
        // Edge-detected, not held-down — a single tap of a direction jumps
        // straight to the next pocket, no crossing distance/speed involved
        // at all (previous continuous drag-a-selector version still felt
        // sluggish even once its speed scaled with table size). True while
        // the stick/keys are past the threshold, so one physical press only
        // fires one jump no matter how long it's held.
        private bool directionHeldLastFrame;
        private const float DirectionPressThreshold = 0.5f;

        // 8-ball call-shot: same top-down table view as ball-in-hand above
        // (reuses StartPlacementView/EndPlacementView as-is — they're
        // already generic "look straight down at the table" plumbing, not
        // ball-specific). Each tap of Move jumps the highlight to whichever
        // OTHER pocket best matches that on-screen direction from the
        // currently highlighted one — not a fixed cycle order, so "right"
        // always means "the pocket that's actually to the right on screen"
        // regardless of which pocket happens to be highlighted right now.
        // Confirmed with Interact. Consumes the frame (like HandleBallInHand)
        // so normal aim-entry below doesn't also run.
        private bool HandleCallPocket()
        {
            if (!IsMyTurnToCallPocket())
            {
                if (callPocketActive) EndCallPocketView();
                return false;
            }

            if (!callPocketActive) StartCallPocketView();

            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            bool directionHeldNow = moveInput.magnitude > DirectionPressThreshold;
            if (directionHeldNow && !directionHeldLastFrame)
            {
                // Same right/up = world X/Z convention as the camera orbit
                // and ball-in-hand placement above — the top-down camera's
                // own rotation never has any yaw, so these stay fixed
                // regardless of which way the player's body is facing.
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
        // perpendicular) — screens out anything roughly sideways/behind so
        // a press never jumps somewhere unrelated just because it was the
        // least-misaligned option on the table.
        private const float MinDirectionAlignment = 0.3f;

        // Picks whichever OTHER pocket best matches screenDirection from the
        // current one. On this table's layout a corner, the middle pocket on
        // its same side, and the opposite corner all sit exactly on one
        // line — direction alone can't tell them apart (identical alignment
        // score), so scoring by alignment/distance instead makes a NEAR,
        // well-aligned pocket win over a FAR one that happens to be exactly
        // as aligned (the middle pocket instead of skipping straight past it
        // to the far corner).
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

            // Start highlighted on whichever pocket is already nearest to
            // the player, rather than an arbitrary one.
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
            if (cinemachineBrain != null) cinemachineBrain.enabled = false;

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

        private void EndPlacementView(bool returnControlToPlayer = true)
        {
            if (returnControlToPlayer) fpsController.enabled = true;
            if (cinemachineBrain != null) cinemachineBrain.enabled = true;
            if (cameraTransform != null)
                cameraTransform.SetLocalPositionAndRotation(placementCameraRestLocalPosition, placementCameraRestLocalRotation);
        }

        private void Update()
        {
            if (HandleBallInHand()) return;
            if (HandleCallPocket()) return;

            if (!isAiming)
            {
                currentCueBall = FindNearbyCueBall();
                if (currentCueBall != null && IsCue(handController.HeldObject) && CanShootNow() && InteractPressedThisFrame()
                    && (cueMelee == null || !cueMelee.IsSwinging) && (unarmedMelee == null || !unarmedMelee.IsAttacking))
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
            // found a nearest pocket — a reliable enough signal on its own
            // for this hint without re-deriving the whole call-mode state.
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

            if (IsOutOfReach)
            {
                GUI.Box(new Rect(Screen.width / 2f - 200f, 60f, 400f, 32f),
                    "Trop loin de la bille — contourne la table pour tirer");
            }

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

        private PoolBall FindNearbyCueBall()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out PoolBall ball) && ball.IsCueBall && ball.Rigidbody.linearVelocity.magnitude < MaxSpeedToAim)
                    return ball;
            }
            return null;
        }

        // CharacterController resolves movement incrementally through Move();
        // reassigning transform.position directly while it's enabled fights
        // that internal state on a jump this large (it can report a bogus
        // collision against whatever the capsule swept through on the way),
        // so it's briefly disabled for the teleport — the standard trick for
        // relocating a CharacterController outside of Move().
        private void TeleportBody(Vector3 position, float yaw)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            characterController.enabled = true;
        }

        // How far past aimBaseDistance the body needs to stand for this shot
        // to actually clear the table — NEVER capped: the body must always
        // end up outside the play area, whatever that takes, or it ends up
        // standing inside the table. What the hands/cue mesh visually
        // stretch to cover (capped by maxHandShift) is a separate concern —
        // see IsOutOfReach/AimMeshReach — not something this distance can
        // be shortened for.
        private float RequiredReachExtra(Vector3 ballPos, Vector3 aimDirection)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            float clearDistance = surface != null
                ? surface.DistanceToClearPlayArea(ballPos, -aimDirection, tableClearanceMargin)
                : 0f;
            return Mathf.Max(0f, clearDistance + tableClearanceMargin - aimBaseDistance);
        }

        // Where the body should stand, and which way it faces, for a shot
        // along aimDirection (pointing THROUGH the ball). Uses the held cue's
        // own line (captured in EnterAim, in this body's local space) rather
        // than the body's forward: the yaw is whatever makes the cue point
        // along aimDirection, the position whatever puts the ball on that
        // line, aimBaseDistance + AimReachExtra along the cue from its origin.
        private void ComputeAimBodyPose(Vector3 ballPos, Vector3 aimDirection, out Vector3 position, out float yaw)
        {
            AimReachExtra = RequiredReachExtra(ballPos, aimDirection);
            float distance = aimBaseDistance + AimReachExtra;

            Vector3 originHorizontal = new Vector3(cueLocalOrigin.x + aimCueOffset.x, 0f, cueLocalOrigin.z + aimCueOffset.z);
            Vector3 tipHorizontal = new Vector3(cueLocalTipDirection.x, 0f, cueLocalTipDirection.z);
            if (!hasCueLine || tipHorizontal.sqrMagnitude < 0.0001f)
            {
                originHorizontal = Vector3.zero;
                tipHorizontal = Vector3.forward;
            }
            tipHorizontal.Normalize();

            float tipAngle = Mathf.Atan2(tipHorizontal.x, tipHorizontal.z) * Mathf.Rad2Deg;
            yaw = Quaternion.LookRotation(aimDirection).eulerAngles.y - tipAngle + bodyYawOffsetWhileAiming;

            Quaternion bodyRotation = Quaternion.Euler(0f, yaw, 0f);
            position = ballPos - bodyRotation * (originHorizontal + tipHorizontal * distance);
            position.y = transform.position.y;
        }

        private void EnterAim()
        {
            isAiming = true;
            chargedPower = 0f;
            contactOffset = Vector2.zero;
            fpsController.enabled = false;
            if (cinemachineBrain != null) cinemachineBrain.enabled = false;
            EnableHeadLook(true);
            SetArmBendGoals(aiming: true);

            // Snapshot the held cue's line in this body's local space while
            // it's still in its idle pose — ComputeAimBodyPose then keeps
            // that exact relationship while lining the shot up.
            LocalGrabbable heldCueOnEnter = handController.HeldObject;
            hasCueLine = heldCueOnEnter != null;
            if (hasCueLine)
            {
                Transform held = heldCueOnEnter.transform;
                cueRestLocalPosition = held.localPosition;
                cueRestLocalRotation = held.localRotation;

                aimBaseDistance = standDistance;
                if (heldCueOnEnter.TryGetComponent(out CueChargeSlide tipSlide) && tipSlide.TipDistance > 0f)
                    aimBaseDistance = tipSlide.TipDistance + currentCueBall.Radius + aimTipGap;
                cueLocalOrigin = transform.InverseTransformPoint(held.position);
                cueLocalTipDirection = transform.InverseTransformDirection(held.TransformDirection(CueTipAxis(heldCueOnEnter)));
            }

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

            Vector3 cueBallPosition = currentCueBall.Rigidbody.position;
            Vector3 toBall = cueBallPosition - transform.position;
            toBall.y = 0f;
            aimYaw = toBall.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toBall).eulerAngles.y
                : transform.eulerAngles.y;

            // Stand behind the cue ball on the shooting side — re-applied
            // every frame in UpdateAim() too, so the body keeps following
            // the shot line as the aim direction is adjusted.
            Vector3 aimDirectionOnEnter = toBall.sqrMagnitude > 0.001f ? toBall.normalized : transform.forward;
            ComputeAimBodyPose(cueBallPosition, aimDirectionOnEnter, out Vector3 standPosition, out float standYaw);

            TeleportBody(standPosition, standYaw);
        }

        // Points the head/spine at the cue ball while aiming (aimIKTarget), and
        // at the camera-following target (normalLookTarget) the rest of the
        // time, by swapping Look At IK's Target — its weight stays up as long
        // as there's something to look at. Enables the component itself too
        // (a disabled Look At IK solves nothing whatever its weight) and
        // reports what's missing instead of failing silently.
        private void EnableHeadLook(bool aiming)
        {
            if (lookAtIK == null)
            {
                if (aiming) Debug.LogWarning("[LocalPoolAimController] No Look At IK assigned — the head won't turn toward the ball.", this);
                return;
            }

            Transform target = aiming ? aimIKTarget : normalLookTarget;
            lookAtIK.solver.target = target;
            lookAtIK.enabled = target != null;
            lookAtIK.solver.IKPositionWeight = target != null ? 1f : 0f;

            if (!aiming || target == null)
            {
                if (aiming) Debug.LogWarning("[LocalPoolAimController] Aim IK Target is empty — the head has nothing to follow while aiming.", this);
                return;
            }

            string message = string.Empty;
            if (!lookAtIK.solver.IsValid(ref message))
                Debug.LogWarning($"[LocalPoolAimController] Look At IK isn't set up correctly: {message}", lookAtIK);
        }

        // Moves each existing bend goal (see the fields above) between its
        // idle local position (captured fresh on entering aim, so it's
        // whatever it actually was, not a guessed value) and that same
        // position plus the aim-only offset. Weight and every other FBBIK
        // setting are left exactly as already configured.
        private void SetArmBendGoals(bool aiming)
        {
            if (aiming)
            {
                if (leftArmBendGoal != null)
                {
                    leftArmBendGoalRestLocalPosition = leftArmBendGoal.localPosition;
                    leftArmBendGoal.localPosition += leftArmBendGoalAimLocalOffset;
                }
                if (rightArmBendGoal != null)
                {
                    rightArmBendGoalRestLocalPosition = rightArmBendGoal.localPosition;
                    rightArmBendGoal.localPosition += rightArmBendGoalAimLocalOffset;
                }
            }
            else
            {
                if (leftArmBendGoal != null) leftArmBendGoal.localPosition = leftArmBendGoalRestLocalPosition;
                if (rightArmBendGoal != null) rightArmBendGoal.localPosition = rightArmBendGoalRestLocalPosition;
            }
        }

        // Where the tip should actually touch the ball, given the current
        // strike point (contactOffset — the "Strike point" GUI dot, spin/
        // effect) instead of always dead centre — the same offset Shoot()
        // turns into spin, so the cue points exactly where the shot strikes.
        private Vector3 StrikePointWorld()
        {
            if (currentCueBall == null) return Vector3.zero;

            Vector3 direction = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
            return currentCueBall.Rigidbody.position + StrikeOffset(direction);
        }

        // Offset from the cue ball's centre to the strike point, for a shot
        // along direction.
        private Vector3 StrikeOffset(Vector3 direction)
        {
            if (contactOffset.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            return (right * contactOffset.x + Vector3.up * contactOffset.y) * (maxOffsetFraction * currentCueBall.Radius);
        }

        // Tip-ward axis in the cue's own local space: detected from its mesh
        // by CueChargeSlide when present, otherwise the aimHoldAxis field.
        private Vector3 CueTipAxis(LocalGrabbable cue)
        {
            return cue != null && cue.TryGetComponent(out CueChargeSlide slide) ? slide.TipAxis : aimHoldAxis.normalized;
        }

        // Moves the held cue (and with it the grip points, hence the hands)
        // forward along its own axis by AimReachExtra (never past
        // maxHandShift), relative to the pose it was held in before aiming.
        // Out of reach it stays at maxHandShift and keeps its tilt: dropping
        // the cue back to its carry pose there left the body in its aiming
        // stance with the arms pulled up, which looked broken — the pose now
        // stays, only the shot is refused (UpdateAim) and the mesh doesn't
        // extend to the ball (AimMeshReach).
        private void ApplyHandShift()
        {
            LocalGrabbable cue = handController.HeldObject;
            if (cue == null || !hasCueLine) return;

            float targetShift = Mathf.Min(AimReachExtra, maxHandShift);
            AimHandShift = Mathf.MoveTowards(AimHandShift, targetShift, 1.5f * Time.deltaTime);
            Transform held = cue.transform;
            Vector3 tipAxis = CueTipAxis(cue);
            Vector3 axisInParent = cueRestLocalRotation * tipAxis;
            Vector3 offsetInParent = held.parent != null
                ? held.parent.InverseTransformVector(transform.TransformVector(aimCueOffset))
                : aimCueOffset;
            held.localPosition = cueRestLocalPosition + offsetInParent + axisInParent * AimHandShift;

            // Tilt about the cue's own origin so the tip points at the ball,
            // starting from the pose it was carried in (not from last
            // frame's, which would compound). Smoothed so crossing the
            // out-of-reach limit doesn't snap it.
            Quaternion restWorld = held.parent != null ? held.parent.rotation * cueRestLocalRotation : cueRestLocalRotation;
            Quaternion targetTilt = Quaternion.identity;
            if (tiltCueToBall && currentCueBall != null)
            {
                Vector3 toBall = StrikePointWorld() - held.position;
                if (toBall.sqrMagnitude > 0.0001f)
                {
                    targetTilt = Quaternion.FromToRotation(restWorld * tipAxis, toBall.normalized);
                    targetTilt.ToAngleAxis(out float tiltAngle, out Vector3 tiltAxis);
                    if (tiltAngle > 180f) tiltAngle -= 360f;
                    if (Mathf.Abs(tiltAngle) > maxCueTilt)
                        targetTilt = Quaternion.AngleAxis(Mathf.Sign(tiltAngle) * maxCueTilt, tiltAxis);
                }
            }
            cueTilt = Quaternion.Slerp(cueTilt, targetTilt, 1f - Mathf.Exp(-12f * Time.deltaTime));
            held.rotation = cueTilt * restWorld;
        }

        private Quaternion cueTilt = Quaternion.identity;

        private void ExitAim() => LeaveAim(returnControlToPlayer: true);

        // Disabled mid-aim or mid-placement: a ragdoll knockdown (see
        // LocalPlayerRagdollController.OnKnockedDown), or scene teardown. Without
        // this, the CinemachineBrain stayed off — so the ragdoll camera, which
        // works through the Brain, never showed — and ExitAim only ran once the
        // player was back up, snapping the recovered body to the old aim yaw.
        // FPS control and the body's facing are left to whoever disabled this.
        private void OnDisable()
        {
            if (isAiming) LeaveAim(returnControlToPlayer: false);

            if (callPocketActive)
            {
                highlightedPocket?.SetSelectionHighlight(false);
                highlightedPocket = null;
                callPocketActive = false;
                EndPlacementView(returnControlToPlayer: false);
            }
            if (ballPlacementActive)
            {
                ballPlacementActive = false;
                placingCueBall = null;
                EndPlacementView(returnControlToPlayer: false);
            }
        }

        private void LeaveAim(bool returnControlToPlayer)
        {
            if (hasCueLine)
            {
                LocalGrabbable cue = handController != null ? handController.HeldObject : null;
                if (cue != null) cue.transform.SetLocalPositionAndRotation(cueRestLocalPosition, cueRestLocalRotation);
            }
            AimHandShift = 0f;
            cueTilt = Quaternion.identity;

            isAiming = false;
            if (returnControlToPlayer) fpsController.enabled = true;
            if (cinemachineBrain != null) cinemachineBrain.enabled = true;
            EnableHeadLook(false);
            SetArmBendGoals(aiming: false);

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

            // Keep facing the direction of the shot just taken (or backed out
            // of) instead of snapping back to whatever the body happened to
            // be facing before aiming started — aimYaw already tracks
            // wherever the player was last looking while aiming.
            if (returnControlToPlayer) transform.rotation = Quaternion.Euler(0f, aimYaw, 0f);

            if (cameraTransform != null)
                cameraTransform.SetLocalPositionAndRotation(cameraRestLocalPosition, cameraRestLocalRotation);

            if (cueBallPreview != null) cueBallPreview.enabled = false;
            if (objectBallPreview != null) objectBallPreview.enabled = false;

            AimReachExtra = 0f;
            hasCueLine = false;
        }

        private void UpdateAim()
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            bool isGamepad = lookAction.activeControl?.device is Gamepad;
            float turnSpeed = isGamepad ? gamepadAimTurnSpeed * Time.deltaTime : mouseAimTurnSpeed;

            // InvertedControlsPower — same look inversion + sensitivity boost
            // as the normal FPS view (LocalFpsPlayerController.InvertLook/
            // SensitivityMultiplier), applied directly here since the aim
            // orbit reads its own look input rather than going through that
            // controller (disabled while aiming). previewHidden also drives
            // hiding the trajectory preview below — no way to "read" the
            // flip off the line to compensate for it.
            PoolMatchRules rules = PoolMatchRules.Instance;
            int effectivePlayer = rules != null ? rules.GetEffectivePlayerIndex(playerInput.playerIndex) : 0;
            bool previewHidden = rules != null && rules.IsControlsInverted(effectivePlayer);
            if (previewHidden)
            {
                look.x = -look.x;
                turnSpeed *= rules.GetInvertedControlsSensitivityMultiplier(effectivePlayer);
            }

            aimYaw += look.x * turnSpeed;

            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            contactOffset += moveInput * offsetAdjustSpeed * Time.deltaTime;
            if (contactOffset.magnitude > 1f) contactOffset = contactOffset.normalized;

            Vector3 aimDirection = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
            Vector3 ballPos = currentCueBall.Rigidbody.position;

            // Keeps the body standing behind the cue (and facing it) as the
            // aim direction is adjusted — a plain direct set rather than
            // TeleportBody's disable/enable dance, since the per-frame delta
            // here is small (driven by look input) rather than the one big
            // jump EnterAim() makes.
            ComputeAimBodyPose(ballPos, aimDirection, out Vector3 standPosition, out float standYaw);
            transform.SetPositionAndRotation(standPosition, Quaternion.Euler(0f, standYaw, 0f));
            ApplyHandShift();

            // Creeps the camera in toward the cue tip as the shot charges —
            // a "charging zoom", on top of the shake LocalPoolPowerEffectReceiver
            // layers on separately (reads ChargeFraction).
            float currentOrbitDistance = orbitDistance - juiceSettings.chargeZoomDistance * ChargeFraction;
            cameraTransform.position = ballPos - aimDirection * currentOrbitDistance + Vector3.up * orbitHeight;
            cameraTransform.LookAt(ballPos + Vector3.up * (orbitHeight * 0.3f));

            if (previewHidden)
            {
                if (cueBallPreview != null) cueBallPreview.enabled = false;
                if (objectBallPreview != null) objectBallPreview.enabled = false;
            }
            else
            {
                UpdatePreview(ballPos, aimDirection);
            }
            UpdateAimIKTarget(ballPos);

            // No shot from an angle the arms can't reach: the aiming pose is
            // kept (see ApplyHandShift) but the tip doesn't reach the ball.
            if (IsOutOfReach)
            {
                chargedPower = 0f;
                return;
            }

            if (attackAction.IsPressed())
                chargedPower = Mathf.Min(chargedPower + chargeSpeed * Time.deltaTime, maxPower);
            else if (chargedPower > 0f)
                Shoot(aimDirection);
        }

        private void UpdateAimIKTarget(Vector3 ballPos)
        {
            if (aimIKTarget != null) aimIKTarget.position = ballPos;
        }

        private void UpdatePreview(Vector3 ballPos, Vector3 direction)
        {
            if (cueBallPreview == null) return;

            float cueRadiusWorld = currentCueBall.Radius;

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

            currentCueBall.ArmContactTracking();
            rules?.NotifyShotFired();

            Rigidbody cueBallBody = currentCueBall.Rigidbody;
            cueBallBody.AddForce(impulse, ForceMode.Impulse);

            Vector3 strikeOffset = StrikeOffset(direction);
            if (strikeOffset != Vector3.zero)
                cueBallBody.AddTorque(Vector3.Cross(strikeOffset, impulse), ForceMode.Impulse);

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
