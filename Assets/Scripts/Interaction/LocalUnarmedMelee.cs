using System.Collections;
using System.Collections.Generic;
using RootMotion;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Bare-handed fighting, Gang Beasts spirit: Punch (left click / LB,
    // hands empty) and Kick (F / RT, any time). R&D option (Boxing
    // Punches, on the R&D player variant): one button per hand (Punch =
    // left, PunchRight = right click / RB), a straighter and snappier punch
    // that the view follows — see CombatSettings; with Use Animations, the
    // guard and punches are animation clips instead (LocalUnarmedMelee.Animated.cs).
    // Otherwise: a punch goes out
    // on the click, no charge (the charged punch was dropped after testing);
    // punches can be spammed: a click during a punch chains the next one
    // with the other hand straight away, the previous arm easing back on its
    // own. A quick click on Kick is a simple kick; holding past the tap time
    // charges it, released to strike, and a charged kick is a Spartan kick
    // ("This is Sparta!", 300 / AC Odyssey): the target is knocked down and
    // launched far backwards, with a zoom on the attacker's view.
    //
    // Procedural, like LocalCueMelee: just before FBBIK solves
    // (solver.OnPreUpdate, after the Animator), the hand or foot effector is
    // pulled toward a guard / raised-knee pose while charging, then driven
    // along the strike path — a hook for the fists (out to the side, then
    // back to the centre, elbow out through a bend goal), straight out for
    // the foot (sole toward the target, knee forward, small lunge) — with
    // the torso twisting and the body leaning. Targets are rebuilt every
    // frame from the animated shoulder or hip and the camera, so the blow
    // follows the look and the walk.
    //
    // Hits go through PuppetMaster's own API (MuscleCollisionBroadcaster.Hit),
    // as for the cue: see MeleeHit.
    [RequireComponent(typeof(LocalPlayerHandController))]
    [RequireComponent(typeof(PlayerInput))]
    public partial class LocalUnarmedMelee : MonoBehaviour
    {
        // Tooltips are in French: they are read by the designer in the
        // Inspector.
        [Tooltip("Nom de l'action map du joueur dans l'asset d'input.")]
        [SerializeField] private string actionMapName = "Player";
        [Tooltip("Action du coup de poing (clic gauche / RB), mains vides uniquement.")]
        [SerializeField] private string punchActionName = "Punch";
        [Tooltip("Action du coup de pied (F / RT).")]
        [SerializeField] private string kickActionName = "Kick";

        // R&D (feature/deplacements-combat), ticked on the R&D player
        // variant only: boxing punches — one button per hand, a guard, a
        // snappier, straighter punch (CombatSettings). No charge: tried, then
        // dropped on 11/10.
        [Header("R&D Boxe")]
        [Tooltip("R&D : un bouton par main (Punch = poing gauche, Punch Right = poing droit). Le poing part à l'appui, presque droit, sec, le corps derrière, et la vue l'accompagne. Durées, forme, force et ressenti : CombatSettings. Décoché = poings en crochet qui alternent sur un seul bouton.")]
        [SerializeField] private bool boxingPunches;
        [Tooltip("Action du poing droit (clic droit / RB), avec les poings de boxe.")]
        [SerializeField] private string punchRightActionName = "PunchRight";

        [Tooltip("Caméra du joueur : les coups suivent son regard. Vide = la première caméra sous ce joueur.")]
        [SerializeField] private Transform viewTransform;
        [Tooltip("Full Body Biped IK du personnage (obligatoire : c'est lui qui bouge le poing et le pied). Vide = le premier trouvé sous ce joueur.")]
        [SerializeField] private FullBodyBipedIK fullBodyIK;
        [Tooltip("Caméra Cinemachine FPS du joueur, pour le zoom du coup de pied spartiate. Vide = la première dont le nom contient « FPS ».")]
        [SerializeField] private CinemachineCamera fpsCamera;

        // The punch and kick distances below are written for a human-sized
        // limb and scaled to this character's real arm/leg length (measured
        // on the skeleton): the cartoon rig's short arms made every target
        // out of reach, so FBBIK locked the arm straight. Targets are also
        // kept within Max Limb Extension of the limb length.
        [Header("Limb size")]
        [Tooltip("Longueur de bras (épaule → poignet, mètres) pour laquelle les distances du poing sont écrites. Elles sont mises à l'échelle du bras réel du personnage : bras 2× plus court = distances 2× plus courtes.")]
        [SerializeField] private float referenceArmLength = 0.6f;
        [Tooltip("Longueur de jambe (hanche → cheville, mètres) pour laquelle les distances du pied sont écrites. Mises à l'échelle de la jambe réelle.")]
        [SerializeField] private float referenceLegLength = 0.9f;
        [Tooltip("Distance maximale d'une cible depuis l'épaule ou la hanche, en part de la longueur du membre (1 = membre complètement tendu, verrouillé).")]
        [SerializeField, Range(0.5f, 1f)] private float maxLimbExtension = 0.95f;

        [Header("Tap or charge (seconds)")]
        [Tooltip("Coup de pied : relâché avant ce délai, c'est un coup simple ; au-delà, il se charge. (Le poing ne se charge pas.)")]
        [SerializeField] private float tapTime = 0.18f;
        [Tooltip("Coup de pied : durée de charge (après le délai du coup simple) pour atteindre la puissance maximale.")]
        [SerializeField] private float chargeTime = 0.7f;
        [Tooltip("Temps pour amener le poing en garde / le genou levé après l'appui. Le poing part dès que la garde est atteinte.")]
        [SerializeField] private float windupTime = 0.08f;

        // Three points, from the punching shoulder, in the body's horizontal
        // frame: start (guard), mid-course, end. Renamed on 29/09 (were
        // guardForward/…/hookOut…) on purpose, so the new defaults replace
        // the values already saved on the component.
        [Header("Punch — hook (metres, from the shoulder)")]
        [Tooltip("1. Départ (garde) : distance du poing devant l'épaule.")]
        [SerializeField] private float startForward = 0.12f;
        [Tooltip("1. Départ (garde) : hauteur du poing par rapport à l'épaule (positif = plus haut ; ~0,1 = à hauteur de mâchoire).")]
        [SerializeField] private float startHeight = 0.08f;
        [Tooltip("1. Départ (garde) : décalage du poing vers l'extérieur de l'épaule (négatif = vers le centre, devant la poitrine).")]
        [SerializeField] private float startOutward = 0.05f;
        [Tooltip("2. Mi-course : avancée, en part de la portée (petit = le poing reste sur le côté plus longtemps, grand = coup plus direct).")]
        [SerializeField, Range(0f, 1f)] private float midForward = 0.5f;
        [Tooltip("2. Mi-course : écart vers l'extérieur de l'épaule. Trop grand = le bras se tend sur le côté au lieu de rester plié.")]
        [SerializeField] private float midOutward = 0.2f;
        [Tooltip("2. Mi-course : hauteur du poing par rapport à l'épaule.")]
        [SerializeField] private float midHeight = 0.06f;
        [Tooltip("3. Arrivée : distance du poing devant les épaules.")]
        [SerializeField] private float punchReach = 0.6f;
        [Tooltip("3. Arrivée : hauteur du poing par rapport à l'épaule.")]
        [SerializeField] private float endHeight = 0f;
        [Tooltip("3. Arrivée : écart entre les deux poings bras tendus, en part de la largeur d'épaules. 1 = chaque poing droit devant son épaule (bras parallèles) ; < 1 = les poings se rapprochent du centre ; > 1 = plus écartés que les épaules. La largeur mesurée est dans la console (Debug Logs).")]
        [SerializeField] private float fistSpread = 1.1f;
        [Tooltip("Part du regard vers le haut/bas reprise par la hauteur du coup de poing (0 = toujours à hauteur d'épaule, 1 = suit complètement le regard).")]
        [SerializeField, Range(0f, 1f)] private float punchPitchFollow = 0.3f;
        [Tooltip("Coude levé et écarté pendant le crochet, bras plié à l'horizontale (0 = libre, 1 = tiré à fond).")]
        [SerializeField, Range(0f, 1f)] private float elbowOut = 0.6f;
        [Tooltip("Orientation du poing : 1 = jointures vers la cible, dos de la main vers le haut (paume vers le bas) ; 0 = orientation de l'animation (la main peut partir vers l'extérieur).")]
        [SerializeField, Range(0f, 1f)] private float fistAlign = 1f;
        [Tooltip("Rotation du poignet (degrés) autour de la direction du coup, en plus : 0 = paume vers le bas ; 90 = paume vers l'intérieur (poing vertical). Même sens pour les deux mains (en miroir).")]
        [SerializeField] private float fistRoll = 0f;
        [Tooltip("Rotation du buste (degrés) dans le coup de poing.")]
        [SerializeField] private float jabTorsoTwist = 12f;
        [Tooltip("Avancée de l'épaule qui frappe (mètres, bras de référence) au bout du coup : donne de l'allonge. Le poing va plus loin d'autant. 0 = l'épaule ne bouge pas.")]
        [SerializeField] private float shoulderReach = 0.1f;
        [Tooltip("Engagement du corps (mètres) dans le coup de poing.")]
        [SerializeField] private float punchLean = 0.12f;

        [Header("Kick (metres, from the hip, horizontal)")]
        [Tooltip("Distance du pied devant la hanche, jambe tendue.")]
        [SerializeField] private float kickReach = 0.95f;
        [Tooltip("Hauteur du pied par rapport à la hanche, jambe tendue (positif = plus haut que la hanche).")]
        [SerializeField] private float kickHeight = 0.1f;
        [Tooltip("Part du regard vers le haut/bas reprise par la hauteur du coup de pied (0 = hauteur fixe).")]
        [SerializeField, Range(0f, 1f)] private float kickPitchFollow = 0.4f;
        [Tooltip("Genou levé : distance du pied devant la hanche.")]
        [SerializeField] private float chamberForward = 0.12f;
        [Tooltip("Genou levé : hauteur du pied par rapport à la hanche.")]
        [SerializeField] private float chamberHeight = -0.2f;
        [Tooltip("Genou levé : hauteur ajoutée au pied à pleine charge (genou encore plus haut).")]
        [SerializeField] private float chamberRaise = 0.12f;
        [Tooltip("Genou vers l'avant pendant le coup de pied (0 = libre, 1 = tiré à fond).")]
        [SerializeField, Range(0f, 1f)] private float kneeForward = 0.6f;
        [Tooltip("Orientation du pied : 1 = semelle face à la cible, orteils vers le haut ; 0 = celle de l'animation.")]
        [SerializeField, Range(0f, 1f)] private float footSoleForward = 1f;
        [Tooltip("Recul du buste (mètres) pendant le coup de pied, pour l'équilibre.")]
        [SerializeField] private float kickLeanBack = 0.25f;
        [Tooltip("Pas en avant (mètres) pendant un coup de pied simple.")]
        [SerializeField] private float kickLunge = 0.2f;
        [Tooltip("Pas en avant (mètres) pendant un coup de pied à pleine charge.")]
        [SerializeField] private float spartanLunge = 0.55f;

        [Header("Timing (seconds)")]
        [Tooltip("Durée de la frappe d'un coup de poing.")]
        [SerializeField] private float jabStrikeTime = 0.09f;
        [Tooltip("Durée de la frappe d'un coup de pied simple.")]
        [SerializeField] private float kickStrikeTime = 0.12f;
        [Tooltip("Durée de la frappe d'un coup de pied chargé.")]
        [SerializeField] private float chargedKickStrikeTime = 0.15f;
        [Tooltip("Pause bras tendu avant le retour.")]
        [SerializeField] private float holdTime = 0.05f;
        [Tooltip("Pause jambe tendue avant le retour.")]
        [SerializeField] private float kickHoldTime = 0.12f;
        [Tooltip("Durée du retour à la pose normale (aussi celle du bras qui se relâche pendant un enchaînement).")]
        [SerializeField] private float recoverTime = 0.2f;
        [Tooltip("Délai minimal avant un nouveau coup une fois le précédent terminé (l'enchaînement des poings n'attend pas).")]
        [SerializeField] private float cooldown = 0.05f;

        [Header("Hit detection")]
        [Tooltip("Layers des colliders des muscles des ragdolls (PuppetMaster). Vide = layer « Ragdoll ».")]
        [SerializeField] private LayerMask muscleLayers;
        [Tooltip("Rayon (mètres) de la zone de touche autour du poing.")]
        [SerializeField] private float fistRadius = 0.15f;
        [Tooltip("Rayon (mètres) de la zone de touche autour du pied.")]
        [SerializeField] private float footRadius = 0.2f;
        [Tooltip("Tests de touche par image, pour qu'un coup rapide ne traverse pas la cible.")]
        [SerializeField] private int subSteps = 3;

        [Header("Punch strength")]
        [Tooltip("Déséquilibre (unpin PuppetMaster) d'un coup de poing. Repère : 5 suffit en général à faire tomber.")]
        [SerializeField] private float jabUnpin = 1.5f;
        [Tooltip("Force (newtons) d'un coup de poing.")]
        [SerializeField] private float jabForce = 500f;

        [Header("Kick strength")]
        [Tooltip("Déséquilibre d'un coup de pied simple (une poussée).")]
        [SerializeField] private float kickUnpin = 2.5f;
        [Tooltip("Force (newtons) d'un coup de pied simple.")]
        [SerializeField] private float kickForce = 900f;
        [Tooltip("Déséquilibre d'un coup de pied à pleine charge.")]
        [SerializeField] private float chargedKickUnpin = 8f;
        [Tooltip("Force (newtons) d'un coup de pied à pleine charge.")]
        [SerializeField] private float chargedKickForce = 2500f;
        [Tooltip("Part de la force dirigée vers le haut (soulève la cible).")]
        [SerializeField, Range(0f, 1f)] private float upwardBias = 0.15f;

        [Header("Spartan kick (charged kick)")]
        [Tooltip("À partir de cette charge, le coup de pied devient un coup de pied spartiate : chute garantie, projection, zoom.")]
        [SerializeField, Range(0f, 1f)] private float spartanMinCharge = 0.8f;
        [Tooltip("Vitesse (m/s) donnée à tout le corps de la cible, vers l'arrière.")]
        [SerializeField] private float launchSpeed = 9f;
        [Tooltip("Vitesse (m/s) vers le haut ajoutée à la projection.")]
        [SerializeField] private float launchLift = 2.5f;
        // No slow motion any more (removed on 30/09 at the user's request):
        // only the zoom on the attacker's view remains. The field keeps its
        // old name so the value set in the Inspector survives.
        [Tooltip("Zoom (degrés de champ de vision en moins) de la caméra de l'attaquant au contact du coup de pied spartiate. 0 = pas de zoom.")]
        [SerializeField] private float slowMoZoom = 12f;
        [Tooltip("Durée (secondes) du zoom du coup de pied spartiate, retour compris.")]
        [SerializeField] private float spartanZoomDuration = 0.5f;

        [Header("Feel")]
        [Tooltip("Durée (secondes, temps réel) du micro-ralenti à l'impact de chaque coup (spartiate compris). 0 = désactivé.")]
        [SerializeField] private float hitstopDuration = 0.05f;
        [Tooltip("Vitesse du temps pendant ce micro-ralenti.")]
        [SerializeField] private float hitstopTimeScale = 0.05f;
        [Tooltip("Affiche la jauge de charge provisoire dans la moitié d'écran du joueur.")]
        [SerializeField] private bool showChargeIndicator = true;

        [Header("Debug")]
        [Tooltip("Écrit dans la console chaque coup et chaque touche.")]
        [SerializeField] private bool debugLogs = true;

        private enum Move { Punch, Kick }
        private enum Phase { Idle, Windup, Strike, Hold, Recover }

        private LocalPlayerHandController handController;
        private LocalPoolAimController aimController;
        private LocalCueMelee cueMelee;
        private LocalPlayerRagdollController ragdoll;
        private LocalPoolPowerEffectReceiver effects;
        private CharacterController characterController;
        private PuppetMaster ownPuppet;
        private Camera viewCamera;
        private InputAction punchAction;
        private InputAction punchRightAction;
        private InputAction kickAction;
        private PlayerInput playerInput;
        private LocalPlayerLocomotion locomotion;   // R&D: view kick when a punch leaves

        private bool Boxing => boxingPunches && punchRightAction != null;

        // R&D guard: both fists up by the chin once punching starts, until
        // Guard Time after the last punch. Index 0 = left hand, 1 = right.
        private float guardWeight;
        private float guardUntil = -1f;
        private readonly Vector3[] guardPos = new Vector3[2];
        private readonly bool[] guardPosValid = new bool[2];
        private readonly bool[] guardDriven = new bool[2];
        private readonly Vector3[] handFingers = new Vector3[2], handBack = new Vector3[2];
        private readonly bool[] handMeasured = new bool[2];
        private readonly float[] fist = new float[2];    // how closed each hand is, 0..1
        private Animator bodyAnimator;

        // Read by LocalPlayerLocomotion: the arms stay stiff in guard.
        public bool IsGuarding => guardWeight > 0.01f;

        // A blow landed on another player: attacker and victim player
        // indices (-1 = not a joined player, e.g. a dummy), contact point,
        // strength 0..1 (jab ~0.3, full charge 1). GameFeel plays its Feel
        // sequence for it.
        public static event System.Action<int, int, Vector3, float> PlayerHit;

        private Phase phase = Phase.Idle;
        private Move move;
        private InputAction heldAction;
        private bool rightHand = true;   // punches alternate hands
        private bool charged;
        private float charge;
        private float phaseTime;
        private float cooldownLeft;
        private bool bufferedPunch;      // clicked during a punch's strike: chain as soon as it lands

        // Written in Update, read by the pre-solve callback.
        private float reach;        // 0 = guard / raised knee, 1 = end of the strike
        private float weight;       // effector weight
        private float twist;        // torso twist, degrees (positive = right shoulder forward)
        private float lean;         // metres along the flat view, positive = forward
        private float recoverTwist; // values at the end of the strike, eased out on recover
        private float recoverLean;

        // The limb doing the current blow.
        private IKEffector activeEffector;
        private FBIKChain activeChain;
        private Transform activeBendGoal;
        private Vector3 soleInFoot;      // kick: foot-space directions measured at the start
        private Vector3 toeInFoot;
        private Vector3 fingersInHand;   // punch: hand-space directions measured at the start
        private Vector3 backInHand;
        private bool limbMeasured;       // measured on the first pre-solve frame (animated pose)
        private Vector3 punchDirection;  // shoulder → end of the punch
        private Vector3 shoulderPush;    // how far the punching shoulder is pushed forward this frame

        // The previous arm while a chained punch is under way: eases back
        // on its own from where it ended.
        private IKEffector fadeEffector;
        private FBIKChain fadeChain;
        private Vector3 fadeTargetLocal; // in this player's root space, so it walks with him
        private float fadeWeight;

        // Bend goals for the elbows and the knee, created at runtime; the
        // chains' own settings are put back when a limb is released.
        private Transform leftElbowGoal, rightElbowGoal, kneeGoal;
        private readonly Dictionary<FBIKChain, (Transform goal, float weight)> savedBend = new Dictionary<FBIKChain, (Transform, float)>();

        private bool hasTarget;
        private Vector3 lastTarget;
        private Vector3 gizmoStart, gizmoControl, gizmoEnd;   // last hook path, for OnDrawGizmosSelected
        private bool limbLogged;
        private float lastShoulderWidth;
        private bool hitSomething;
        private readonly HashSet<PuppetMaster> hitThisBlow = new HashSet<PuppetMaster>();
        private readonly Collider[] overlapBuffer = new Collider[32];
        private Coroutine zoomRoutine;
        private float baseFov;
        private LocalHitGauge lastGauge;     // R&D: the last target's gauge, shown for a moment
        private float lastGaugeTime = -10f;

        // Read by LocalCueMelee and LocalPoolAimController so nothing else
        // starts driving the body mid-blow.
        public bool IsAttacking => phase != Phase.Idle || animPunching;

        private void Awake()
        {
            handController = GetComponent<LocalPlayerHandController>();
            aimController = GetComponent<LocalPoolAimController>();
            cueMelee = GetComponent<LocalCueMelee>();
            ragdoll = GetComponent<LocalPlayerRagdollController>();
            effects = GetComponent<LocalPoolPowerEffectReceiver>();
            characterController = GetComponent<CharacterController>();
            ownPuppet = GetComponentInChildren<PuppetMaster>(true);
            if (fullBodyIK == null) fullBodyIK = GetComponentInChildren<FullBodyBipedIK>(true);
            viewCamera = viewTransform != null ? viewTransform.GetComponent<Camera>() : GetComponentInChildren<Camera>(true);
            if (viewTransform == null && viewCamera != null) viewTransform = viewCamera.transform;
            if (fpsCamera == null)
                foreach (CinemachineCamera cam in GetComponentsInChildren<CinemachineCamera>(true))
                    if (cam.name.Contains("FPS")) { fpsCamera = cam; break; }

            leftElbowGoal = CreateGoal("MeleeElbowGoal_L");
            rightElbowGoal = CreateGoal("MeleeElbowGoal_R");
            kneeGoal = CreateGoal("MeleeKneeGoal");

            playerInput = GetComponent<PlayerInput>();
            locomotion = GetComponent<LocalPlayerLocomotion>();
            LocalCharacterAnimationController animControl = GetComponent<LocalCharacterAnimationController>();
            bodyAnimator = animControl != null && animControl.Animator != null ? animControl.Animator : GetComponentInChildren<Animator>(true);
            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            punchAction = map.FindAction(punchActionName, throwIfNotFound: true);
            punchRightAction = map.FindAction(punchRightActionName);
            kickAction = map.FindAction(kickActionName, throwIfNotFound: true);
            FindBlockAction(map);
            if (boxingPunches && punchRightAction == null)
                Debug.LogWarning($"[UnarmedMelee] {name} : action « {punchRightActionName} » introuvable, poings de boxe désactivés.", this);

            if (muscleLayers.value == 0) muscleLayers = LayerMask.GetMask("Ragdoll");
        }

        private Transform CreateGoal(string goalName)
        {
            Transform goal = new GameObject(goalName).transform;
            goal.SetParent(transform, false);
            return goal;
        }

        private void OnEnable()
        {
            if (fullBodyIK != null)
            {
                fullBodyIK.solver.OnPreUpdate += ApplyBody;
                fullBodyIK.solver.OnPostUpdate += CurlFingers;
            }
            HookLookAt(true);
        }

        private void OnDisable()
        {
            if (fullBodyIK != null)
            {
                fullBodyIK.solver.OnPreUpdate -= ApplyBody;
                fullBodyIK.solver.OnPostUpdate -= CurlFingers;
            }
            HookLookAt(false);
            ReleaseFade();
            EndBlow();
            guardWeight = 0f;
            IsBlocking = false;
            ReleaseGuard();
            ReleaseAnimatedLayer();
            RestoreFov();
        }

        private void Update()
        {
            if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;
            UpdateBlock(Time.deltaTime);
            UpdateStamina(Time.deltaTime);
            if (boxingPunches) UpdateGuard(Time.deltaTime);

            // Animated punches (R&D): the Animator layer plays them; nothing
            // else starts until the punch is done.
            bool animated = AnimatedPunches;
            if (animated)
            {
                UpdateAnimated(Time.deltaTime);
                if (animPunching) return;
            }
            else if (animPunching || LayerBusy) ReleaseAnimatedLayer();

            if (phase == Phase.Idle)
            {
                if (cooldownLeft > 0f) return;
                if (IsBlocking) return;   // no punch, no kick while guarding
                if (Boxing)
                {
                    // One button per hand.
                    if (punchAction.WasPressedThisFrame() && CanStart(Move.Punch))
                    {
                        if (animated) StartAnimatedPunch(false);
                        else { rightHand = false; Begin(Move.Punch, punchAction); }
                    }
                    else if (punchRightAction.WasPressedThisFrame() && CanStart(Move.Punch))
                    {
                        if (animated) StartAnimatedPunch(true);
                        else { rightHand = true; Begin(Move.Punch, punchRightAction); }
                    }
                    else if (kickAction.WasPressedThisFrame() && CanStart(Move.Kick)) Begin(Move.Kick, kickAction);
                }
                else if (punchAction.WasPressedThisFrame() && CanStart(Move.Punch)) Begin(Move.Punch, punchAction);
                else if (kickAction.WasPressedThisFrame() && CanStart(Move.Kick)) Begin(Move.Kick, kickAction);
                return;
            }

            // Knocked down, or something grabbed mid-punch (the
            // InteractionSystem now owns the hand): drop the blow.
            if (IsDown() || (move == Move.Punch && handController.HeldObject != null))
            {
                ReleaseFade();
                EndBlow();
                return;
            }

            // Spamming the punch: a click once the fist is on its way chains
            // the next punch with the other hand without waiting for this
            // arm to come back.
            // Boxing punches: it's the OTHER hand's button that chains (the
            // same hand can't punch again before its arm is back).
            InputAction chainAction = Boxing ? (rightHand ? punchAction : punchRightAction) : punchAction;
            if (move == Move.Punch && phase != Phase.Windup && chainAction.WasPressedThisFrame())
            {
                if (phase == Phase.Strike) bufferedPunch = true;
                else { ChainPunch(); return; }
            }

            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case Phase.Windup: UpdateWindup(); break;
                case Phase.Strike: UpdateStrike(); break;
                case Phase.Hold:
                    if (phaseTime >= (move == Move.Kick ? kickHoldTime : Boxing ? CombatSettings.Instance.holdTime : holdTime)) { phase = Phase.Recover; phaseTime = 0f; }
                    break;
                case Phase.Recover: UpdateRecover(); break;
            }
        }

        private bool CanStart(Move m)
        {
            if (fullBodyIK == null || viewTransform == null || IsDown()) return false;
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null && !rules.MatchStarted) return false;
            if (aimController != null && (aimController.IsAiming || aimController.IsPlacementViewActive)) return false;
            if (cueMelee != null && cueMelee.IsSwinging) return false;
            // Not while picking the cue up or letting it go: the pickup pins
            // the feet and drives the hands.
            if (handController.HandsMoving) return false;
            if (!HasStamina(m)) return false;   // R&D: too tired
            if (m == Move.Punch)
            {
                // Fists only with empty hands (with the cue, the click is
                // the cue swing; with an object, it's the throw).
                if (handController.HeldObject != null) return false;
                if (handController.CueBusy) return false;
            }
            return true;
        }

        private bool IsDown() => ragdoll != null && ragdoll.IsRagdollCameraActive;

        // Up, hands empty and not busy with the cue or aiming: what the R&D
        // guard (after punching, or held) needs.
        private bool HandsFreeForFists() =>
            !IsDown() && handController.HeldObject == null && !handController.CueBusy && !handController.HandsMoving
            && (aimController == null || (!aimController.IsAiming && !aimController.IsPlacementViewActive))
            && (cueMelee == null || !cueMelee.IsSwinging);

        private Vector3 FlatForward()
        {
            Vector3 flat = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up);
            return flat.sqrMagnitude < 1e-4f ? transform.forward : flat.normalized;
        }

        // ---- Phases ----

        private void Begin(Move m, InputAction action)
        {
            move = m;
            heldAction = action;
            charged = false;
            charge = 0f;
            reach = 0f;
            weight = 0f;
            twist = 0f;
            lean = 0f;
            hasTarget = false;
            hitSomething = false;
            bufferedPunch = false;
            limbLogged = false;
            hitThisBlow.Clear();
            StartCounterIfAny();
            SpendStamina(m);

            IKSolverFullBodyBiped solver = fullBodyIK.solver;
            if (m == Move.Kick)
            {
                activeEffector = solver.rightFootEffector;
                activeChain = solver.rightLegChain;
                activeBendGoal = kneeGoal;
            }
            else
            {
                activeEffector = rightHand ? solver.rightHandEffector : solver.leftHandEffector;
                activeChain = rightHand ? solver.rightArmChain : solver.leftArmChain;
                activeBendGoal = rightHand ? rightElbowGoal : leftElbowGoal;
            }
            if (!savedBend.ContainsKey(activeChain))
                savedBend[activeChain] = (activeChain.bendConstraint.bendGoal, activeChain.bendConstraint.weight);
            activeChain.bendConstraint.bendGoal = activeBendGoal;
            limbMeasured = false;

            phase = Phase.Windup;
            phaseTime = 0f;
        }

        // The hand's fingers (continuing the forearm) and back (facing out
        // from the body: an arm hanging at rest has the palm toward the
        // thigh), in the hand bone's own space — whatever the rig's bone
        // axes are — so the punch can turn the knuckles toward the target
        // with the back of the hand up. Without it the hand keeps the
        // animation's rotation, which turned the fist outward once the arm
        // was raised.
        private void MeasureHand(BipedReferences r) => MeasureHandFor(r, rightHand, out fingersInHand, out backInHand);

        // The foot's sole (down, while standing) and toes, in the foot
        // bone's own space — whatever the rig's bone axes are — so the kick
        // can turn the sole toward the target with the toes up.
        private void MeasureFoot()
        {
            Transform foot = fullBodyIK.references.rightFoot;
            if (foot == null) { soleInFoot = toeInFoot = Vector3.zero; return; }
            soleInFoot = foot.InverseTransformDirection(-transform.up);
            Vector3 toe = foot.childCount > 0 ? foot.GetChild(0).position - foot.position : transform.forward;
            toeInFoot = foot.InverseTransformDirection(toe.normalized);
        }

        private bool ChainPunch()
        {
            bufferedPunch = false;
            if (!CanStart(Move.Punch)) return false;
            // R&D: the arm that just punched goes back to the guard (instead
            // of fading to its hanging pose).
            if (Boxing) HandToGuard();
            else StartFade();
            AdvanceHand();
            Begin(Move.Punch, Boxing && rightHand ? punchRightAction : punchAction);
            return true;
        }

        private void UpdateWindup()
        {
            float windup = move == Move.Punch && Boxing ? CombatSettings.Instance.windupTime : windupTime;
            weight = windup <= 0f ? 1f : Mathf.Clamp01(phaseTime / windup);

            // Punches don't charge: the fist goes out as soon as it's up in
            // guard, held or not.
            bool strikeNow;
            if (move == Move.Punch && Boxing)
            {
                // Leaves on the press (after the short guard), never waits
                // for the release: that wait read as lag.
                twist = 0f;
                lean = 0f;
                strikeNow = phaseTime >= windup;
            }
            else if (move == Move.Punch)
            {
                twist = 0f;
                lean = 0f;
                strikeNow = phaseTime >= windupTime || !heldAction.IsPressed();
            }
            else
            {
                charged = phaseTime >= tapTime;
                charge = charged ? Mathf.Clamp01((phaseTime - tapTime) / Mathf.Max(0.01f, chargeTime)) : 0f;
                twist = 0f;
                lean = -kickLeanBack * (0.4f + 0.6f * charge) * weight;
                // A tap released before the knee is fully up kicks from
                // wherever the foot got to: no waiting.
                strikeNow = !heldAction.IsPressed();
            }

            if (strikeNow)
            {
                phase = Phase.Strike;
                phaseTime = 0f;
                if (IsSpartan) SpendSpartanStamina();
                // The view goes with the punch as it leaves (R&D).
                if (move == Move.Punch && Boxing && locomotion != null)
                {
                    CombatSettings cfg = CombatSettings.Instance;
                    locomotion.KickView((rightHand ? -1f : 1f) * cfg.throwViewRoll, cfg.throwViewPush, cfg.throwViewTime);
                }
                if (debugLogs)
                    Debug.Log($"[UnarmedMelee] {name}: {Describe()} (charge {charge:F2})", this);
            }
        }

        private void UpdateStrike()
        {
            float t = Mathf.Clamp01(phaseTime / StrikeTime());
            float previousReach = reach;
            bool snappy = move == Move.Punch && Boxing;
            // Fast out, settles on the target (R&D punch: sharper, most of
            // the distance in the first third).
            reach = snappy ? 1f - (1f - t) * (1f - t) * (1f - t) : 1f - (1f - t) * (1f - t);
            weight = 1f;

            if (move == Move.Punch)
            {
                float side = rightHand ? 1f : -1f;
                if (snappy)
                {
                    CombatSettings cfg = CombatSettings.Instance;
                    twist = side * cfg.torsoTwist * reach;
                    lean = cfg.bodyLean * reach;
                }
                else
                {
                    twist = side * jabTorsoTwist * reach;
                    lean = punchLean * 0.4f * reach;
                }
            }
            else
            {
                lean = -kickLeanBack * Mathf.Lerp(0.4f + 0.6f * charge, 1f, reach);
                // A step into the kick: reaches further, and sells the push.
                float lunge = charged ? Mathf.Lerp(kickLunge, spartanLunge, charge) : kickLunge;
                if (characterController != null && characterController.enabled)
                    characterController.Move(FlatForward() * ((reach - previousReach) * lunge));
            }

            if (t >= 1f)
            {
                if (!hitSomething && debugLogs) Debug.Log($"[UnarmedMelee] {name}: {Describe()} missed", this);
                if (bufferedPunch && ChainPunch()) return;
                recoverTwist = twist;
                recoverLean = lean;
                phase = Phase.Hold;
                phaseTime = 0f;
            }
        }

        private void UpdateRecover()
        {
            float t = Mathf.Clamp01(phaseTime / RecoverTime());
            float left = 1f - t * t * (3f - 2f * t);
            weight = left;
            // R&D: the fist comes back along its path to the guard, the arm
            // staying driven while the guard is up.
            if (move == Move.Punch && Boxing)
            {
                reach = left;
                weight = Mathf.Max(left, guardWeight);
            }
            twist = recoverTwist * left;
            lean = recoverLean * left;
            if (t >= 1f) EndBlow();
        }

        private void EndBlow()
        {
            if (phase != Phase.Idle && move == Move.Punch && Boxing)
            {
                guardUntil = Time.time + CombatSettings.Instance.guardTime;
                HandToGuard();
            }
            ReleaseLimb(activeEffector, activeChain);
            activeEffector = null;
            activeChain = null;
            if (phase != Phase.Idle)
            {
                cooldownLeft = cooldown;
                if (move == Move.Punch) AdvanceHand();
            }
            phase = Phase.Idle;
            weight = 0f;
            bufferedPunch = false;
        }

        // R&D: the punching arm is handed over to the guard from where its
        // fist is, so it eases back up instead of dropping. The active limb
        // is cleared without releasing it: the guard drives it from now on.
        private void HandToGuard()
        {
            if (activeEffector == null || guardWeight <= 0f) return;
            int i = rightHand ? 1 : 0;
            guardPos[i] = activeEffector.position;   // where the fist was driven this frame
            guardPosValid[i] = true;
            guardDriven[i] = true;
            activeEffector = null;
            activeChain = null;
        }

        // Punches strictly alternate hands — a chained punch must never
        // reuse the arm that is still easing back.
        private void AdvanceHand()
        {
            rightHand = !rightHand;
        }

        // Hands the current arm over to the fade: it eases back from where
        // it is while the other arm punches.
        private void StartFade()
        {
            ReleaseFade();
            if (activeEffector == null) return;
            fadeEffector = activeEffector;
            fadeChain = activeChain;
            fadeWeight = weight;
            fadeTargetLocal = transform.InverseTransformPoint(hasTarget ? lastTarget : activeEffector.position);
            activeEffector = null;
            activeChain = null;
        }

        private void ReleaseFade()
        {
            if (fadeEffector != null) ReleaseLimb(fadeEffector, fadeChain);
            fadeEffector = null;
            fadeChain = null;
        }

        private void ReleaseLimb(IKEffector effector, FBIKChain chain)
        {
            if (effector != null)
            {
                effector.positionWeight = 0f;
                effector.rotationWeight = 0f;
            }
            if (chain != null && savedBend.TryGetValue(chain, out var saved))
            {
                chain.bendConstraint.bendGoal = saved.goal;
                chain.bendConstraint.weight = saved.weight;
                savedBend.Remove(chain);
            }
        }

        private float StrikeTime()
        {
            if (move == Move.Punch) return Boxing ? CombatSettings.Instance.strikeTime : jabStrikeTime;
            return charged ? chargedKickStrikeTime : kickStrikeTime;
        }

        // The R&D punch comes back faster (the kick keeps its own timing).
        private float RecoverTime() =>
            move == Move.Punch && Boxing ? CombatSettings.Instance.recoverTime : recoverTime;

        private bool IsSpartan => move == Move.Kick && charged && charge >= spartanMinCharge;

        private string Describe()
        {
            if (move == Move.Kick) return IsSpartan ? "SPARTAN KICK" : charged ? "charged kick" : "kick";
            return $"{(charged ? "charged punch" : "punch")} ({(rightHand ? "right" : "left")})";
        }

        // ---- Body (just before FBBIK solves) ----

        private void ApplyBody()
        {
            if (viewTransform == null) return;
            if (fadeEffector != null) ApplyFade();
            BipedReferences r = fullBodyIK.references;
            Vector3 flatForward = FlatForward();
            // Animated punches: the guard is the clip's, not the IK's.
            if (boxingPunches && !AnimatedPunches) ApplyGuard(r, flatForward);
            if (AnimatedPunches)
            {
                UpdateLookAtClamp();
                // Normally already done before the head's Look At IK solved.
                if (punchAimFrame != Time.frameCount) ApplyPunchAim(r);
            }
            if (phase == Phase.Idle || activeEffector == null) return;

            // Measured here, not in Begin: in Update the bones still hold
            // last frame's IK result; here they hold this frame's animation.
            if (!limbMeasured)
            {
                if (move == Move.Kick) MeasureFoot();
                else MeasureHand(r);
                limbMeasured = true;
            }

            // Torso first, so the shoulder the fist starts from turns with it.
            if (twist != 0f && r.spine != null && r.spine.Length > 0)
            {
                Quaternion perBone = Quaternion.AngleAxis(twist / r.spine.Length, Vector3.up);
                foreach (Transform bone in r.spine)
                    if (bone != null) bone.rotation = perBone * bone.rotation;
            }

            fullBodyIK.solver.bodyEffector.positionOffset += flatForward * lean;

            Vector3 target;
            if (move == Move.Punch)
            {
                target = PunchTarget(r, flatForward, out Vector3 elbow);
                activeBendGoal.position = elbow;
                activeChain.bendConstraint.weight = weight * elbowOut * reach;
                // positionOffset is cleared by FBBIK after each solve: added
                // every frame, nothing to undo.
                IKEffector shoulderEffector = rightHand ? fullBodyIK.solver.rightShoulderEffector : fullBodyIK.solver.leftShoulderEffector;
                shoulderEffector.positionOffset += shoulderPush;
                if (fingersInHand != Vector3.zero && fistAlign > 0f)
                {
                    // Knuckles along the punch, back of the hand up (then
                    // Fist Roll, mirrored for the left hand).
                    float side = rightHand ? 1f : -1f;
                    // R&D: vertical in guard, turning palm down on the way.
                    float roll = fistRoll + (Boxing ? CombatSettings.Instance.corkscrew * (1f - reach) : 0f);
                    Quaternion measured = Quaternion.LookRotation(fingersInHand, backInHand);
                    Quaternion wanted = Quaternion.AngleAxis(side * roll, punchDirection) * Quaternion.LookRotation(punchDirection, Vector3.up);
                    activeEffector.rotation = wanted * Quaternion.Inverse(measured);
                    activeEffector.rotationWeight = weight * fistAlign;
                }
            }
            else
            {
                target = KickTarget(r, flatForward, out Vector3 knee);
                activeBendGoal.position = knee;
                activeChain.bendConstraint.weight = weight * kneeForward;
                if (soleInFoot != Vector3.zero)
                {
                    // Sole toward the target, toes up: rotation taking the
                    // foot's measured sole/toe directions onto forward/up.
                    Quaternion measured = Quaternion.LookRotation(soleInFoot, toeInFoot);
                    activeEffector.rotation = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Inverse(measured);
                    activeEffector.rotationWeight = weight * footSoleForward * Mathf.Lerp(0.4f, 1f, reach);
                }
            }
            activeEffector.position = target;
            activeEffector.positionWeight = weight;

            if (phase == Phase.Strike)
            {
                if (hasTarget) DetectHits(lastTarget, target, flatForward);
                lastTarget = target;
                hasTarget = true;
            }
        }

        // ---- R&D guard and closed fists ----

        // Up while punching, and for Guard Time after the last punch — as
        // long as the hands are free and the player is up.
        private void UpdateGuard(float dt)
        {
            CombatSettings cfg = CombatSettings.Instance;
            bool allowed = Boxing && cfg.guardTime > 0f && HandsFreeForFists();
            bool want = allowed && ((phase != Phase.Idle && move == Move.Punch) || animPunching || IsBlocking || Time.time < guardUntil);
            float time = want ? cfg.guardRaiseTime : cfg.guardLowerTime;
            guardWeight = allowed ? Mathf.MoveTowards(guardWeight, want ? 1f : 0f, dt / Mathf.Max(0.01f, time)) : 0f;
            if (guardWeight <= 0f) ReleaseGuard();
        }

        // Each hand not punching (and not easing back from a hook) is held
        // in guard by the chin, fist vertical, elbow down.
        private void ApplyGuard(BipedReferences r, Vector3 flatForward)
        {
            IKSolverFullBodyBiped solver = fullBodyIK.solver;
            CombatSettings cfg = CombatSettings.Instance;
            for (int i = 0; i < 2; i++)
            {
                bool right = i == 1;
                IKEffector effector = right ? solver.rightHandEffector : solver.leftHandEffector;
                bool active = phase != Phase.Idle && move == Move.Punch && effector == activeEffector;
                fist[i] = active ? Mathf.Max(weight, guardWeight) : guardWeight;
                if (active || effector == fadeEffector || guardWeight <= 0f) continue;

                FBIKChain chain = right ? solver.rightArmChain : solver.leftArmChain;
                if (!handMeasured[i])
                {
                    MeasureHandFor(r, right, out handFingers[i], out handBack[i]);
                    handMeasured[i] = true;
                }
                Vector3 point = GuardPoint(r, right, flatForward, out Vector3 elbow);
                guardPos[i] = guardPosValid[i] ? Vector3.Lerp(guardPos[i], point, 1f - Mathf.Exp(-25f * Time.deltaTime)) : point;
                guardPosValid[i] = true;

                if (!savedBend.ContainsKey(chain)) savedBend[chain] = (chain.bendConstraint.bendGoal, chain.bendConstraint.weight);
                Transform goal = right ? rightElbowGoal : leftElbowGoal;
                goal.position = elbow;
                chain.bendConstraint.bendGoal = goal;
                chain.bendConstraint.weight = guardWeight * 0.8f;
                effector.position = guardPos[i];
                effector.positionWeight = guardWeight;
                if (handFingers[i] != Vector3.zero && fistAlign > 0f)
                {
                    float side = right ? 1f : -1f;
                    Quaternion measured = Quaternion.LookRotation(handFingers[i], handBack[i]);
                    Quaternion wanted = Quaternion.AngleAxis(side * (fistRoll + cfg.corkscrew), flatForward) * Quaternion.LookRotation(flatForward, Vector3.up);
                    effector.rotation = wanted * Quaternion.Inverse(measured);
                    effector.rotationWeight = guardWeight * fistAlign;
                }
                guardDriven[i] = true;
            }
        }

        // The guard lets go of both hands (only those it was driving).
        private void ReleaseGuard()
        {
            if (fullBodyIK == null) return;
            IKSolverFullBodyBiped solver = fullBodyIK.solver;
            for (int i = 0; i < 2; i++)
            {
                fist[i] = phase != Phase.Idle && move == Move.Punch && (i == 1) == rightHand ? weight : 0f;
                if (!guardDriven[i]) continue;
                IKEffector effector = i == 1 ? solver.rightHandEffector : solver.leftHandEffector;
                if (effector == activeEffector) { guardDriven[i] = false; continue; }
                ReleaseLimb(effector, i == 1 ? solver.rightArmChain : solver.leftArmChain);
                guardDriven[i] = false;
                guardPosValid[i] = false;
                handMeasured[i] = false;
            }
        }

        // Fist by the chin: the punch's start point (Start Forward / Height /
        // Outward), elbow down and a little out.
        private Vector3 GuardPoint(BipedReferences r, bool right, Vector3 flatForward, out Vector3 elbow)
        {
            Transform shoulderBone = right ? r.rightUpperArm : r.leftUpperArm;
            Vector3 shoulder = shoulderBone != null ? shoulderBone.position : viewTransform.position - Vector3.up * 0.2f;
            float armLength = LimbLength(shoulderBone, right ? r.rightForearm : r.leftForearm, right ? r.rightHand : r.leftHand, referenceArmLength);
            float k0 = armLength / Mathf.Max(0.01f, referenceArmLength);
            Vector3 f = flatForward * k0, u = Vector3.up * k0, rt = Vector3.Cross(Vector3.up, flatForward) * k0;
            float side = right ? 1f : -1f;
            elbow = shoulder + rt * (side * 0.15f) - u * 0.35f + f * 0.05f;
            return WithinReach(shoulder, shoulder + f * startForward + u * startHeight + rt * (side * startOutward), armLength * maxLimbExtension);
        }

        private void MeasureHandFor(BipedReferences r, bool right, out Vector3 fingers, out Vector3 back)
        {
            Transform hand = right ? r.rightHand : r.leftHand;
            Transform forearm = right ? r.rightForearm : r.leftForearm;
            if (hand == null || forearm == null) { fingers = back = Vector3.zero; return; }
            fingers = hand.InverseTransformDirection((hand.position - forearm.position).normalized);
            back = hand.InverseTransformDirection(transform.right * (right ? 1f : -1f));
        }

        // After FBBIK: the fingers of each closed hand fold toward the palm
        // (worked out from the bones' positions, whatever the rig's axes).
        // The Animator writes the fingers every frame, so nothing builds up.
        private void CurlFingers()
        {
            // Animated punches: the clips already close the hands.
            if (!Boxing || bodyAnimator == null || !bodyAnimator.isHuman || AnimatedPunches) return;
            CombatSettings cfg = CombatSettings.Instance;
            for (int i = 0; i < 2; i++)
                if (fist[i] > 0.001f) CurlHand(i == 1, fist[i], cfg);
        }

        private void CurlHand(bool right, float amount, CombatSettings cfg)
        {
            float f = cfg.fingerCurl * amount;
            HandPose.Curl(bodyAnimator, right, f, f, f, f, cfg.thumbCurl * amount);
        }

        private void ApplyFade()
        {
            fadeWeight -= Time.deltaTime / Mathf.Max(0.01f, Boxing ? CombatSettings.Instance.recoverTime : recoverTime);
            if (fadeWeight <= 0f) { ReleaseFade(); return; }
            fadeEffector.position = transform.TransformPoint(fadeTargetLocal);
            fadeEffector.positionWeight = fadeWeight * fadeWeight * (3f - 2f * fadeWeight);
            fadeChain.bendConstraint.weight = fadeEffector.positionWeight * elbowOut;
            fadeEffector.rotationWeight = fadeEffector.positionWeight * fistAlign;
        }

        // Hook: from a boxing guard (fist raised beside the jaw, cocked
        // further back while charging), out a little to the side at
        // mid-course, then forward to finish in front of its own shoulder
        // (quadratic Bézier: start, control, end). The windup brings the
        // fist up from the hanging arm to the guard.
        // Laid out in the body's horizontal frame (flat view forward, world
        // up), not the camera's: with the camera frame, looking slightly
        // down at a close opponent tilted the whole hook down to the belly.
        // The look only nudges the height (Punch Pitch Follow). Where the
        // fists end sideways is Fist Spread (in shoulder widths) — ending
        // near the chest's midline made the arms cross the body diagonally.
        private Vector3 PunchTarget(BipedReferences r, Vector3 flatForward, out Vector3 elbow)
        {
            Transform shoulderBone = rightHand ? r.rightUpperArm : r.leftUpperArm;
            Vector3 shoulder = shoulderBone != null ? shoulderBone.position : viewTransform.position - Vector3.up * 0.2f;
            float side = rightHand ? 1f : -1f;
            // Scale to this character's arm: every distance below is written
            // for Reference Arm Length.
            float armLength = LimbLength(shoulderBone, rightHand ? r.rightForearm : r.leftForearm, rightHand ? r.rightHand : r.leftHand, referenceArmLength);
            float k0 = armLength / Mathf.Max(0.01f, referenceArmLength);
            Vector3 f = flatForward * k0, u = Vector3.up * k0, rt = Vector3.Cross(Vector3.up, flatForward) * k0;

            // Reach: the punching shoulder goes forward with the blow (FBBIK
            // shoulder effector, see ApplyBody). Every point is measured from
            // the pushed shoulder, so the fist goes further by the same amount
            // instead of the arm just bending more.
            float drive = Boxing ? CombatSettings.Instance.shoulderDrive : shoulderReach;
            shoulderPush = f * (drive * reach * weight);
            shoulder += shoulderPush;
            float pitchLift = viewTransform.forward.y * punchReach * punchPitchFollow;

            Vector3 guard = shoulder + f * startForward + u * startHeight + rt * (side * startOutward);
            Vector3 control = shoulder + f * (punchReach * midForward) + rt * (side * midOutward) + u * (midHeight + pitchLift * midForward);

            // End: the two fists Fist Spread × shoulder width apart, measured
            // on the rig (not scaled: it's already this character's size),
            // around the middle of the shoulders.
            Vector3 rtUnit = Vector3.Cross(Vector3.up, flatForward);
            Vector3 center = shoulder;
            float shoulderWidth = 0.3f;
            if (r.leftUpperArm != null && r.rightUpperArm != null)
            {
                center = (r.leftUpperArm.position + r.rightUpperArm.position) * 0.5f;
                shoulderWidth = Vector3.Distance(r.leftUpperArm.position, r.rightUpperArm.position);
            }
            float spread = Boxing ? CombatSettings.Instance.fistSpread : fistSpread;
            Vector3 end = center + rtUnit * (side * shoulderWidth * 0.5f * spread) + f * punchReach + u * (endHeight + pitchLift);
            lastShoulderWidth = shoulderWidth;
            punchDirection = (end - shoulder).sqrMagnitude > 1e-6f ? (end - shoulder).normalized : flatForward;

            // R&D: from a straight punch (control on the guard → end line)
            // to the full hook, by Hook Amount.
            float hook = Boxing ? CombatSettings.Instance.hookAmount : 1f;
            if (hook < 1f) control = Vector3.Lerp((guard + end) * 0.5f, control, hook);

            float maxDistance = armLength * maxLimbExtension;
            guard = WithinReach(shoulder, guard, maxDistance);
            control = WithinReach(shoulder, control, maxDistance);
            end = WithinReach(shoulder, end, maxDistance);

            gizmoStart = guard;
            gizmoControl = control;
            gizmoEnd = end;
            LogLimbOnce("arm", armLength, k0);

            // Elbow out to the side at about shoulder height: a hook's arm
            // stays bent and horizontal, the torso turn does the work. A
            // straight punch keeps the elbow down and slightly out.
            float s = reach, k = 1f - s;
            Vector3 hookElbow = shoulder + rt * (side * 0.4f) - u * 0.05f - f * 0.05f;
            Vector3 straightElbow = shoulder + rt * (side * 0.2f) - u * 0.3f;
            elbow = Vector3.Lerp(straightElbow, hookElbow, hook);
            return WithinReach(shoulder, k * k * guard + 2f * k * s * control + s * s * end, maxDistance);
        }

        // Scene view, player selected, while a punch is under way: the hook
        // path (yellow), its three points (start, mid-course, end) and the
        // fist target (red) — to tune the Punch settings by eye.
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || phase == Phase.Idle || move != Move.Punch) return;
            Gizmos.color = Color.yellow;
            Vector3 previous = gizmoStart;
            for (int i = 1; i <= 16; i++)
            {
                float s = i / 16f, k = 1f - s;
                Vector3 point = k * k * gizmoStart + 2f * k * s * gizmoControl + s * s * gizmoEnd;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.DrawWireSphere(gizmoStart, 0.03f);
            Gizmos.DrawWireSphere(gizmoControl, 0.03f);
            Gizmos.DrawWireSphere(gizmoEnd, 0.03f);
            if (activeEffector != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(activeEffector.position, 0.04f);
            }
        }

        private Vector3 KickTarget(BipedReferences r, Vector3 flatForward, out Vector3 knee)
        {
            Vector3 hip = r.rightThigh != null ? r.rightThigh.position : transform.position + Vector3.up * 0.9f;
            // Scale to this character's leg: distances written for Reference Leg Length.
            float legLength = LimbLength(r.rightThigh, r.rightCalf, r.rightFoot, referenceLegLength);
            float k0 = legLength / Mathf.Max(0.01f, referenceLegLength);
            Vector3 f = flatForward * k0, u = Vector3.up * k0;

            Vector3 chamber = hip + f * chamberForward + u * (chamberHeight + chamberRaise * charge);
            float height = kickHeight + viewTransform.forward.y * kickReach * kickPitchFollow;
            Vector3 extended = hip + f * kickReach + u * height;
            float maxDistance = legLength * maxLimbExtension;
            chamber = WithinReach(hip, chamber, maxDistance);
            extended = WithinReach(hip, extended, maxDistance);
            LogLimbOnce("leg", legLength, k0);

            knee = hip + f * 0.7f + u * 0.3f;
            return WithinReach(hip, Vector3.Lerp(chamber, extended, reach), maxDistance);
        }

        // Upper + lower segment lengths from the animated bones, or the
        // reference length if the rig references are missing.
        private static float LimbLength(Transform root, Transform middle, Transform end, float fallback)
        {
            if (root == null || middle == null || end == null) return fallback;
            return Vector3.Distance(root.position, middle.position) + Vector3.Distance(middle.position, end.position);
        }

        private static Vector3 WithinReach(Vector3 origin, Vector3 point, float maxDistance)
        {
            Vector3 offset = point - origin;
            return offset.magnitude > maxDistance ? origin + offset.normalized * maxDistance : point;
        }

        // Once per blow (Debug Logs): the measured limb and the scale used,
        // to check the targets fit this character.
        private void LogLimbOnce(string limb, float length, float scale)
        {
            if (!debugLogs || limbLogged) return;
            limbLogged = true;
            Debug.Log($"[UnarmedMelee] {name}: {limb} length {length:F2} m → distances ×{scale:F2} (reference {(limb == "arm" ? referenceArmLength : referenceLegLength):F2} m), targets kept within {maxLimbExtension:P0} of it" +
                      (limb == "arm" ? $"; shoulder width {lastShoulderWidth:F2} m → fists {lastShoulderWidth * fistSpread:F2} m apart at the end" : ""), this);
        }

        // ---- Hits ----

        private void DetectHits(Vector3 from, Vector3 to, Vector3 flatForward)
        {
            float radius = move == Move.Punch ? fistRadius : footRadius;
            int steps = Mathf.Max(1, subSteps);
            Vector3 previous = from;
            for (int s = 1; s <= steps; s++)
            {
                Vector3 point = Vector3.Lerp(from, to, s / (float)steps);
                int count = Physics.OverlapCapsuleNonAlloc(previous, point, radius, overlapBuffer, muscleLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) TryHit(overlapBuffer[i], point, to - from, flatForward);
                previous = point;
            }
        }

        private void TryHit(Collider hit, Vector3 contact, Vector3 travel, Vector3 flatForward)
        {
            if (!MeleeHit.TryGetTarget(hit, transform, ownPuppet, hitThisBlow, out MuscleCollisionBroadcaster broadcaster, out PuppetMaster target)) return;
            hitSomething = true;

            float unpin, force;
            CombatSettings cfg = CombatSettings.Instance;
            if (move == Move.Punch && Boxing)
            {
                unpin = cfg.unpin;
                force = cfg.force;
            }
            else if (move == Move.Punch)
            {
                unpin = jabUnpin;
                force = jabForce;
            }
            else
            {
                unpin = charged ? Mathf.Lerp(kickUnpin, chargedKickUnpin, charge) : kickUnpin;
                force = charged ? Mathf.Lerp(kickForce, chargedKickForce, charge) : kickForce;
            }

            // A hook pushes where the fist is travelling (sideways-in, then
            // forward); a kick pushes flat, away from the kicker.
            Vector3 push = move == Move.Punch && travel.sqrMagnitude > 1e-6f ? travel.normalized : move == Move.Punch ? viewTransform.forward : flatForward;
            Vector3 forceDirection = Vector3.Lerp(push, Vector3.up, upwardBias).normalized;
            Vector3 point = hit.ClosestPoint(contact);
            BehaviourPuppet puppet = MeleeHit.FindPuppetBehaviour(target);
            bool spartan = IsSpartan && puppet != null;

            // R&D hit gauge: filled before the force goes in, so a blow that
            // only staggers already has the steadier balance.
            LocalHitGauge gauge = Boxing && cfg.useHitGauge ? LocalHitGauge.For(target) : null;
            bool knockedOut = false, blocked = false, brokeGuard = false;
            bool spartanKnockOut = spartan;
            HitZone zone = HitZone.Body;
            if (gauge != null)
            {
                // The zone comes from the muscle hit (head, body, legs).
                if (broadcaster.muscleIndex >= 0 && broadcaster.muscleIndex < target.muscles.Length)
                    zone = LocalHitGauge.ZoneOf(target.muscles[broadcaster.muscleIndex].props.group);

                // The victim's guard (R&D): a blocked blow fills the gauge
                // and pushes the body far less. The blow that empties the
                // block meter is still blocked; it only breaks the guard, and
                // from then on blows go to the hit gauge as usual — the first
                // version also dazed (gauge pushed to 70 %), so the next blow
                // knocked out: removed on 12/10 at the user's request.
                LocalUnarmedMelee victim = gauge.GetComponent<LocalUnarmedMelee>();
                if (victim != null && victim != this) blocked = victim.TryBlock(forceDirection, zone, move == Move.Kick, spartan, out brokeGuard);
                // A Spartan kick taken on the guard only breaks it: no K.O.,
                // no launch (CombatSettings.Spartan Breaks Guard Only).
                if (blocked) spartanKnockOut = false;

                float damage = move == Move.Punch ? cfg.punchDamage : cfg.kickDamage;
                if (counterPunch && move == Move.Punch) { damage *= cfg.counterDamage; force *= cfg.counterDamage; }
                if (move == Move.Punch && Tired) { damage *= cfg.tiredDamage; force *= cfg.tiredDamage; }
                if (blocked)
                {
                    damage *= move == Move.Kick ? cfg.blockedKickDamage : cfg.blockedDamage;
                    unpin *= cfg.blockedForce;
                    force *= cfg.blockedForce;
                }
                knockedOut = gauge.TakeHit(damage, spartanKnockOut, zone, forceDirection, blocked);
                if (brokeGuard && !knockedOut) gauge.PlayGuardBreakReaction(forceDirection);
                lastGauge = gauge;
                lastGaugeTime = Time.time;
            }

            broadcaster.Hit(unpin, forceDirection * force, point);
            if (spartan && gauge == null) puppet.SetState(BehaviourPuppet.State.Unpinned);

            if (debugLogs)
                Debug.Log($"[UnarmedMelee] {name} HIT {target.name} on '{hit.name}' — {Describe()}, charge {charge:F2}, unpin {unpin:F1}, force {force:F0}" +
                          (gauge != null ? $", zone {zone}, gauge {gauge.Fill:P0}{(knockedOut ? " → K.O." : "")}" : "") +
                          (counterPunch && move == Move.Punch ? " [COUNTER]" : "") + (blocked ? " [BLOCKED]" : "") + (brokeGuard ? " [GUARD BROKEN]" : "") +
                          (spartanKnockOut ? " → knockdown forced (Spartan kick)" : spartan ? " → Spartan kick on the guard" : ""), this);

            float strength = move == Move.Kick ? (spartanKnockOut ? 1f : 0.5f + 0.4f * charge) : 0.4f;
            if (blocked) strength *= 0.3f;
            if (Boxing && move == Move.Punch)
                LocalPoolPowerEffectReceiver.PlayImpact(OwnIndex(), 0.018f * cfg.hitShake, 0.1f, Color.clear, 0f, 0f);
            else if (effects != null) effects.PlayShotFeedback();
            PlayerHit?.Invoke(OwnIndex(), VictimIndex(target), point, strength);
            if (spartan)
            {
                if (spartanKnockOut) StartCoroutine(Launch(target, flatForward * launchSpeed + Vector3.up * launchLift));
                if (fpsCamera != null && slowMoZoom > 0f)
                {
                    if (zoomRoutine != null) { StopCoroutine(zoomRoutine); RestoreFov(); }
                    zoomRoutine = StartCoroutine(SpartanZoom());
                }
            }
            if (hitstopDuration > 0f)
                StartCoroutine(MeleeHit.Hitstop(hitstopDuration, hitstopTimeScale));
        }

        // This player's index in the match (the side it plays in solo).
        private int OwnIndex()
        {
            if (playerInput == null) return -1;
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules != null ? rules.GetEffectivePlayerIndex(playerInput.playerIndex) : playerInput.playerIndex;
        }

        // The player hit, or -1 for a body that isn't a joined player (dummy).
        private static int VictimIndex(PuppetMaster target)
        {
            PlayerInput victim = target != null ? target.GetComponentInParent<PlayerInput>() : null;
            return victim != null && victim.enabled ? victim.playerIndex : -1;
        }

        // Throws the whole ragdoll along velocity. Waits one physics step so
        // BehaviourPuppet has actually released the muscles (SetState clamps
        // their velocity as it unpins, and the pins would pull a launched
        // body back toward the animation otherwise).
        private IEnumerator Launch(PuppetMaster target, Vector3 velocity)
        {
            yield return new WaitForFixedUpdate();
            if (target == null) yield break;
            foreach (Muscle muscle in target.muscles)
                if (muscle.rigidbody != null) muscle.rigidbody.AddForce(velocity, ForceMode.VelocityChange);
            if (debugLogs) Debug.Log($"[UnarmedMelee] → {target.name} launched at {velocity.magnitude:F1} m/s", this);
        }

        // Narrows the attacker's view on a Spartan kick, then eases it back
        // (real time, so the hitstop doesn't stretch it).
        private IEnumerator SpartanZoom()
        {
            baseFov = fpsCamera.Lens.FieldOfView;
            float duration = Mathf.Max(0.05f, spartanZoomDuration);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                // Quick in (first 15 %), hold, ease out over the last 40 %.
                float amount = k < 0.15f ? k / 0.15f : k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f;
                SetFov(baseFov - slowMoZoom * Mathf.SmoothStep(0f, 1f, amount));
                yield return null;
            }
            RestoreFov();
        }

        private void RestoreFov()
        {
            if (zoomRoutine == null || fpsCamera == null) return;
            SetFov(baseFov);
            zoomRoutine = null;
        }

        private void SetFov(float fov)
        {
            LensSettings lens = fpsCamera.Lens;
            lens.FieldOfView = fov;
            fpsCamera.Lens = lens;
        }

        // ---- Indicator (provisional, until the new UI) ----

        private void OnGUI()
        {
            if (!showChargeIndicator || viewCamera == null) return;
            DrawTargetGauge();
            DrawBlockMeter();
            DrawStamina();
            if (phase != Phase.Windup || !charged) return;

            bool top = move == Move.Kick && charge >= spartanMinCharge;
            DrawBar(0.22f, 140f, 10f, charge,
                top ? new Color(1f, .78f, .15f) : Color.Lerp(new Color(.19f, .71f, .4f), new Color(1f, .3f, .24f), charge),
                top ? "SPARTE !" : "PIED", 16);
        }

        // R&D: the hit gauge of the last target, for 2.5 s after a blow,
        // under the crosshair of the attacker's half of the screen.
        private void DrawTargetGauge()
        {
            if (lastGauge == null || Time.time - lastGaugeTime > 2.5f) return;
            float fill = lastGauge.Fill;
            string state = lastGauge.IsKnockedOut ? "K.O." : lastGauge.Dazed > 0f ? "SONNÉ" : lastGauge.IsProtected ? "réveil" : "";
            DrawBar(0.36f, 160f, 8f, fill,
                lastGauge.IsKnockedOut ? new Color(1f, .78f, .15f) : Color.Lerp(new Color(.19f, .71f, .4f), new Color(1f, .3f, .24f), fill),
                $"{lastGauge.name}  {state}", 13);
        }

        // One provisional bar (until the new UI), centred in this player's
        // half of the screen at heightFraction from the bottom, with an
        // optional label above it. Styles are made once per font size: a new
        // GUIStyle every OnGUI call was garbage every frame.
        private static readonly Dictionary<int, GUIStyle> barStyles = new Dictionary<int, GUIStyle>();

        private void DrawBar(float heightFraction, float width, float height, float fill, Color color, string label = null, int fontSize = 12)
        {
            Rect view = viewCamera.pixelRect;
            float x = view.x + view.width / 2f, y = Screen.height - view.yMin - view.height * heightFraction;
            if (!string.IsNullOrEmpty(label))
            {
                if (!barStyles.TryGetValue(fontSize, out GUIStyle style))
                    barStyles[fontSize] = style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(x - 90f, y - fontSize - 12f, 180f, fontSize + 10f), label, style);
            }
            GUI.color = new Color(0f, 0f, 0f, .6f);
            GUI.DrawTexture(new Rect(x - width / 2f, y, width, height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x - width / 2f, y, width * Mathf.Clamp01(fill), height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
