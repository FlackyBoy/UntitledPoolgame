using System.Collections;
using System.Collections.Generic;
using RootMotion;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Hitting the other player with the held cue: hold Attack (cue in hand,
    // not aiming) to charge, release to strike. The direction comes from
    // how the player turns while charging, measured from the view at the
    // press — turn right = the cue winds up on the right (the blow sweeps
    // back to the left), turn left = the opposite, look up = the cue goes
    // up (overhead blow), no clear turn = thrust with the tip, pool-shot
    // stance. What the player sees while charging is the blow that will
    // come. On release the view swings back toward where it was at the
    // press — or toward an opponent in a cone around it (light aim
    // assist) — so the blow lands where the player was looking, arcade
    // style. The arc is laid out in the camera's frame.
    //
    // The swing is procedural. The held cue — and with it the grips, so the
    // hands through FBBIK — orbits around the shoulders: the hands travel a
    // wide arc at a constant distance from the shoulders (always within
    // reach, so FBBIK doesn't drag the body toward them). On top of that,
    // just before FBBIK solves (solver.OnPreUpdate), the spine is turned by
    // a share of the swing angle and the body effector is leaned back while
    // charging and forward on the strike, so the torso takes part in the
    // blow instead of only the arms.
    //
    // A muscle of another player's ragdoll caught by the cue is hit through
    // PuppetMaster's own API (MuscleCollisionBroadcaster.Hit): unpin and
    // force grow with the charge, and a full charge knocks the target down
    // outright. BehaviourPuppet handles the stumble/fall; a fall goes
    // through LocalPlayerRagdollController as usual.
    [RequireComponent(typeof(LocalPlayerHandController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalCueMelee : MonoBehaviour
    {
        // Tooltips are in French: they are read by the designer in the
        // Inspector.
        [Tooltip("Nom de l'action map du joueur dans l'asset d'input.")]
        [SerializeField] private string actionMapName = "Player";
        [Tooltip("Action qui charge (maintenir) et déclenche (relâcher) le coup.")]
        [SerializeField] private string attackActionName = "Attack";

        [Tooltip("Caméra du joueur : le coup est posé dans son repère. Vide = la première caméra sous ce joueur.")]
        [SerializeField] private Transform viewTransform;
        [Tooltip("Full Body Biped IK du personnage. Vide = le premier trouvé sous ce joueur. Sans lui, le coup marche mais le buste ne suit pas et la queue pivote autour des mains.")]
        [SerializeField] private FullBodyBipedIK fullBodyIK;

        [Header("Direction from turning while charging")]
        [Tooltip("Degrés à tourner (sur le côté ou vers le haut) depuis la vue à l'appui pour choisir le coup : droite = balayage vers la gauche, gauche = vers la droite, haut = vertical. En dessous au relâchement : estoc.")]
        [SerializeField] private float turnThreshold = 12f;
        [Tooltip("Secondes sans rotation avant que la queue se mette en position d'estoc (un appui bref reste un estoc).")]
        [SerializeField] private float thrustStanceDelay = 0.2f;
        [Tooltip("Secondes de fondu quand la pose change brusquement (position d'estoc → balayage).")]
        [SerializeField] private float poseBlendTime = 0.1f;

        [Header("Strike: view swings back to the target")]
        [Tooltip("Retour de la vue vers la cible pendant la frappe : 1 = revient complètement là où l'on regardait à l'appui (ou vers l'adversaire visé), 0 = la vue ne bouge pas.")]
        [SerializeField, Range(0f, 1f)] private float returnViewOnStrike = 1f;
        [Tooltip("Rotation maximale (degrés) imposée à la vue pendant la frappe.")]
        [SerializeField] private float maxViewReturn = 120f;
        [Tooltip("Assistance : demi-angle (degrés) du cône autour de la vue à l'appui dans lequel un adversaire devient la cible du retour de vue.")]
        [SerializeField] private float assistAngle = 40f;
        [Tooltip("Assistance : distance maximale (mètres) de l'adversaire visé.")]
        [SerializeField] private float assistRange = 3f;

        [Header("Charge")]
        [Tooltip("Secondes de maintien pour atteindre la charge maximale.")]
        [SerializeField] private float chargeTime = 0.8f;
        [Tooltip("Part de l'armé déjà atteinte sans aucune charge (0 = la queue part du repos, 1 = toujours armée à fond).")]
        [SerializeField, Range(0f, 1f)] private float minWindup = 0.45f;

        [Header("Arcs (degrees)")]
        [Tooltip("Centre de rotation de la queue : 1 = les épaules (les mains décrivent un grand arc, les bras travaillent), 0 = le point entre les mains (seule la pointe bouge).")]
        [SerializeField, Range(0f, 1f)] private float shoulderPivot = 1f;
        [Tooltip("Balayages : angle (degrés) d'armé à pleine charge, sur le côté.")]
        [SerializeField] private float sweepWindup = 100f;
        [Tooltip("Balayages : angle (degrés) atteint de l'autre côté en fin de coup.")]
        [SerializeField] private float sweepFollowThrough = 110f;
        [Tooltip("Coup vertical : angle (degrés) d'armé vers le haut à pleine charge.")]
        [SerializeField] private float overheadWindup = 95f;
        [Tooltip("Coup vertical : angle (degrés) atteint vers le bas en fin de coup.")]
        [SerializeField] private float overheadFollowThrough = 70f;
        [Header("Thrust — pool-shot stance, strike with the tip (metres)")]
        [Tooltip("Estoc : recul (mètres) de la queue le long de son axe à pleine charge.")]
        [SerializeField] private float thrustPullBack = 0.25f;
        [Tooltip("Estoc : avancée (mètres) de la queue le long de son axe en fin de coup.")]
        [SerializeField] private float thrustReach = 0.5f;
        [Tooltip("Position de tir : décalage (mètres) du point entre les mains à droite (+) des épaules, dans le repère de la caméra.")]
        [SerializeField] private float stanceSide = 0.12f;
        [Tooltip("Position de tir : hauteur (mètres) du point entre les mains par rapport aux épaules (négatif = en dessous).")]
        [SerializeField] private float stanceHeight = -0.25f;
        [Tooltip("Position de tir : distance (mètres) du point entre les mains devant les épaules.")]
        [SerializeField] private float stanceForward = 0.15f;
        [Tooltip("Secondes pour passer de la pose de port à la position de tir.")]
        [SerializeField] private float stanceBlendTime = 0.15f;

        [Header("Body (applied just before FBBIK solves)")]
        [Tooltip("Part de l'angle du balayage donnée au buste (répartie sur les vertèbres) : il tourne avec le coup.")]
        [SerializeField, Range(0f, 1f)] private float sweepTorsoTwist = 0.4f;
        [Tooltip("Part de l'angle du coup vertical donnée au buste : il se plie avec le coup.")]
        [SerializeField, Range(0f, 1f)] private float overheadTorsoBend = 0.25f;
        [Tooltip("Recul du corps (mètres) à pleine charge.")]
        [SerializeField] private float windupLeanBack = 0.1f;
        [Tooltip("Engagement du corps dans le coup (mètres) au moment de la frappe.")]
        [SerializeField] private float strikeLean = 0.18f;

        [Header("Timing (seconds)")]
        [Tooltip("Durée de la frappe, de l'armé à la fin du geste.")]
        [SerializeField] private float strikeTime = 0.13f;
        [Tooltip("Pause en fin de geste avant le retour : donne du poids au coup.")]
        [SerializeField] private float followThroughHold = 0.08f;
        [Tooltip("Durée du retour à la pose de port.")]
        [SerializeField] private float recoverTime = 0.3f;
        [Tooltip("Délai minimal entre deux coups.")]
        [SerializeField] private float cooldown = 0.25f;

        [Header("Hit detection")]
        [Tooltip("Layers des colliders des muscles des ragdolls (PuppetMaster). Vide = layer « Ragdoll ».")]
        [SerializeField] private LayerMask muscleLayers;
        [Tooltip("Rayon (mètres) de la zone de touche le long de la queue. Volontairement large (arcade) : la queue fait ~3 cm.")]
        [SerializeField] private float hitRadius = 0.2f;
        [Tooltip("Tests de touche par image entre deux poses, pour qu'un coup rapide ne traverse pas un bras.")]
        [SerializeField] private int subSteps = 4;

        [Header("Strength (from the charge, 0 = tap, 1 = full)")]
        // Reference: PuppetMaster's raycast-hit demo knocks a character
        // down with unpin 5 / force 1500 on a single muscle.
        [Tooltip("Déséquilibre (unpin PuppetMaster) d'un coup sans charge. Repère : 5 suffit en général à faire tomber.")]
        [SerializeField] private float minUnpin = 1f;
        [Tooltip("Déséquilibre (unpin PuppetMaster) d'un coup à pleine charge.")]
        [SerializeField] private float maxUnpin = 8f;
        [Tooltip("Force (newtons) appliquée au muscle touché, sans charge.")]
        [SerializeField] private float minForce = 400f;
        [Tooltip("Force (newtons) appliquée au muscle touché, à pleine charge.")]
        [SerializeField] private float maxForce = 2500f;
        [Tooltip("À partir de cette charge, la cible tombe à coup sûr, quelle que soit sa résistance.")]
        [SerializeField, Range(0f, 1f)] private float guaranteedKnockdownAt = 0.9f;
        [Tooltip("Part de la force dirigée vers le haut (soulève la cible).")]
        [SerializeField, Range(0f, 1f)] private float upwardBias = 0.15f;

        [Header("Feel")]
        // Global on purpose: both split-screen players feel the hit.
        [Tooltip("Durée (secondes, temps réel) du ralenti à l'impact. 0 = désactivé. S'applique aux deux joueurs.")]
        [SerializeField] private float hitstopDuration = 0.06f;
        [Tooltip("Vitesse du temps pendant ce ralenti (0,05 = quasi figé).")]
        [SerializeField] private float hitstopTimeScale = 0.05f;
        [Tooltip("Affiche la jauge de charge et la flèche de direction provisoires dans la moitié d'écran du joueur.")]
        [SerializeField] private bool showChargeIndicator = true;

        [Header("Debug")]
        [Tooltip("Écrit dans la console chaque coup et chaque touche (direction, charge, muscle, déséquilibre, force, état de la cible juste après).")]
        [SerializeField] private bool debugLogs = true;

        private enum Direction { SweepLeft, SweepRight, Overhead, Thrust }
        private enum Phase { Idle, Charging, Strike, Hold, Recover }

        private LocalPlayerHandController handController;
        private LocalCuePickupTrigger cueTrigger;
        private LocalPoolAimController aimController;
        private LocalPoolPowerEffectReceiver effects;
        private PuppetMaster ownPuppet;
        private Camera viewCamera;
        private LocalFpsPlayerController fps;
        private LocalUnarmedMelee unarmedMelee;
        private InputAction attackAction;

        // Every player's melee, for the aim assist to find opponents.
        private static readonly List<LocalCueMelee> all = new List<LocalCueMelee>();

        private Phase phase = Phase.Idle;
        private Direction direction;
        private bool directionLocked;  // a turn decided the blow (else: thrust)
        private float startYaw;
        private float startPitch;
        private Vector3 startForward;
        private float returnYaw;       // view swing planned for the strike
        private float returnPitch;
        private float appliedYaw;
        private float appliedPitch;
        private Vector3 fadeFromPosition;
        private Quaternion fadeFromRotation;
        private float fadeLeft;
        private float charge;
        private float phaseTime;
        private float cooldownLeft;
        private float fromParam;
        private float lastParam;
        private float currentParam;
        private float leanAmount;   // -1 = full lean back, +1 = full lean into the blow
        private bool hitSomething;

        private Transform swingCue;
        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;
        private Vector3 pivotLocal;   // what the cue orbits around
        private Vector3 gripLocal;    // between the hands — start of the hit capsule
        private Vector3 tipLocal;     // tip of the cue at rest
        private Vector3 shoulderLocal;
        private Vector3 gripInCue;    // same points in the cue's own space (thrust stance)
        private Vector3 tipInCue;
        private Vector3 tipAxisInCue;
        private float stanceBlend;    // thrust only: 0 = carry pose, 1 = pool-shot stance
        private readonly HashSet<PuppetMaster> hitThisSwing = new HashSet<PuppetMaster>();
        private readonly Collider[] overlapBuffer = new Collider[32];

        // Read by LocalPoolAimController so aim mode can't start mid-swing
        // (it would snapshot the cue's pose halfway through the arc).
        public bool IsSwinging => phase != Phase.Idle;

        private void Awake()
        {
            handController = GetComponent<LocalPlayerHandController>();
            cueTrigger = GetComponent<LocalCuePickupTrigger>();
            aimController = GetComponent<LocalPoolAimController>();
            effects = GetComponent<LocalPoolPowerEffectReceiver>();
            fps = GetComponent<LocalFpsPlayerController>();
            unarmedMelee = GetComponent<LocalUnarmedMelee>();
            ownPuppet = GetComponentInChildren<PuppetMaster>(true);
            if (fullBodyIK == null) fullBodyIK = GetComponentInChildren<FullBodyBipedIK>(true);
            viewCamera = viewTransform != null ? viewTransform.GetComponent<Camera>() : GetComponentInChildren<Camera>(true);
            if (viewTransform == null && viewCamera != null) viewTransform = viewCamera.transform;

            InputActionMap map = GetComponent<PlayerInput>().actions.FindActionMap(actionMapName, throwIfNotFound: true);
            attackAction = map.FindAction(attackActionName, throwIfNotFound: true);

            if (muscleLayers.value == 0) muscleLayers = LayerMask.GetMask("Ragdoll");
        }

        private void OnEnable()
        {
            if (fullBodyIK != null) fullBodyIK.solver.OnPreUpdate += ApplyBody;
            all.Add(this);
        }

        private void OnDisable()
        {
            if (fullBodyIK != null) fullBodyIK.solver.OnPreUpdate -= ApplyBody;
            all.Remove(this);
            EndSwing(restorePose: true);
        }

        private void Update()
        {
            if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;

            if (phase == Phase.Idle)
            {
                if (cooldownLeft <= 0f && attackAction.WasPressedThisFrame() && CanSwing())
                    BeginCharge();
                return;
            }

            // The cue left the hands mid-swing (dropped, knocked down):
            // nothing left to animate — and not ours to put back.
            if (handController.HeldObject == null || handController.HeldObject.transform != swingCue)
            {
                EndSwing(restorePose: false);
                return;
            }

            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case Phase.Charging: UpdateCharging(); break;
                case Phase.Strike: UpdateStrike(); break;
                case Phase.Hold: if (phaseTime >= followThroughHold) { phase = Phase.Recover; phaseTime = 0f; } break;
                case Phase.Recover: UpdateRecover(); break;
            }
        }

        // ---- Input ----

        // Degrees turned since the press: yaw positive = to the right, pitch
        // positive = up.
        private float CurrentYaw => transform.eulerAngles.y;
        private float CurrentPitch => fps != null ? fps.Pitch : 0f;
        private float TurnedYaw => Mathf.DeltaAngle(startYaw, CurrentYaw);
        private float TurnedUp => startPitch - CurrentPitch;

        // Turning right winds the cue up on the right, so the blow sweeps to
        // the left (and the other way round); looking up raises it for an
        // overhead. Looking down decides nothing.
        private bool TryReadTurn(out Direction turned)
        {
            float yaw = TurnedYaw, up = TurnedUp;
            turned = Direction.Thrust;
            if (Mathf.Abs(yaw) < turnThreshold && up < turnThreshold) return false;
            if (Mathf.Abs(yaw) >= up) turned = yaw > 0f ? Direction.SweepLeft : Direction.SweepRight;
            else turned = Direction.Overhead;
            return true;
        }

        private bool CanSwing()
        {
            LocalGrabbable held = handController.HeldObject;
            if (held == null || !held.TryGetComponent(out Cue _)) return false;
            if (aimController != null && (aimController.IsAiming || aimController.IsPlacementViewActive)) return false;
            if (unarmedMelee != null && unarmedMelee.IsAttacking) return false;
            // Only once the pickup has finished moving the cue into the
            // hands: before that, PickUpCue itself still writes its pose.
            PickUpCue pickUp = cueTrigger != null ? cueTrigger.PickUpCue : null;
            return viewTransform != null && (pickUp == null || pickUp.IsSettled);
        }

        // ---- Phases ----

        private void BeginCharge()
        {
            swingCue = handController.HeldObject.transform;
            if (swingCue.parent == null) { swingCue = null; return; }

            restLocalPosition = swingCue.localPosition;
            restLocalRotation = swingCue.localRotation;

            Vector3 gripWorld = swingCue.position;
            InteractionTarget[] grips = swingCue.GetComponentsInChildren<InteractionTarget>();
            if (grips.Length > 0)
            {
                gripWorld = Vector3.zero;
                foreach (InteractionTarget grip in grips) gripWorld += grip.transform.position;
                gripWorld /= grips.Length;
            }
            Vector3 pivotWorld = Vector3.Lerp(gripWorld, ShoulderCenter(gripWorld), shoulderPivot);

            Transform parent = swingCue.parent;
            pivotLocal = parent.InverseTransformPoint(pivotWorld);
            gripLocal = parent.InverseTransformPoint(gripWorld);
            tipLocal = parent.InverseTransformPoint(TipWorld());
            // Shoulders sampled once: reading them every frame would feed
            // the IK's own lean and walk bob back into the stance.
            shoulderLocal = parent.InverseTransformPoint(ShoulderCenter(gripWorld));
            gripInCue = swingCue.InverseTransformPoint(gripWorld);
            tipInCue = swingCue.InverseTransformPoint(TipWorld());
            tipAxisInCue = swingCue.TryGetComponent(out CueChargeSlide slide) ? slide.TipAxis : Vector3.forward;
            stanceBlend = 0f;
            fadeLeft = 0f;

            startYaw = CurrentYaw;
            startPitch = CurrentPitch;
            startForward = viewTransform.forward;
            direction = Direction.Thrust;   // until a turn says otherwise
            directionLocked = false;
            charge = 0f;
            currentParam = 0f;
            leanAmount = 0f;
            hitSomething = false;
            hitThisSwing.Clear();
            phase = Phase.Charging;
            phaseTime = 0f;
        }

        private Vector3 ShoulderCenter(Vector3 fallback)
        {
            if (fullBodyIK == null) return fallback;
            BipedReferences r = fullBodyIK.references;
            if (r.leftUpperArm == null || r.rightUpperArm == null) return fallback;
            return (r.leftUpperArm.position + r.rightUpperArm.position) * 0.5f;
        }

        private void UpdateCharging()
        {
            charge = Mathf.Clamp01(phaseTime / chargeTime);

            // The first clear turn decides the blow for the rest of the
            // charge. Until then the cue settles into the thrust stance.
            if (!directionLocked && TryReadTurn(out Direction turned))
            {
                StartPoseFade();
                direction = turned;
                directionLocked = true;
                if (debugLogs) Debug.Log($"[CueMelee] {name}: direction {direction} (turned {TurnedYaw:F0}° sideways, {TurnedUp:F0}° up)", this);
            }
            if (!directionLocked)
                stanceBlend = Mathf.Clamp01((phaseTime - thrustStanceDelay) / Mathf.Max(0.01f, stanceBlendTime));

            currentParam = WindupParam();
            leanAmount = -charge;
            ApplyPose(currentParam);

            if (!attackAction.IsPressed()) BeginStrike();
        }

        private void BeginStrike()
        {
            fromParam = WindupParam();
            lastParam = fromParam;
            PlanViewReturn(out string target);
            appliedYaw = 0f;
            appliedPitch = 0f;
            phase = Phase.Strike;
            phaseTime = 0f;
            if (debugLogs) Debug.Log($"[CueMelee] {name}: strike {direction}, charge {charge:F2}, view back {returnYaw:F0}° / {returnPitch:F0}° toward {target}", this);
        }

        // Where the view goes during the strike: back to the view at the
        // press, or to the nearest opponent's chest within the assist cone
        // around it.
        private void PlanViewReturn(out string target)
        {
            Vector3 origin = viewTransform.position;
            Vector3 aim = startForward;
            target = "the press direction";
            float best = assistAngle;
            foreach (LocalCueMelee other in all)
            {
                if (other == this || other.viewTransform == null) continue;
                Vector3 to = other.viewTransform.position - Vector3.up * 0.35f - origin;
                if (to.magnitude > assistRange) continue;
                float angle = Vector3.Angle(startForward, to);
                if (angle > best) continue;
                best = angle;
                aim = to.normalized;
                target = other.name;
            }

            float aimYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            float aimPitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
            returnYaw = Mathf.Clamp(Mathf.DeltaAngle(CurrentYaw, aimYaw), -maxViewReturn, maxViewReturn) * returnViewOnStrike;
            returnPitch = Mathf.Clamp(aimPitch - CurrentPitch, -maxViewReturn, maxViewReturn) * returnViewOnStrike;
            if (fps == null) returnYaw = returnPitch = 0f;
        }

        // Swings the view by the planned amount over the strike (smooth, so
        // it leads the cue and settles on the target as the blow lands).
        private void ApplyViewReturn(float t)
        {
            if (fps == null) return;
            float eased = t * t * (3f - 2f * t);
            float yaw = returnYaw * eased, pitch = returnPitch * eased;
            fps.AddLook(yaw - appliedYaw, pitch - appliedPitch);
            appliedYaw = yaw;
            appliedPitch = pitch;
        }

        private void UpdateStrike()
        {
            float t = Mathf.Clamp01(phaseTime / strikeTime);
            // Accelerates into the target: slow start, fast finish.
            float param = Mathf.Lerp(fromParam, EndParam(), t * t * t);
            currentParam = param;
            leanAmount = Mathf.Lerp(-charge, 1f, Mathf.Sqrt(t));
            // A quick tap still reaches the full stance before the tip lands.
            if (direction == Direction.Thrust)
                stanceBlend = Mathf.Min(1f, stanceBlend + Time.deltaTime / Mathf.Max(0.01f, Mathf.Min(stanceBlendTime, strikeTime * 0.5f)));
            ApplyViewReturn(t);
            ApplyPose(param);
            DetectHits(lastParam, param);
            lastParam = param;

            if (t >= 1f)
            {
                if (!hitSomething && debugLogs) Debug.Log($"[CueMelee] {name}: {direction} missed (nothing on the muscle layers along the cue)", this);
                fromParam = param;
                phase = Phase.Hold;
                phaseTime = 0f;
            }
        }

        private void UpdateRecover()
        {
            float t = Mathf.Clamp01(phaseTime / recoverTime);
            float eased = 1f - (1f - t) * (1f - t);
            currentParam = Mathf.Lerp(fromParam, 0f, eased);
            leanAmount = 1f - eased;
            stanceBlend = 1f - eased;
            ApplyPose(currentParam);
            if (t >= 1f) EndSwing(restorePose: true);
        }

        private void EndSwing(bool restorePose)
        {
            if (restorePose && swingCue != null && handController != null && handController.HeldObject != null
                && handController.HeldObject.transform == swingCue)
                swingCue.SetLocalPositionAndRotation(restLocalPosition, restLocalRotation);

            if (phase != Phase.Idle) cooldownLeft = cooldown;
            phase = Phase.Idle;
            currentParam = 0f;
            leanAmount = 0f;
            swingCue = null;
        }

        // ---- Pose ----
        // One parameter per direction: an angle (degrees) for sweeps and
        // overheads, a distance along the view (metres) for the thrust.
        // 0 = the held pose.

        private float WindupParam()
        {
            float k = Mathf.Lerp(minWindup, 1f, charge);
            switch (direction)
            {
                case Direction.SweepLeft: return sweepWindup * k;       // cue to the right
                case Direction.SweepRight: return -sweepWindup * k;     // cue to the left
                case Direction.Overhead: return -overheadWindup * k;    // cue up
                default: return -thrustPullBack * k;                    // pulled back
            }
        }

        private float EndParam()
        {
            switch (direction)
            {
                case Direction.SweepLeft: return -sweepFollowThrough;
                case Direction.SweepRight: return sweepFollowThrough;
                case Direction.Overhead: return overheadFollowThrough;
                default: return thrustReach;
            }
        }

        // Axis of the arc in world space, from the camera's frame so the
        // swing tilts with the look (positive angle around the view's up =
        // to the right; around the view's right = down).
        private Vector3 ArcAxisWorld() => direction == Direction.Overhead ? viewTransform.right : viewTransform.up;

        private Quaternion SwingLocal(float param) =>
            Quaternion.AngleAxis(param, swingCue.parent.InverseTransformDirection(ArcAxisWorld()));

        // Thrust pose in world space: the carry pose blended toward a
        // pool-shot stance — cue turned so its tip points along the view
        // (smallest rotation, so it keeps its roll and the hands stay on
        // their grips), the point between the grips placed in front of the
        // chest — then moved along the view by param (pulled back while
        // charging, driven forward on the strike: the tip leads).
        private void ThrustPose(float param, float blend, out Vector3 position, out Quaternion rotation)
        {
            Transform parent = swingCue.parent;
            Vector3 restPosition = parent.TransformPoint(restLocalPosition);
            Quaternion restRotation = parent.rotation * restLocalRotation;

            Vector3 forward = viewTransform.forward;
            Quaternion stanceRotation = Quaternion.FromToRotation(restRotation * tipAxisInCue, forward) * restRotation;
            Vector3 shoulders = parent.TransformPoint(shoulderLocal);
            Vector3 stanceGrip = shoulders + viewTransform.right * stanceSide + viewTransform.up * stanceHeight + forward * (stanceForward + param);
            Vector3 stancePosition = stanceGrip - stanceRotation * Vector3.Scale(swingCue.lossyScale, gripInCue);

            position = Vector3.Lerp(restPosition, stancePosition, blend);
            rotation = Quaternion.Slerp(restRotation, stanceRotation, blend);
        }

        private void ApplyPose(float param)
        {
            Vector3 localPosition;
            Quaternion localRotation;
            if (direction == Direction.Thrust)
            {
                ThrustPose(param, stanceBlend, out Vector3 position, out Quaternion rotation);
                Transform parent = swingCue.parent;
                localPosition = parent.InverseTransformPoint(position);
                localRotation = Quaternion.Inverse(parent.rotation) * rotation;
            }
            else
            {
                Quaternion swing = SwingLocal(param);
                localPosition = pivotLocal + swing * (restLocalPosition - pivotLocal);
                localRotation = swing * restLocalRotation;
            }

            if (fadeLeft > 0f)
            {
                fadeLeft -= Time.deltaTime;
                float w = 1f - Mathf.Clamp01(fadeLeft / Mathf.Max(0.01f, poseBlendTime));
                w = w * w * (3f - 2f * w);
                localPosition = Vector3.Lerp(fadeFromPosition, localPosition, w);
                localRotation = Quaternion.Slerp(fadeFromRotation, localRotation, w);
            }
            swingCue.SetLocalPositionAndRotation(localPosition, localRotation);
        }

        // Starts a short cross-fade from the cue's current pose, for when
        // the pose target jumps (the cue was settling into the thrust
        // stance and a turn now asks for a sweep).
        private void StartPoseFade()
        {
            fadeFromPosition = swingCue.localPosition;
            fadeFromRotation = swingCue.localRotation;
            fadeLeft = poseBlendTime;
        }

        private void SegmentAt(float param, out Vector3 gripWorld, out Vector3 tipWorld)
        {
            Transform parent = swingCue.parent;
            if (direction == Direction.Thrust)
            {
                ThrustPose(param, stanceBlend, out Vector3 position, out Quaternion rotation);
                Matrix4x4 pose = Matrix4x4.TRS(position, rotation, swingCue.lossyScale);
                gripWorld = pose.MultiplyPoint3x4(gripInCue);
                tipWorld = pose.MultiplyPoint3x4(tipInCue);
                return;
            }
            Quaternion swing = SwingLocal(param);
            gripWorld = parent.TransformPoint(pivotLocal + swing * (gripLocal - pivotLocal));
            tipWorld = parent.TransformPoint(pivotLocal + swing * (tipLocal - pivotLocal));
        }

        // Tip of the cue in world space: from CueChargeSlide's mesh analysis
        // when available (same source as the aim code), otherwise along the
        // cue's forward.
        private Vector3 TipWorld()
        {
            if (swingCue.TryGetComponent(out CueChargeSlide slide) && slide.TipDistance > 0f)
                return swingCue.TransformPoint(slide.TipAxis * slide.TipDistance);
            return swingCue.position + swingCue.forward * 0.7f;
        }

        // ---- Body ----
        // Called by FBBIK right before it solves, after the Animator wrote
        // this frame's pose: rotations added here are on top of the
        // animation (nothing accumulates), and positionOffset is reset by
        // FBBIK itself after each solve.
        private void ApplyBody()
        {
            if (phase == Phase.Idle || viewTransform == null) return;

            BipedReferences r = fullBodyIK.references;
            float share = direction == Direction.Overhead ? overheadTorsoBend : direction == Direction.Thrust ? 0f : sweepTorsoTwist;
            if (share > 0f && r.spine != null && r.spine.Length > 0)
            {
                Quaternion perBone = Quaternion.AngleAxis(currentParam * share / r.spine.Length, ArcAxisWorld());
                foreach (Transform bone in r.spine)
                    if (bone != null) bone.rotation = perBone * bone.rotation;
            }

            Vector3 into = direction == Direction.Overhead
                ? (viewTransform.forward - viewTransform.up * 0.5f).normalized
                : viewTransform.forward;
            float lean = leanAmount < 0f ? leanAmount * windupLeanBack : leanAmount * strikeLean;
            fullBodyIK.solver.bodyEffector.positionOffset += into * lean;
        }

        // ---- Hits ----

        // Capsule from the grips to the tip, tested at a few points between
        // last frame's pose and this frame's so a fast swing can't pass
        // through an arm between two frames.
        private void DetectHits(float fromP, float toP)
        {
            int steps = Mathf.Max(1, subSteps);
            SegmentAt(fromP, out _, out Vector3 previousTip);
            for (int s = 1; s <= steps; s++)
            {
                SegmentAt(Mathf.Lerp(fromP, toP, s / (float)steps), out Vector3 grip, out Vector3 tip);
                Vector3 travel = tip - previousTip;
                int count = Physics.OverlapCapsuleNonAlloc(grip, tip, hitRadius, overlapBuffer, muscleLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) TryHit(overlapBuffer[i], grip, tip, travel);
                previousTip = tip;
            }
        }

        private void TryHit(Collider hit, Vector3 grip, Vector3 tip, Vector3 travel)
        {
            if (!MeleeHit.TryGetTarget(hit, transform, ownPuppet, hitThisSwing, out MuscleCollisionBroadcaster broadcaster, out PuppetMaster target)) return;
            hitSomething = true;

            float unpin = Mathf.Lerp(minUnpin, maxUnpin, charge);
            float force = Mathf.Lerp(minForce, maxForce, charge);

            // Where the tip is going, plus a little lift (the thrust pushes
            // along the view).
            Vector3 push = direction == Direction.Thrust || travel.sqrMagnitude < 1e-6f ? viewTransform.forward : travel.normalized;
            Vector3 forceDirection = Vector3.Lerp(push, Vector3.up, upwardBias).normalized;
            Vector3 point = hit.ClosestPoint(Vector3.Lerp(grip, tip, 0.8f));

            broadcaster.Hit(unpin, forceDirection * force, point);

            BehaviourPuppet puppet = MeleeHit.FindPuppetBehaviour(target);
            bool forced = charge >= guaranteedKnockdownAt && puppet != null;
            if (forced) puppet.SetState(BehaviourPuppet.State.Unpinned);

            if (debugLogs)
            {
                Debug.Log($"[CueMelee] {name} HIT {target.name} on '{hit.name}' — {direction}, charge {charge:F2}, unpin {unpin:F1}, force {force:F0}" +
                          (forced ? " → knockdown forced (full charge)" : ""), this);
                if (puppet != null) StartCoroutine(LogOutcome(puppet));
            }

            if (effects != null) effects.PlayShotFeedback();
            if (hitstopDuration > 0f) StartCoroutine(MeleeHit.Hitstop(hitstopDuration, hitstopTimeScale));
        }

        private IEnumerator LogOutcome(BehaviourPuppet puppet)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log($"[CueMelee] → {puppet.puppetMaster.name} is now {puppet.state} (Puppet = still standing, Unpinned = down, GetUp = getting up)", this);
        }

        // ---- Indicator (provisional, until the new UI) ----
        // Drawn inside this player's own viewport so it works in split-screen.
        private void OnGUI()
        {
            if (!showChargeIndicator || phase != Phase.Charging || viewCamera == null) return;

            Rect view = viewCamera.pixelRect;
            float x = view.x + view.width / 2f, y = Screen.height - view.yMin - view.height * 0.22f;

            // The arrow shows where the blow will go (not where the cue is).
            string arrow = direction == Direction.SweepLeft ? "◀" : direction == Direction.SweepRight ? "▶" : direction == Direction.Overhead ? "▼" : "●";
            GUI.Label(new Rect(x - 20f, y - 34f, 40f, 30f), arrow, new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter });

            const float w = 140f, h = 10f;
            GUI.color = new Color(0f, 0f, 0f, .6f);
            GUI.DrawTexture(new Rect(x - w / 2f, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(.19f, .71f, .4f), new Color(1f, .3f, .24f), charge);
            GUI.DrawTexture(new Rect(x - w / 2f, y, w * charge, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
