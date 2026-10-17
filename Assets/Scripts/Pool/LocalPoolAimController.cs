using RootMotion.FinalIK;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Aiming and shooting. Holding the cue near a still cue ball, Interact
    // enters aim (camera orbits the ball), Look aims, Move sets the strike
    // point, Attack charges and shoots. Also runs the top-down ball-in-hand
    // view and the 8-ball pocket call.
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
        // Mouse deltas are pixels, sticks are -1..1: one speed each.
        [SerializeField] private float mouseAimTurnSpeed = 0.1f;
        [SerializeField] private float gamepadAimTurnSpeed = 150f;

        [Header("Shot")]
        [SerializeField] private float minPower = 0.1f;
        [SerializeField] private float maxPower = 1f;
        [SerializeField] private float chargeSpeed = 0.85f;
        // The charging zoom is in PoolScreenJuiceSettings, next to the charge shake.

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

        // The cue is never moved while aiming: the hands follow its grip
        // points and FBBIK would drag the whole body along. The body is turned
        // around it instead (ComputeAimBodyPose). This is the cue's local
        // axis along its length, tip first; flip it if the body faces backwards.
        [SerializeField] private Vector3 aimHoldAxis = Vector3.forward;

        // Part of the extra reach taken by moving the cue (so the hands)
        // forward; the rest is the mesh sliding through the hands. Kept small,
        // FBBIK pulls the body after the hands.
        [SerializeField] private float maxHandShift = 0.4f;

        // How far the hands themselves may go forward. Further, the arms
        // stretch and FBBIK folds the body over the rail; the rest of the
        // reach is the mesh sliding (CueChargeSlide). 0.75 m looked right.
        [Tooltip("Avancée maximale des mains (mètres), à portée comme hors de portée. Max Hand Shift reste la portée jusqu'à laquelle on peut tirer ; au-delà de cette avancée, c'est la queue qui glisse dans les mains. Trop grand = le corps se plie par-dessus la bande pour suivre les mains ; trop petit = beaucoup de coups « trop loin ».")]
        [SerializeField] private float maxHandStretch = 0.75f;

        // Past the arms' stretch, the torso leans over the table, like a
        // real player on a long shot.
        [Tooltip("Penché du buste au-dessus de la table (mètres) pour les coups plus loin que Max Hand Stretch : ajoute cette distance à la portée jouable, les bras gardant leur position.")]
        [SerializeField] private float maxBodyLean = 0.45f;
        [Tooltip("Flexion du buste (degrés) au penché maximal.")]
        [SerializeField] private float bodyLeanBend = 30f;
        [Tooltip("Full Body Biped IK du personnage, pour le penché (vide = le premier trouvé sous ce joueur).")]
        [SerializeField] private FullBodyBipedIK leanIK;

        // Beyond the normal reach: one leg up, hip on the rail. The hips rise
        // onto the rail, the support foot stays planted and the other leg lifts.
        [Header("Stretched shot (beyond the normal reach)")]
        [Tooltip("Portée supplémentaire (mètres) au-delà de la portée normale, couverte par le coup allongé (hanche sur la bande, jambe levée). 0 = pas de coup allongé : « trop loin » dès la portée normale dépassée.")]
        [SerializeField] private float maxStretchReach = 0.6f;
        [Tooltip("Montée du bassin (mètres) au coup allongé maximal : il passe sur la bande au lieu de s'enfoncer.")]
        [SerializeField] private float stretchHipRise = 0.2f;
        [Tooltip("Flexion du buste supplémentaire (degrés) au coup allongé maximal : le buste se couche sur la table.")]
        [SerializeField] private float stretchBend = 35f;
        [Tooltip("Hauteur (mètres) à laquelle la jambe arrière se lève au coup allongé maximal.")]
        [SerializeField] private float stretchLegLift = 0.45f;
        [Tooltip("Recul (mètres) de la jambe levée au coup allongé maximal.")]
        [SerializeField] private float stretchLegBack = 0.25f;
        [Tooltip("Tremblement de la visée (degrés) au coup allongé maximal, pour que ce coup reste un pari. 0 = aucun.")]
        [SerializeField] private float stretchAimWobble = 1.5f;
        [Tooltip("Tient le pied d'appui au sol (IK) dès que le buste se penche, pour que les jambes ne suivent pas le bassin.")]
        [SerializeField] private bool plantFeetWhileLeaning = true;
        [Tooltip("Jusqu'où (mètres) le bassin peut avancer au-dessus du tapis, depuis son bord. 0 = le bassin s'arrête au bord du tapis (au-dessus de la bande) ; le reste de l'avancée vient du buste qui se couche.")]
        [SerializeField] private float hipMaxOverFelt = 0f;
        [Tooltip("Hauteur minimale (mètres) du bassin au-dessus de ce qui est sous lui (bande, tapis) : il passe par-dessus au lieu d'entrer dedans.")]
        [SerializeField] private float hipAboveTable = 0.15f;
        [Tooltip("Flexion du buste supplémentaire maximale (degrés) pour compenser l'avancée que le bassin n'a pas pu faire.")]
        [SerializeField] private float maxCompensationBend = 40f;

        // Diagnostic of the last lean (aim trace): how far the hips were held
        // back from the felt and raised above the table.
        private float lastHipHeldBack, lastHipRaise, lastCompensationBend;

        // These jump when the player orbits past a corner, hence the smoothing.
        // The support foot is picked once per lean, otherwise it flips
        // between the two feet when both are about as close.
        [Tooltip("Vitesse de lissage de la retenue, de la montée et de la flexion du bassin (plus grand = plus réactif, plus petit = plus doux).")]
        [SerializeField] private float hipAdjustSharpness = 6f;
        private float smoothHeldBack, smoothRaise, smoothCompensation;

        // Out of reach even stretched: the front hand lets go and the body
        // straightens up until the shot is reachable again.
        [Header("Out of reach")]
        [Tooltip("Hors de portée : la main avant lâche la queue et le corps se redresse, puis elle la reprend dès que le coup redevient possible.")]
        [SerializeField] private bool releaseFrontHandOutOfReach = true;
        [Tooltip("Marge (mètres) au-delà de la portée avant de lâcher la main : évite de lâcher et reprendre sans arrêt à la limite.")]
        [SerializeField] private float outOfReachHysteresis = 0.1f;
        [Tooltip("Durée (secondes) pour lâcher ou reprendre la queue de la main avant.")]
        [SerializeField] private float frontHandReleaseTime = 0.25f;
        private LocalCueHolder cueHolder;
        private bool reachLost;
        private float frontHandRelease;
        private bool supportFootChosen, leftFootSupports;

        // Lean over the table (m) beyond Max Hand Stretch, applied in
        // ApplyLean. Past Max Body Lean it's the stretched shot.
        private float bodyLean;
        private bool feetDriven;

        // 0 = normal reach, 1 = stretched as far as Max Stretch Reach allows.
        public float StretchFraction => maxStretchReach > 0f ? Mathf.Clamp01((AimHandShift - maxHandStretch - maxBodyLean) / maxStretchReach) : 0f;

        // Cue offset while aiming only, on top of its carried pose, in body
        // axes (x right, y up, z forward). Read every frame, tweakable live.
        [SerializeField] private Vector3 aimCueOffset;

        public float AimReachExtra { get; private set; }
        public float AimHandShift { get; private set; }

        // The ball is further than the arms reach from outside the table: the
        // pose stays but no shot until the player orbits to a closer angle.
        public bool IsOutOfReach => isAiming && AimReachExtra > StretchedReach;

        // The normal reach plus the stretched shot's, still within Max Hand
        // Shift (the hands never go further forward than that).
        private float StretchedReach => Mathf.Max(MaxReach, Mathf.Min(maxHandShift, MaxReach + maxStretchReach));

        // Playable reach: Max Hand Shift, capped at the arms plus what the cue
        // can slide without its butt leaving the hands.
        private float MaxReach
        {
            get
            {
                LocalGrabbable cue = handController != null ? handController.HeldObject : null;
                float slide = cue != null && cue.TryGetComponent(out CueChargeSlide s) ? s.MaxMeshReach : 0f;
                return Mathf.Min(maxHandShift, maxHandStretch + maxBodyLean + slide);
            }
        }

        // How far CueChargeSlide slides the mesh: what the hands didn't cover.
        public float AimMeshReach => IsOutOfReach ? 0f : Mathf.Max(0f, AimReachExtra - AimHandShift);

        // Distance from the cue's origin to the ball at which the tip
        // touches it (measured from the mesh, else standDistance).
        private float aimBaseDistance;

        // Gap between the cue tip and the ball's surface at rest (metres):
        // 0 = touching, positive = a little way back from it.
        [SerializeField] private float aimTipGap = 0.02f;

        // Tilts the cue about its origin so the tip points at the ball, or it
        // passes over the ball when the hands are high. Capped so the grips
        // don't swing far.
        [SerializeField] private bool tiltCueToBall = true;

        [Tooltip("Diagnostic : pendant la visée, écrit toutes les 0,5 s la portée demandée, le décalage des mains, l'inclinaison de la queue, la position du corps et du bassin, pour comparer à portée / limite / hors de portée.")]
        [SerializeField] private bool traceAimPose;
        private float traceAimNext;
        [SerializeField] private float maxCueTilt = 25f;

        private Vector3 cueRestLocalPosition;
        private Quaternion cueRestLocalRotation;

        private Vector3 cueLocalOrigin;
        private Vector3 cueLocalTipDirection;
        private bool hasCueLine;

        // Head and spine turn toward the ball while aiming. Set the Look At
        // IK's Target to aimIKTarget, which already follows the ball.
        [SerializeField] private LookAtIK lookAtIK;

        // FBBIK's own bend goals, moved between an idle and an aiming offset
        // (weights untouched). Empty = not moved.
        [SerializeField] private Transform leftArmBendGoal;
        [SerializeField] private Vector3 leftArmBendGoalAimLocalOffset;
        [SerializeField] private Transform rightArmBendGoal;
        [SerializeField] private Vector3 rightArmBendGoalAimLocalOffset;

        private Vector3 leftArmBendGoalRestLocalPosition;
        private Vector3 rightArmBendGoalRestLocalPosition;

        // What the head follows outside aiming (child of CM_FPS). Empty = no
        // head look outside aiming.
        [SerializeField] private Transform normalLookTarget;

        private void Start()
        {
            EnableHeadLook(false);
            // Start, not Awake: LocalPlayerHandController.Awake adds them when
            // the prefab doesn't carry them, possibly after this Awake.
            powerController = GetComponent<LocalPoolPowerController>();
            effectReceiver = GetComponent<LocalPoolPowerEffectReceiver>();
        }

        // Body yaw added on top of the cue's line (90 = side-on stance). The
        // cue stays on the shot line either way.
        [SerializeField] private float bodyYawOffsetWhileAiming = 90f;

        [Header("Body positioning while aiming")]
        [SerializeField] private float standDistance = 1f;
        [Tooltip("Distance (mètres) entre le bord du tapis et le point où se tient le personnage en visée : largeur de la bande + un peu d'espace pour le corps. Plus petit = le personnage se colle à la table et atteint des billes plus loin.")]
        [SerializeField] private float tableClearanceMargin = 0.5f;

        [Header("Ball-in-hand placement (top-down view)")]
        // Camera height above the table, set by hand.
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
        // Optional: while it's picking a pocket for a power, aiming stands aside.
        private LocalPoolPowerController powerController;
        private LocalPoolPowerEffectReceiver effectReceiver;
        // The pocket pick for the 8-ball call (shared with the powers).
        private LocalPocketPointer pocketPointer;
        private bool IsChoosingPowerPocket => powerController != null && powerController.IsChoosingPocket;
        private CharacterController characterController;
        private PlayerInput playerInput;
        // The Brain rewrites the camera every LateUpdate, which would undo
        // the aim orbit: it's turned off while aiming.
        private CinemachineBrain cinemachineBrain;
        private InputAction lookAction;
        private InputAction moveAction;
        private InputAction interactAction;
        private InputAction attackAction;

        private PoolBall currentCueBall;
        private bool isAiming;
        // While aiming the camera's world pose is set here every frame:
        // shakes must add to it, not overwrite it.
        public bool IsAiming => isAiming;
        // 0..1 charge, for the charge shake and the zoom.
        public float ChargeFraction => maxPower > 0f ? Mathf.Clamp01(chargedPower / maxPower) : 0f;
        private float aimYaw;
        private float chargedPower;
        // A press of Attack made during the current aim (see UpdateAim).
        private bool chargeArmed;
        [Tooltip("Diagnostic : signale si le tir est vu comme déjà enfoncé à l'entrée en visée, et quelle touche lance chaque charge.")]
        [SerializeField] private bool traceAimInput = true;
        private Vector2 contactOffset;
        // Strike point on the cue ball (-1..1 each way), for the HUD's spin disc.
        public Vector2 ContactOffset => contactOffset;
        public int PlayerIndex => playerInput != null ? playerInput.playerIndex : 0;

        // A shot was struck: this player's PlayerInput index and the charge
        // it was struck with (0..1) — the HUD's "OHHH !" on a full-power shot.
        public static event System.Action<int, float> ShotTaken;
        private Vector3 cameraRestLocalPosition;
        private Quaternion cameraRestLocalRotation;

        private void Awake()
        {
            fpsController = GetComponent<LocalFpsPlayerController>();
            handController = GetComponent<LocalPlayerHandController>();
            cueHolder = GetComponent<LocalCueHolder>();
            cueMelee = GetComponent<LocalCueMelee>();
            unarmedMelee = GetComponent<LocalUnarmedMelee>();
            if (leanIK == null) leanIK = GetComponentInChildren<FullBodyBipedIK>(true);
            cinemachineBrain = GetComponentInChildren<CinemachineBrain>(true);
            characterController = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();
            pocketPointer = LocalPocketPointer.For(gameObject);

            if (juiceSettings == null)
                juiceSettings = PoolSettingsLoader.LoadOrDefault<PoolScreenJuiceSettings>("PoolScreenJuiceSettings");

            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            lookAction = map.FindAction(lookActionName, throwIfNotFound: true);
            moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
            attackAction = map.FindAction(attackActionName, throwIfNotFound: true);

            ResolveMissingReferences();

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

        // A player spawned by PlayerInputManager only has what its prefab
        // holds (scene overrides don't reach it): anything left empty is
        // found among its children or created.
        private void ResolveMissingReferences()
        {
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null) cameraTransform = cam.transform;
            }
            if (lookAtIK == null) lookAtIK = GetComponentInChildren<LookAtIK>(true);
            // Only ever moved onto the cue ball (UpdateAimIKTarget), so any
            // empty object will do.
            if (aimIKTarget == null)
            {
                aimIKTarget = new GameObject("AimIKTarget (auto)").transform;
                aimIKTarget.SetParent(transform, false);
            }

            if (cueBallPreview != null && objectBallPreview != null) return;
            LineRenderer[] lines = GetComponentsInChildren<LineRenderer>(true);
            if (cueBallPreview == null) cueBallPreview = PickLine(lines, "CueBall");
            if (objectBallPreview == null) objectBallPreview = PickLine(lines, "ObjectBall");
            // Unnamed lines left on the player, then new ones.
            if (cueBallPreview == null) cueBallPreview = PickLine(lines, null);
            if (objectBallPreview == null) objectBallPreview = PickLine(lines, null);
            if (cueBallPreview == null) cueBallPreview = CreatePreviewLine("CueBallPreviewLine (auto)", objectBallPreview);
            if (objectBallPreview == null) objectBallPreview = CreatePreviewLine("ObjectBallPreviewLine (auto)", cueBallPreview);
        }

        // First line not already used as a preview, whose object name holds
        // keyword (any name when null).
        private LineRenderer PickLine(LineRenderer[] lines, string keyword)
        {
            foreach (LineRenderer line in lines)
            {
                if (line == cueBallPreview || line == objectBallPreview) continue;
                if (keyword == null || line.name.Contains(keyword)) return line;
            }
            return null;
        }

        private LineRenderer CreatePreviewLine(string lineName, LineRenderer lookLike)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (lookLike != null)
            {
                line.widthMultiplier = lookLike.widthMultiplier;
                line.sharedMaterial = lookLike.sharedMaterial;
            }
            else
            {
                line.widthMultiplier = 0.005f;
                // Always included in builds; tinted by the line's colors.
                line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            return line;
        }

        public bool WantsInteractThisFrame(LocalGrabbable heldObject)
        {
            if (IsChoosingPowerPocket || callConfirmedFrame == Time.frameCount) return true;
            if (isAiming) return true;
            if (IsMyTurnToPlaceBall()) return true;
            if (IsMyTurnToCallPocket()) return true;
            return IsCue(heldObject) && FindNearbyCueBall() != null;
        }

        private static bool IsCue(LocalGrabbable heldObject)
        {
            return heldObject != null && heldObject.IsCue;
        }

        // Only the player whose turn it is can aim.
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

        // 8-ball call: group cleared and no pocket called yet.
        private bool IsMyTurnToCallPocket()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.CanPlayerShoot(playerInput.playerIndex) || rules.CalledEightBallPocket != null)
                return false;

            int effectivePlayer = rules.GetEffectivePlayerIndex(playerInput.playerIndex);
            return rules.IsShootingForEightBall(effectivePlayer);
        }

        private PoolBall placingCueBall;
        // One flag per interaction so neither clears the other's state.
        private bool ballPlacementActive;
        private bool callPocketActive;
        // The top-down view also drives the camera directly (see IsAiming).
        public bool IsPlacementViewActive => ballPlacementActive;
        // Read by the in-game HUD (MatchHud): which interaction is up, and
        // the pocket currently pointed at for the 8-ball call.
        public bool IsBallPlacementActive => ballPlacementActive;
        public bool IsCallPocketActive => callPocketActive;
        public PoolPocket HighlightedPocket => highlightedPocket;
        private Vector3 placementCameraRestLocalPosition;
        private Quaternion placementCameraRestLocalRotation;
        [Tooltip("Logs [PlacementTrace] de la vue de dessus (bille en main) : caméra, fenêtre, hauteur, plafond.")]
        [SerializeField] private bool tracePlacementView = true;
        private Vector3 placementCameraExpected;
        private int placementCheckFrames;
        [Tooltip("Vue de dessus : rien n'est dessiné au-delà de cette hauteur au-dessus du tapis (lampe, plafond, têtes des joueurs penchés), pour voir toute la table.")]
        [SerializeField] private float placementClearAbove = 0.5f;
        private Camera placementCamera;
        private float placementRestNearClip = -1f;

        // After a foul, the player who has the turn sees the table from above
        // and moves the cue ball with Move, Interact to confirm. Returns true
        // while active so the aim code below doesn't run.
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
                // Camera axes, not the table's, so Move matches the screen.
                Vector2 moveInput = moveAction.ReadValue<Vector2>();
                Vector3 delta = (cameraTransform.right * moveInput.x + cameraTransform.up * moveInput.y)
                    * placementMoveSpeed * Time.deltaTime;
                Vector3 rawTarget = placingCueBall.transform.position + delta;
                Vector3 clamped = surface.ClampToPlayArea(rawTarget, placingCueBall.Radius);
                clamped = PoolPocket.AvoidAllPockets(clamped, placingCueBall.Radius);
                // Nor on a hole smashed in the felt (TableHolePower).
                clamped = TableHole.AvoidAll(clamped, placingCueBall.Radius);
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
        // Interact was used for the call this frame: the hands mustn't
        // take it as a drop (WantsInteractThisFrame).
        private int callConfirmedFrame = -1;

        // 8-ball call: same pocket pick as the powers (LocalPocketPointer),
        // in the normal view; Interact confirms. Aiming waits for it.
        private bool HandleCallPocket()
        {
            // A power's pocket pick holds the pointer: the call waits for it.
            if (!IsMyTurnToCallPocket() || IsChoosingPowerPocket)
            {
                if (callPocketActive) EndCallPocket();
                return false;
            }

            if (!callPocketActive)
            {
                callPocketActive = true;
                if (isAiming) ExitAim();
            }

            highlightedPocket = pocketPointer.Point();

            if (InteractPressedThisFrame() && highlightedPocket != null)
            {
                PoolMatchRules.Instance.CallEightBallPocket(highlightedPocket);
                EndCallPocket();
                callConfirmedFrame = Time.frameCount;
            }

            return true;
        }

        private void EndCallPocket()
        {
            pocketPointer.Release();
            highlightedPocket = null;
            callPocketActive = false;
        }

        // Camera handling of the top-down view only (state is in
        // ballPlacementActive).
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
            placementCameraExpected = cameraTransform.position;
            placementCheckFrames = 10;

            // Anything over the table (lamp, ceiling, a player) hid the felt:
            // the near clip plane goes down to just above it. Cinemachine is
            // off here, so the lens stays as set.
            placementCamera = cameraTransform.GetComponent<Camera>();
            if (placementCamera != null)
            {
                placementRestNearClip = placementCamera.nearClipPlane;
                float aboveFelt = cameraTransform.position.y - center.y;
                placementCamera.nearClipPlane = Mathf.Max(placementRestNearClip, aboveFelt - placementClearAbove);
            }

            if (tracePlacementView)
            {
                Camera cam = cameraTransform.GetComponent<Camera>();
                float ceiling = Physics.Raycast(center + Vector3.up * 0.2f, Vector3.up, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.distance + 0.2f : -1f;
                Debug.Log($"[PlacementTrace] P{playerInput.playerIndex} top-down view: camera '{cameraTransform.name}' " +
                          $"(Camera component: {(cam != null ? "yes" : "NO")}, brain: {(cinemachineBrain != null ? "yes" : "no")}), " +
                          $"viewport rect {(cam != null ? cam.rect.ToString() : "-")}, aspect {(cam != null ? cam.aspect : 0f):F2}, " +
                          $"screen {Screen.width}x{Screen.height}, fov {(cam != null ? cam.fieldOfView : 0f):F0}, " +
                          $"height {PlacementHeightForCurrentViewport():F2} m (tuned {placementCameraHeight:F2}), table center {center}, " +
                          $"table right {(surface != null ? surface.transform.right.ToString() : "-")}, " +
                          $"half length {(surface != null ? surface.HalfLength : 0f):F2} / width {(surface != null ? surface.HalfWidth : 0f):F2}, " +
                          $"ceiling above table {(ceiling < 0f ? "none" : ceiling.ToString("F2") + " m" + (hit.collider != null ? $" ({hit.collider.name})" : ""))}", this);
            }
        }

        // Diagnostic: the top-down camera moved by something else after we placed it.
        private void CheckPlacementCamera()
        {
            if (placementCheckFrames <= 0 || cameraTransform == null) return;
            placementCheckFrames--;
            if (tracePlacementView && (cameraTransform.position - placementCameraExpected).sqrMagnitude > 0.0001f)
            {
                Debug.LogWarning($"[PlacementTrace] P{playerInput.playerIndex} top-down camera moved by something else: " +
                                 $"expected {placementCameraExpected}, now {cameraTransform.position}.", this);
                placementCheckFrames = 0;
            }
        }

        // The height is tuned full screen. A narrower split-screen viewport
        // shows less of the table at the same height, so it's scaled by the
        // aspect ratio difference.
        private float PlacementHeightForCurrentViewport()
        {
            Camera cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (cam == null || cam.aspect <= 0f || Screen.height <= 0) return placementCameraHeight;

            float fullScreenAspect = Screen.width / (float)Screen.height;
            return placementCameraHeight * (fullScreenAspect / cam.aspect);
        }

        private void EndPlacementView(bool returnControlToPlayer = true)
        {
            if (placementCamera != null && placementRestNearClip > 0f)
            {
                placementCamera.nearClipPlane = placementRestNearClip;
                placementRestNearClip = -1f;
            }
            if (returnControlToPlayer) fpsController.enabled = true;
            if (cinemachineBrain != null) cinemachineBrain.enabled = true;
            if (cameraTransform != null)
                cameraTransform.SetLocalPositionAndRotation(placementCameraRestLocalPosition, placementCameraRestLocalRotation);
        }

        private void Update()
        {
            CheckPlacementCamera();
            if (HandleBallInHand()) return;
            if (HandleCallPocket()) return;

            // Picking a pocket for a power (LocalPoolPowerController): it
            // takes Interact, and the pointer needs the normal FPS view.
            if (IsChoosingPowerPocket)
            {
                if (isAiming) ExitAim();
                return;
            }

            if (!isAiming)
            {
                currentCueBall = FindNearbyCueBall();
                if (currentCueBall != null && IsCue(handController.HeldObject) && CanShootNow() && InteractPressedThisFrame()
                    && (cueMelee == null || !cueMelee.IsSwinging) && (unarmedMelee == null || !unarmedMelee.IsAttacking)
                    && handController.CueSettled)
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
            // The in-game HUD (MatchHud) draws all of this in its own style.
            if (PoolMatchRules.ExternalHud) return;

            // Only set while the call is in progress.
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

        private int nearbyCueBallFrame = -1;
        private PoolBall nearbyCueBall;

        // Cue ball at rest within interactRange. Asked twice a frame
        // (Update, WantsInteractThisFrame), so worked out once.
        private PoolBall FindNearbyCueBall()
        {
            if (nearbyCueBallFrame == Time.frameCount) return nearbyCueBall;
            nearbyCueBallFrame = Time.frameCount;
            nearbyCueBall = null;
            PoolBall ball = PoolBall.FindCueBall();
            // Parked (ball in hand, table hole): its collider is off, the
            // overlap test never found it either.
            if (ball == null || ball.Rigidbody.isKinematic) return null;
            float reach = interactRange + ball.Radius;
            if ((ball.transform.position - transform.position).sqrMagnitude > reach * reach) return null;
            if (ball.Rigidbody.linearVelocity.magnitude < MaxSpeedToAim) nearbyCueBall = ball;
            return nearbyCueBall;
        }

        // The CharacterController fights a direct jump this big (bogus
        // collisions), so it's off for the teleport.
        private void TeleportBody(Vector3 position, float yaw)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            characterController.enabled = true;
        }

        // How much further back the body must stand so its root clears the
        // table by Table Clearance Margin. Never capped, or the body ends up
        // inside the table; the cue itself can be over it.
        private float RequiredReachExtra(Vector3 ballPos, Vector3 aimDirection, Vector3 rootOffset)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return 0f;
            // Walk back from the root's spot until it leaves the table.
            float clearDistance = surface.DistanceToClearPlayArea(ballPos + rootOffset, -aimDirection, tableClearanceMargin);
            return Mathf.Max(0f, clearDistance - aimBaseDistance);
        }

        // Where the body stands and faces for a shot along aimDirection: the
        // yaw that points the held cue's line (captured in EnterAim) at the
        // ball, the position that puts the ball on that line.
        private void ComputeAimBodyPose(Vector3 ballPos, Vector3 aimDirection, out Vector3 position, out float yaw)
        {
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

            // bodyRotation * tipHorizontal is aimDirection, so the root is at
            // ballPos - bodyRotation * originHorizontal - aimDirection * distance.
            Vector3 rootOffset = -(bodyRotation * originHorizontal);
            AimReachExtra = RequiredReachExtra(ballPos, aimDirection, rootOffset);
            float distance = aimBaseDistance + AimReachExtra;

            position = ballPos + rootOffset - aimDirection * distance;
            position.y = transform.position.y;
        }

        private void EnterAim()
        {
            isAiming = true;
            chargedPower = 0f;
            chargeArmed = false;
            // Diagnostic for the "already charged on entering aim" bug: which
            // control Attack thinks is held at this moment.
            if (traceAimInput && attackAction.IsPressed())
                Debug.LogWarning($"[AimInput] {name}: Attack already held on entering aim — control {attackAction.activeControl?.path ?? "?"}, " +
                                 $"value {attackAction.ReadValue<float>():F2}, phase {attackAction.phase}, scheme {playerInput.currentControlScheme}. Ignored until released and pressed again.", this);
            contactOffset = Vector2.zero;
            fpsController.enabled = false;
            if (cinemachineBrain != null) cinemachineBrain.enabled = false;
            EnableHeadLook(true);
            SetArmBendGoals(aiming: true);

            // The cue's line in body space, taken in its idle pose.
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

            // A queued Vision Impair starts counting on entering aim, i.e. at
            // the start of this player's turn.
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

            // Re-applied every frame in UpdateAim.
            Vector3 aimDirectionOnEnter = toBall.sqrMagnitude > 0.001f ? toBall.normalized : transform.forward;
            ComputeAimBodyPose(cueBallPosition, aimDirectionOnEnter, out Vector3 standPosition, out float standYaw);

            TeleportBody(standPosition, standYaw);
        }

        // Look At IK target: the cue ball while aiming, the camera target
        // otherwise. Also enables the component and logs what's missing.
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

        // Moves each bend goal between its idle position (taken on entering
        // aim) and that position plus the aim offset.
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

        // Where the tip touches the ball, strike point (spin) included.
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

        // Moves the cue (so the hands) forward along its axis by the reach,
        // up to Max Hand Stretch; the mesh slides for the rest. Out of reach
        // the pose stays, only the shot is refused.
        private void ApplyHandShift()
        {
            LocalGrabbable cue = handController.HeldObject;
            if (cue == null || !hasCueLine) return;

            // Hands forward up to the arms' stretch plus the lean; past the
            // normal reach the stretched shot adds to it.
            float stretch = Mathf.Clamp(AimReachExtra - MaxReach, 0f, maxStretchReach);
            float targetShift = Mathf.Min(AimReachExtra, maxHandShift, maxHandStretch + maxBodyLean + stretch);

            // Some hysteresis so the hand doesn't let go and grab again at
            // the limit.
            if (releaseFrontHandOutOfReach)
            {
                if (!reachLost && AimReachExtra > StretchedReach + outOfReachHysteresis) reachLost = true;
                else if (reachLost && AimReachExtra <= StretchedReach) reachLost = false;
            }
            else reachLost = false;
            if (reachLost) targetShift = Mathf.Min(AimReachExtra, maxHandStretch);
            frontHandRelease = Mathf.MoveTowards(frontHandRelease, reachLost ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, frontHandReleaseTime));
            if (cueHolder != null) cueHolder.FrontHandRelease = frontHandRelease;
            AimHandShift = Mathf.MoveTowards(AimHandShift, targetShift, 1.5f * Time.deltaTime);
            bodyLean = Mathf.Max(0f, AimHandShift - maxHandStretch);
            Transform held = cue.transform;
            Vector3 tipAxis = CueTipAxis(cue);
            Vector3 axisInParent = cueRestLocalRotation * tipAxis;
            Vector3 offsetInParent = held.parent != null
                ? held.parent.InverseTransformVector(transform.TransformVector(aimCueOffset))
                : aimCueOffset;
            held.localPosition = cueRestLocalPosition + offsetInParent + axisInParent * AimHandShift;

            // From the carried pose (not last frame's, it would compound),
            // smoothed so the reach limit doesn't snap it.
            Quaternion restWorld = held.parent != null ? held.parent.rotation * cueRestLocalRotation : cueRestLocalRotation;
            Quaternion targetTilt = Quaternion.identity;
            if (tiltCueToBall && currentCueBall != null)
            {
                // Out of reach: tilted as for the farthest possible shot.
                // Aiming at the real ball folded the body over the rail.
                Vector3 aimPoint = StrikePointWorld();
                if (IsOutOfReach)
                {
                    Vector3 back = Vector3.ProjectOnPlane(aimPoint - held.position, Vector3.up);
                    if (back.sqrMagnitude > 1e-6f) aimPoint -= back.normalized * (AimReachExtra - StretchedReach);
                }
                Vector3 toBall = aimPoint - held.position;
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

            if (traceAimPose && Time.time >= traceAimNext)
            {
                traceAimNext = Time.time + 0.5f;
                cueTilt.ToAngleAxis(out float traceTilt, out _);
                if (traceTilt > 180f) traceTilt -= 360f;
                Vector3 toBallFlat = currentCueBall != null ? Vector3.ProjectOnPlane(currentCueBall.transform.position - transform.position, Vector3.up) : Vector3.zero;
                Animator anim = GetComponentInChildren<Animator>();
                Transform hips = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
                string slideInfo = held.TryGetComponent(out CueChargeSlide traceSlide)
                    ? $"charge {ChargeFraction:F2} | cue follows {(traceSlide.FollowedAimController == this ? "this player" : traceSlide.FollowedAimController != null ? traceSlide.FollowedAimController.name : "nobody")} | mesh offset {traceSlide.CurrentMeshOffset:F2} m | "
                    : "no CueChargeSlide on the held cue | ";
                Debug.Log($"[AimTrace] {name}: {slideInfo}{(IsOutOfReach ? "OUT OF REACH" : "in reach")} | reach extra {AimReachExtra:F2} (normal max {MaxReach:F2}, stretched max {StretchedReach:F2}, Max Hand Shift {maxHandShift:F2}) | hand shift {AimHandShift:F2} | body lean {bodyLean:F2} | stretch {StretchFraction:F2} | front hand {(reachLost ? "LET GO" : "on cue")} ({frontHandRelease:F2}) | hips held back {lastHipHeldBack:F2} m, raised {lastHipRaise:F2} m, extra bend {lastCompensationBend:F0}° | mesh slide {AimMeshReach:F2} | cue tilt {traceTilt:F1}° " +
                          $"| cue local pos {held.localPosition} | body→ball {toBallFlat.magnitude:F2} m | root y {transform.position.y:F2} | hips y {(hips != null ? hips.position.y : float.NaN):F2}", this);
            }
        }

        private Quaternion cueTilt = Quaternion.identity;

        private void ExitAim() => LeaveAim(returnControlToPlayer: true);

        // Disabled mid-aim (ragdoll knockdown, teardown): the Brain must come
        // back on or the ragdoll camera never shows.
        private void OnEnable()
        {
            if (leanIK != null) leanIK.solver.OnPreUpdate += ApplyLean;
        }

        // Leans the body over the table after the Animator, before FBBIK:
        // forward and a bit down, spine bent to match.
        private void ApplyLean()
        {
            if (leanIK == null) return;
            if (!isAiming || bodyLean <= 0f)
            {
                ReleaseFeet();
                return;
            }
            LocalGrabbable cue = handController.HeldObject;
            Vector3 forward = cue != null ? Vector3.ProjectOnPlane(cue.transform.TransformDirection(CueTipAxis(cue)), Vector3.up) : transform.forward;
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            forward.Normalize();

            float k = Mathf.Clamp01(bodyLean / Mathf.Max(0.01f, maxBodyLean));
            float s = StretchFraction;
            RootMotion.BipedReferences r = leanIK.references;

            // Normal lean: forward and a little down. Stretched: the hips come
            // back up, onto the rail.
            float sink = Mathf.Min(bodyLean, maxBodyLean) * 0.3f;
            Vector3 offset = forward * bodyLean + Vector3.up * (stretchHipRise * s - sink);

            // The hips stop at the felt's edge and stay above what's under
            // them; the torso bends further to make up the difference.
            float heldBack = 0f, raise = 0f, compensation = 0f;
            if (r.pelvis != null)
            {
                Vector3 hips = r.pelvis.position + offset;
                PoolTableSurface surface = PoolTableSurface.Instance;
                if (surface != null)
                {
                    float over = surface.DistanceToClearPlayArea(hips, -forward, -hipMaxOverFelt);
                    if (over > 0f)
                    {
                        heldBack = Mathf.Max(0f, Mathf.Min(over, Vector3.Dot(offset, forward)));
                        float torso = TorsoLength(r);
                        if (torso > 0.05f)
                            compensation = Mathf.Min(maxCompensationBend, Mathf.Asin(Mathf.Clamp01(heldBack / torso)) * Mathf.Rad2Deg);
                    }
                }
                float below = SurfaceHeightBelow(hips - forward * heldBack);
                if (!float.IsNegativeInfinity(below))
                    raise = Mathf.Max(0f, below + hipAboveTable - (hips.y));
            }
            float blend = 1f - Mathf.Exp(-hipAdjustSharpness * Time.deltaTime);
            smoothHeldBack = Mathf.Lerp(smoothHeldBack, heldBack, blend);
            smoothRaise = Mathf.Lerp(smoothRaise, raise, blend);
            smoothCompensation = Mathf.Lerp(smoothCompensation, compensation, blend);
            offset += Vector3.up * smoothRaise - forward * smoothHeldBack;
            lastHipHeldBack = smoothHeldBack;
            lastHipRaise = smoothRaise;
            lastCompensationBend = smoothCompensation;

            float bend = bodyLeanBend * k + stretchBend * s + smoothCompensation;
            if (bend > 0f && r.spine != null && r.spine.Length > 0)
            {
                Quaternion perBone = Quaternion.AngleAxis(bend / r.spine.Length, Vector3.Cross(Vector3.up, forward));
                foreach (Transform bone in r.spine)
                    if (bone != null) bone.rotation = perBone * bone.rotation;
            }
            leanIK.solver.bodyEffector.positionOffset += offset;

            if (!plantFeetWhileLeaning || r.leftFoot == null || r.rightFoot == null) { ReleaseFeet(keepLean: true); return; }

            // Feet as animated: the one nearer the ball supports, the other
            // lifts up and back when stretched.
            if (!supportFootChosen)
            {
                Vector3 ball = currentCueBall != null ? currentCueBall.transform.position : transform.position + forward;
                leftFootSupports = Vector3.ProjectOnPlane(r.leftFoot.position - ball, Vector3.up).sqrMagnitude
                                 <= Vector3.ProjectOnPlane(r.rightFoot.position - ball, Vector3.up).sqrMagnitude;
                supportFootChosen = true;
            }
            bool leftSupports = leftFootSupports;
            Vector3 lift = Vector3.up * (stretchLegLift * s) - forward * (stretchLegBack * s);
            IKEffector left = leanIK.solver.leftFootEffector, right = leanIK.solver.rightFootEffector;
            left.position = r.leftFoot.position + (leftSupports ? Vector3.zero : lift);
            right.position = r.rightFoot.position + (leftSupports ? lift : Vector3.zero);
            // Faded in over the first few centimetres of lean, so starting to
            // lean doesn't snap the legs.
            float w = Mathf.Clamp01(bodyLean / 0.1f);
            left.positionWeight = w;
            right.positionWeight = w;
            feetDriven = true;
        }

        // Pelvis to the shoulders' midpoint, as animated.
        private static float TorsoLength(RootMotion.BipedReferences r)
        {
            if (r.pelvis == null || r.leftUpperArm == null || r.rightUpperArm == null) return 0f;
            return Vector3.Distance(r.pelvis.position, (r.leftUpperArm.position + r.rightUpperArm.position) * 0.5f);
        }

        private readonly RaycastHit[] surfaceHits = new RaycastHit[16];
        private int surfaceMask;   // built on first use (GetMask isn't allowed in a field initializer)

        // Height of the highest solid surface under a point, ignoring this
        // player, the held cue and the balls. -Infinity if none.
        private float SurfaceHeightBelow(Vector3 point)
        {
            Transform cue = handController != null && handController.HeldObject != null ? handController.HeldObject.transform : null;
            // Player layers excluded too: the ragdoll's own colliders sit
            // right under this ray and would push the hips up frame after frame.
            if (surfaceMask == 0) surfaceMask = ~LayerMask.GetMask("Ragdoll", "Player", "PlayerAnimated");
            int count = Physics.RaycastNonAlloc(point + Vector3.up, Vector3.down, surfaceHits, 2.5f, surfaceMask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = surfaceHits[i].collider;
                if (c.transform.IsChildOf(transform) || (cue != null && c.transform.IsChildOf(cue))) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.TryGetComponent(out PoolBall _)) continue;
                best = Mathf.Max(best, surfaceHits[i].point.y);
            }
            return best;
        }

        // Only undoes what ApplyLean set: the pickup (LocalCueHolder) and the
        // kicks (LocalUnarmedMelee) drive these effectors too.
        private void ReleaseFeet(bool keepLean = false)
        {
            // Lean over: the next one starts fresh (support foot picked
            // again, no leftover hip adjustment).
            if (!keepLean)
            {
                supportFootChosen = false;
                smoothHeldBack = smoothRaise = smoothCompensation = 0f;
            }
            if (!feetDriven || leanIK == null) return;
            leanIK.solver.leftFootEffector.positionWeight = 0f;
            leanIK.solver.rightFootEffector.positionWeight = 0f;
            feetDriven = false;
        }

        private void OnDisable()
        {
            ReleaseFeet();
            if (leanIK != null) leanIK.solver.OnPreUpdate -= ApplyLean;
            if (isAiming) LeaveAim(returnControlToPlayer: false);

            if (callPocketActive) EndCallPocket();
            if (ballPlacementActive)
            {
                ballPlacementActive = false;
                placingCueBall = null;
                EndPlacementView(returnControlToPlayer: false);
            }
        }

        // shotTaken: left by shooting (Shoot), not by backing out.
        private void LeaveAim(bool returnControlToPlayer, bool shotTaken = false)
        {
            if (hasCueLine)
            {
                LocalGrabbable cue = handController != null ? handController.HeldObject : null;
                if (cue != null) cue.transform.SetLocalPositionAndRotation(cueRestLocalPosition, cueRestLocalRotation);
            }
            AimHandShift = 0f;
            cueTilt = Quaternion.identity;
            reachLost = false;
            frontHandRelease = 0f;
            if (cueHolder != null) cueHolder.FrontHandRelease = 0f;

            isAiming = false;
            if (returnControlToPlayer) fpsController.enabled = true;
            if (cinemachineBrain != null) cinemachineBrain.enabled = true;
            EnableHeadLook(false);
            SetArmBendGoals(aiming: false);

            // A debuff started on entering aim ends with the shot; without a
            // shot it goes on hold until the next aim this turn.
            PoolMatchRules rulesForPower = PoolMatchRules.Instance;
            if (rulesForPower != null)
            {
                int effectivePlayer = rulesForPower.GetEffectivePlayerIndex(playerInput.playerIndex);
                if (shotTaken)
                {
                    rulesForPower.EndVisionImpair(effectivePlayer);
                    rulesForPower.EndInvertedControls(effectivePlayer);
                }
                else
                {
                    rulesForPower.SuspendVisionImpair(effectivePlayer);
                    rulesForPower.SuspendInvertedControls(effectivePlayer);
                }
            }

            // Keep facing the shot instead of snapping back.
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

            // Inverted Controls: same inversion and boost as the FPS view
            // (that controller is off while aiming). The preview is hidden
            // too, or it would give the flip away.
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

            // Stretched shot: the aim sways with the stretch, shown on the
            // cue and the preview so the player can time it.
            float wobble = stretchAimWobble * StretchFraction;
            if (wobble > 0f) wobble *= (Mathf.PerlinNoise(Time.time * 1.3f, playerInput.playerIndex * 7.1f) - 0.5f) * 2f;
            Vector3 aimDirection = Quaternion.Euler(0f, aimYaw + wobble, 0f) * Vector3.forward;
            Vector3 ballPos = currentCueBall.Rigidbody.position;

            // Small per-frame change: set directly, no teleport needed.
            ComputeAimBodyPose(ballPos, aimDirection, out Vector3 standPosition, out float standYaw);
            transform.SetPositionAndRotation(standPosition, Quaternion.Euler(0f, standYaw, 0f));
            ApplyHandShift();

            // Charging zoom toward the tip.
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
                chargeArmed = false;
                return;
            }

            // Attack sometimes reads as held on entering aim: only a press
            // made during this aim charges.
            if (attackAction.WasPressedThisFrame())
            {
                chargeArmed = true;
                if (traceAimInput) Debug.Log($"[AimInput] {name}: charge started by {attackAction.activeControl?.path ?? "?"}", this);
            }
            if (chargeArmed && attackAction.IsPressed())
                chargedPower = Mathf.Min(chargedPower + chargeSpeed * Time.deltaTime, maxPower);
            else if (chargedPower > 0f)
                Shoot(aimDirection);
            else if (!attackAction.IsPressed())
                chargeArmed = false;
        }

        private void UpdateAimIKTarget(Vector3 ballPos)
        {
            if (aimIKTarget != null) aimIKTarget.position = ballPos;
        }

        private void UpdatePreview(Vector3 ballPos, Vector3 direction)
        {
            if (cueBallPreview == null) return;

            float cueRadiusWorld = currentCueBall.Radius;

            // A sphere cast ignores what it overlaps at the start, so it
            // starts just past the cue ball's surface.
            Vector3 castOrigin = ballPos + direction * (cueRadiusWorld + 0.001f);
            float castDistance = Mathf.Max(0f, previewMaxDistance - cueRadiusWorld);

            // The line always shows, full length when nothing is hit.
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
            ShotTaken?.Invoke(PlayerIndex, ChargeFraction);
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
            LeaveAim(returnControlToPlayer: true, shotTaken: true);

            // After ExitAim, so the recoil doesn't fight the aim orbit.
            if (effectReceiver != null) effectReceiver.PlayShotFeedback();
        }
    }
}
