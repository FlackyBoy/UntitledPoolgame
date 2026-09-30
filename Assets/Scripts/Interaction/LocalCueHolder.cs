using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Procedural cue pickup, replacing LocalCuePickupTrigger + PickUpCue's
    // FinalIK InteractionSystem route while this component is present and
    // enabled (disable it to fall back to the old route, untouched).
    //
    // Why: the InteractionSystem sends the hands to grip points fixed on the
    // cue, reachable only from one position in front of it. Standing up, the
    // grips end up at floor level and the body collapses to reach them;
    // widening the trigger's angle lets the player pick it up from positions
    // those grips weren't made for. Here the grips are chosen each time:
    //
    // 1. Finding: on Interact, the closest free cue within reach that the
    //    player is looking at, whatever its orientation or the side the
    //    player comes from (no per-scene player <-> cue wiring).
    // 2. Grips: around the point of the cue nearest a comfortable spot in
    //    front of the chest, one per hand, the right hand on the player's
    //    right. Hand rotations come from the cue's own carry grips (its two
    //    InteractionTargets, the poses already tuned for holding it): same
    //    wrap around the cue, turned so the palm faces the hand's shoulder.
    // 3. Posture: a low cue is reached by bending the knees (feet pinned,
    //    body effector lowered) and leaning, not by the arms dragging the
    //    body down.
    // 4. Lift: the cue goes to its carry pose (PickUpCue's hold point and
    //    rotation) while the hands slide along it to the carry grips. If the
    //    cue lies reversed (hand order along it opposite to the carry), the
    //    left hand lets go and regrips instead of crossing the other.
    // 5. Held: the hands follow the carry grips every frame (aiming, the cue
    //    swing and the charge slide move the cue; the hands follow).
    // 6. Release: the cue is dropped to physics and the hands ease off.
    //
    // Everything is applied just before FBBIK solves (solver.OnPreUpdate).
    [RequireComponent(typeof(LocalPlayerHandController))]
    public class LocalCueHolder : MonoBehaviour
    {
        // Tooltips are in French: they are read by the designer in the
        // Inspector.
        [Header("References (auto)")]
        [Tooltip("Caméra du joueur (vide = la première caméra sous ce joueur) : sert à savoir quelle queue il regarde.")]
        [SerializeField] private Transform viewTransform;
        [Tooltip("Full Body Biped IK du personnage (vide = le premier trouvé sous ce joueur).")]
        [SerializeField] private FullBodyBipedIK fullBodyIK;
        [Tooltip("Pose de port de la queue (point, rotation, parent) reprise de ce PickUpCue. Vide = celui du Local Cue Pickup Trigger de ce joueur.")]
        [SerializeField] private PickUpCue carryPose;

        [Header("Finding a cue")]
        [Tooltip("Distance maximale (mètres) entre le joueur et le point le plus proche de la queue.")]
        [SerializeField] private float pickupRange = 1.3f;
        [Tooltip("Angle maximal (degrés) entre le regard et la queue pour pouvoir la ramasser.")]
        [SerializeField] private float viewAngle = 50f;
        [Tooltip("Affiche « E : ramasser » quand une queue peut être ramassée.")]
        [SerializeField] private bool showPrompt = true;

        // Body-relative distances are written for a human-sized arm and
        // scaled to this character's (measured on the skeleton), as for the
        // punches: the cartoon rig is much smaller, and a fixed 0.55 m crouch
        // sank it into the floor.
        [Header("Grips (metres, for a 0.6 m arm, scaled to the character)")]
        [Tooltip("Longueur de bras (épaule → poignet) pour laquelle Comfort Forward / Down et Lean Forward sont écrits ; ils sont mis à l'échelle du bras réel.")]
        [SerializeField] private float referenceArmLength = 0.6f;
        [Tooltip("Point confortable pour attraper : distance devant les épaules.")]
        [SerializeField] private float comfortForward = 0.35f;
        [Tooltip("Point confortable pour attraper : distance sous les épaules.")]
        [SerializeField] private float comfortDown = 0.35f;
        [Tooltip("Écart entre les deux mains le long de la queue au moment de la prise.")]
        [SerializeField] private float gripSpacing = 0.3f;
        [Tooltip("Distance minimale entre une main et un bout de la queue.")]
        [SerializeField] private float endMargin = 0.12f;

        [Header("Posture")]
        [Tooltip("Part de la hauteur à descendre prise par les jambes (genoux pliés, pieds au sol) : 1 = tout, 0 = seuls les bras descendent.")]
        [SerializeField, Range(0f, 1f)] private float crouchAmount = 0.85f;
        [Tooltip("Descente maximale du corps, en part de la hauteur des hanches mesurée sur le personnage (0,45 = le bassin descend de 45 % de sa hauteur ; au-delà les jambes ne plient plus assez et le corps s'enfonce dans le sol).")]
        [SerializeField, Range(0f, 0.8f)] private float maxCrouchFraction = 0.45f;
        [Tooltip("Penché du buste vers l'avant (mètres, bras de référence) à la descente maximale.")]
        [SerializeField] private float leanForward = 0.15f;
        [Tooltip("Flexion maximale du buste aux hanches (degrés) pour aller chercher une queue basse : prend la hauteur que les genoux ne couvrent pas.")]
        [SerializeField, Range(0f, 90f)] private float maxHipBend = 75f;
        [Tooltip("Force avec laquelle les bras tirent le corps vers les mains pendant la prise (Pull des chaînes de bras du FBBIK, 1 par défaut). 0 = le corps ne se fait pas entraîner vers le sol ; la valeur d'origine est rendue une fois la queue en main.")]
        [SerializeField, Range(0f, 1f)] private float armPullWhilePicking = 0f;

        [Header("Timing (seconds)")]
        [Tooltip("Durée du geste vers la queue.")]
        [SerializeField] private float reachTime = 0.4f;
        [Tooltip("Durée de la mise en main (la queue rejoint la pose de port).")]
        [SerializeField] private float liftTime = 0.45f;
        [Tooltip("Durée du relâchement des mains au lâcher.")]
        [SerializeField] private float releaseTime = 0.25f;
        [Tooltip("Moment (en part du geste) où les doigts commencent à se fermer.")]
        [SerializeField, Range(0f, 1f)] private float fingerCloseStart = 0.6f;

        [Header("Elbows while picking up")]
        [Tooltip("Force avec laquelle les coudes sont guidés vers l'extérieur pendant la prise (sans guide, FinalIK les plie selon l'animation, souvent vers l'intérieur).")]
        [SerializeField, Range(0f, 1f)] private float pickupElbowGuide = 0.8f;
        [Tooltip("Coudes pendant la prise : écart vers l'extérieur (mètres, bras de référence) par rapport au milieu épaule-main.")]
        [SerializeField] private float pickupElbowOutward = 0.3f;
        [Tooltip("Coudes pendant la prise : recul vers l'arrière (mètres, bras de référence).")]
        [SerializeField] private float pickupElbowBack = 0.1f;

        [Header("Arm bend goals")]
        [Tooltip("Poids des bend goals des bras quand la queue est tenue (0 sans la queue).")]
        [SerializeField] private float heldArmBendWeight = 1f;
        [Tooltip("Durée du fondu du poids des bend goals.")]
        [SerializeField] private float armBendBlendTime = 0.2f;

        [Header("Debug")]
        [Tooltip("Console : queue choisie, prises, descente, et pourquoi une prise est refusée. Vue Scene (joueur sélectionné) : prises en jaune, point confortable en cyan.")]
        [SerializeField] private bool debugLogs = true;
        [Tooltip("Pendant la prise, écrit toutes les 0,15 s : phase, hauteur de la racine du joueur, du bassin avant et après l'IK, des cibles des mains et leurs poids. Sert à trouver ce qui fait descendre le corps.")]
        [SerializeField] private bool traceIK = true;

        private enum Phase { Idle, Reaching, Lifting, Held, Releasing }

        // One hand's grip: pickup pose (cue space) → carry pose (the cue's
        // InteractionTarget for that hand).
        private struct Hand
        {
            public IKEffector effector;
            public Poser poser;
            public Transform shoulder;            // upper arm bone, for the reach clamp
            public FBIKChain chain;               // arm chain, whose bend goal is swapped during the pickup
            public Transform elbowGoal;           // runtime bend goal used while picking up
            public Transform savedBendGoal;       // the chain's own (aiming) bend goal, put back once held
            public float side;                    // -1 left, +1 right
            public Transform carryTarget;
            public Vector3 pickupLocalPosition;   // in cue space
            public Quaternion pickupLocalRotation;
            public Vector3 lastWorldPosition;     // for the release fade
            public Quaternion lastWorldRotation;
            public bool regrip;                   // lets go and regrips during the lift
        }

        private LocalPlayerHandController handController;
        private Camera viewCamera;
        private Phase phase = Phase.Idle;
        private float phaseTime;

        private LocalGrabbable cueGrabbable;
        private Transform cue;
        private CueChargeSlide cueSlide;
        private Hand left, right;

        private Transform carryParent;
        private Vector3 grabLocalPosition;       // cue pose in carryParent space at grab
        private Quaternion grabLocalRotation;
        private Vector3 holdLocalPosition;
        private Quaternion holdLocalRotation;

        private float crouch;                    // metres, for this pickup
        private float maxCrouch;                 // metres, from the measured hip height
        private float bodyScale = 1f;            // measured arm / reference arm
        private float hipBend;                   // degrees, for this pickup
        private float crouchNow;                 // crouch applied this frame (metres), for the reach clamp
        private bool holdPending;                // carry pose still to be read (on the animated pose)
        private float armLength = 0.6f;          // measured, for the reach clamp
        private float savedLeftArmPull = -1f;    // FBBIK arm chain pulls, restored after the pickup
        private float savedRightArmPull = -1f;
        private Vector3 leftFootPin, rightFootPin;
        private float armBend;

        private Vector3 gizmoComfort, gizmoLeft, gizmoRight;
        private Cue promptCue;

        // Busy from the reach until the hands have let go.
        public bool IsBusy => phase != Phase.Idle;
        // Held and done moving into the carry pose: others may move the cue.
        public bool IsSettled => phase == Phase.Held;

        private void Awake()
        {
            handController = GetComponent<LocalPlayerHandController>();
            if (fullBodyIK == null) fullBodyIK = GetComponentInChildren<FullBodyBipedIK>(true);
            viewCamera = viewTransform != null ? viewTransform.GetComponent<Camera>() : GetComponentInChildren<Camera>(true);
            if (viewTransform == null && viewCamera != null) viewTransform = viewCamera.transform;
            // Everything below is on the player prefab itself, so a player
            // spawned at runtime finds it the same as one placed in the scene.
            if (carryPose == null && TryGetComponent(out LocalCuePickupTrigger trigger)) carryPose = trigger.PickUpCue;
            if (carryPose == null) carryPose = GetComponentInChildren<PickUpCue>(true);
        }

        private void OnEnable()
        {
            if (fullBodyIK != null)
            {
                fullBodyIK.solver.OnPreUpdate += ApplyIK;
                fullBodyIK.solver.OnPostUpdate += TraceAfterSolve;
            }
        }

        private void OnDisable()
        {
            if (fullBodyIK != null)
            {
                fullBodyIK.solver.OnPreUpdate -= ApplyIK;
                fullBodyIK.solver.OnPostUpdate -= TraceAfterSolve;
            }
            if (phase != Phase.Idle) ReleaseNow();
        }

        // ---- Called by LocalPlayerHandController ----

        public bool TryStartPickup()
        {
            if (phase != Phase.Idle) return false;
            if (fullBodyIK == null || viewTransform == null || carryPose == null || carryPose.HoldPoint == null || carryPose.CarryParent == null)
            {
                Log("pickup refused — missing reference (Full Body IK, camera, or the PickUpCue giving the carry pose)");
                return false;
            }
            // No turn check: a cue can be picked up any time (to fight with
            // it, or to be ready); only the shot is turn-bound
            // (LocalPoolAimController.CanShootNow).
            Cue target = FindCue(out string why);
            if (target == null) { Log("pickup refused — " + why); return false; }
            SetUpHands(target);

            cueGrabbable = target.Grabbable;
            cue = target.transform;
            cueSlide = target.ChargeSlide;
            carryParent = carryPose.CarryParent;

            PlanGrips();
            cueGrabbable.MarkExternallyHeld(true);
            handController.NotifyExternallyHeld(cueGrabbable);
            SetPhase(Phase.Reaching);
            return true;
        }

        public bool TryRelease(LocalGrabbable grabbable)
        {
            if (grabbable == null || grabbable != cueGrabbable || phase == Phase.Idle || phase == Phase.Releasing) return false;

            // Keep the hands where they are while they ease off.
            left.lastWorldPosition = left.effector.position;
            left.lastWorldRotation = left.effector.rotation;
            right.lastWorldPosition = right.effector.position;
            right.lastWorldRotation = right.effector.rotation;

            cue.SetParent(null, worldPositionStays: true);
            cueGrabbable.MarkExternallyHeld(false);
            if (cue.TryGetComponent(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            SetFeetPinned(0f);

            // IK off (knocked down): nothing will fade the hands, so let go at once.
            if (!fullBodyIK.enabled) { ReleaseNow(); return true; }
            SetPhase(Phase.Releasing);
            return true;
        }

        // ---- Finding a cue ----

        // Any free, usable cue of the scene (Cue.All: placed in the level or
        // spawned at runtime, no owner), within reach and looked at.
        private Cue FindCue(out string why)
        {
            // Each rejected cue says why (one line per cue in the refusal log).
            var reasons = new System.Text.StringBuilder();
            Cue best = null;
            float bestScore = float.MaxValue;
            Vector3 eye = viewTransform.position;

            foreach (Cue candidate in Cue.All)
            {
                if (candidate == null) continue;
                if (!candidate.IsUsable) { reasons.Append($"\n  {candidate.name}: not usable (missing parts, see the [Cue] warning)"); continue; }
                if (!candidate.IsFree)
                {
                    Transform holder = candidate.transform.parent;
                    reasons.Append($"\n  {candidate.name}: marked as held (parent: {(holder != null ? holder.name : "none")})");
                    continue;
                }

                Segment(candidate.transform, candidate.ChargeSlide, out Vector3 butt, out Vector3 tip);
                Vector3 closest = ClosestOnSegment(butt, tip, transform.position + Vector3.up * 0.5f);
                Vector3 flat = Vector3.ProjectOnPlane(closest - transform.position, Vector3.up);
                if (flat.magnitude > pickupRange)
                {
                    reasons.Append($"\n  {candidate.name}: too far ({flat.magnitude:F2} m > {pickupRange:F2} m; cue length {Vector3.Distance(butt, tip):F2} m)");
                    continue;
                }

                // Looking at it: angle to the nearest point of the cue from the eye.
                Vector3 seen = ClosestOnSegment(butt, tip, eye + viewTransform.forward * Vector3.Distance(eye, closest));
                float angle = Vector3.Angle(viewTransform.forward, seen - eye);
                if (angle > viewAngle) { reasons.Append($"\n  {candidate.name}: not looked at ({angle:F0}° > {viewAngle:F0}°)"); continue; }

                float score = flat.magnitude + angle * 0.01f;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            why = Cue.All.Count == 0
                ? "no cue registered in the scene (no object with a Cue component)"
                : $"no pickable cue among the {Cue.All.Count} of the scene:{reasons}";
            return best;
        }

        private static void Segment(Transform cueTransform, CueChargeSlide slide, out Vector3 butt, out Vector3 tip)
        {
            float tipDistance = slide.TipDistance > 0f ? slide.TipDistance : 0.7f;
            float buttDistance = slide.ButtDistance < 0f ? slide.ButtDistance : -0.7f;
            tip = cueTransform.TransformPoint(slide.TipAxis * tipDistance);
            butt = cueTransform.TransformPoint(slide.TipAxis * buttDistance);
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        // ---- Planning the grips ----

        // Carry grips come from the cue itself (Cue.LeftGrip / RightGrip).
        private void SetUpHands(Cue target)
        {
            Transform leftTarget = target.LeftGrip.transform, rightTarget = target.RightGrip.transform;

            BipedReferences r = fullBodyIK.references;
            left = new Hand
            {
                effector = fullBodyIK.solver.leftHandEffector, carryTarget = leftTarget, shoulder = r.leftUpperArm,
                chain = fullBodyIK.solver.leftArmChain, elbowGoal = ElbowGoal(ref leftElbowGoal, "PickupElbowGoal_L"), side = -1f,
                poser = r.leftHand != null ? r.leftHand.GetComponent<Poser>() : null,
            };
            right = new Hand
            {
                effector = fullBodyIK.solver.rightHandEffector, carryTarget = rightTarget, shoulder = r.rightUpperArm,
                chain = fullBodyIK.solver.rightArmChain, elbowGoal = ElbowGoal(ref rightElbowGoal, "PickupElbowGoal_R"), side = 1f,
                poser = r.rightHand != null ? r.rightHand.GetComponent<Poser>() : null,
            };
            left.savedBendGoal = left.chain.bendConstraint.bendGoal;
            right.savedBendGoal = right.chain.bendConstraint.bendGoal;
        }

        private Transform leftElbowGoal, rightElbowGoal;

        private Transform ElbowGoal(ref Transform goal, string goalName)
        {
            if (goal == null)
            {
                goal = new GameObject(goalName).transform;
                goal.SetParent(transform, false);
            }
            return goal;
        }

        // Elbows out and slightly back while reaching: placed from the
        // shoulder–hand midpoint, weight `w` of Pickup Elbow Guide.
        private void GuideElbow(Hand hand, Vector3 handTarget, float w)
        {
            if (hand.chain == null || hand.shoulder == null) return;
            Vector3 flatForward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;
            Vector3 playerRight = Vector3.Cross(Vector3.up, flatForward);
            Vector3 mid = (hand.shoulder.position + Vector3.down * crouchNow + handTarget) * 0.5f;
            hand.elbowGoal.position = mid + playerRight * (hand.side * pickupElbowOutward * bodyScale) - flatForward * (pickupElbowBack * bodyScale);
            hand.chain.bendConstraint.bendGoal = hand.elbowGoal;
            hand.chain.bendConstraint.weight = pickupElbowGuide * w;
        }

        // Puts the chains' own (aiming) bend goals back, weight 0: the
        // held-cue weight is then blended in by UpdateArmBend.
        private void RestoreElbowGoals()
        {
            if (left.chain != null && left.chain.bendConstraint.bendGoal == left.elbowGoal)
            {
                left.chain.bendConstraint.bendGoal = left.savedBendGoal;
                left.chain.bendConstraint.weight = 0f;
            }
            if (right.chain != null && right.chain.bendConstraint.bendGoal == right.elbowGoal)
            {
                right.chain.bendConstraint.bendGoal = right.savedBendGoal;
                right.chain.bendConstraint.weight = 0f;
            }
        }

        private void PlanGrips()
        {
            BipedReferences r = fullBodyIK.references;
            Segment(cue, cueSlide, out Vector3 butt, out Vector3 tip);
            Vector3 axis = (tip - butt).normalized;
            float length = Vector3.Distance(butt, tip);

            // Comfortable spot in front of the chest.
            Vector3 flatForward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up);
            flatForward = flatForward.sqrMagnitude > 1e-4f ? flatForward.normalized : transform.forward;
            Vector3 shoulders = r.leftUpperArm != null && r.rightUpperArm != null
                ? (r.leftUpperArm.position + r.rightUpperArm.position) * 0.5f
                : viewTransform.position - Vector3.up * 0.25f;
            // Scale to this character's arm (see Reference Arm Length).
            armLength = r.rightUpperArm != null && r.rightForearm != null && r.rightHand != null
                ? Vector3.Distance(r.rightUpperArm.position, r.rightForearm.position) + Vector3.Distance(r.rightForearm.position, r.rightHand.position)
                : referenceArmLength;
            bodyScale = armLength / Mathf.Max(0.01f, referenceArmLength);
            Vector3 comfort = shoulders + flatForward * (comfortForward * bodyScale) - Vector3.up * (comfortDown * bodyScale);
            gizmoComfort = comfort;

            // Grip centre: the point of the cue nearest that spot, kept away
            // from the ends; the two grips either side of it.
            float margin = Mathf.Min(endMargin + gripSpacing * 0.5f, length * 0.5f);
            float s = Mathf.Clamp(Vector3.Dot(comfort - butt, axis), margin, length - margin);
            Vector3 gripA = butt + axis * (s - gripSpacing * 0.5f);
            Vector3 gripB = butt + axis * (s + gripSpacing * 0.5f);

            // Carry order: which hand is further toward the tip when held.
            float carryOrder = Mathf.Sign(Vector3.Dot(right.carryTarget.position - left.carryTarget.position, axis));

            // Right hand on the player's right; near-vertical cue (no clear
            // side): keep the carry order.
            Vector3 playerRight = Vector3.Cross(Vector3.up, flatForward);
            float sideA = Vector3.Dot(gripA - transform.position, playerRight);
            float sideB = Vector3.Dot(gripB - transform.position, playerRight);
            bool aIsRight = Mathf.Abs(sideA - sideB) > 0.05f ? sideA > sideB : carryOrder < 0f;
            Vector3 rightGrip = aIsRight ? gripA : gripB;
            Vector3 leftGrip = aIsRight ? gripB : gripA;
            gizmoLeft = leftGrip;
            gizmoRight = rightGrip;

            // Reversed relative to the carry pose: the left hand regrips
            // during the lift instead of sliding past the right one.
            float pickupOrder = Mathf.Sign(Vector3.Dot(rightGrip - leftGrip, axis));
            left.regrip = pickupOrder != carryOrder;
            right.regrip = false;

            SetPickupGrip(ref left, leftGrip, axis, r.leftUpperArm);
            SetPickupGrip(ref right, rightGrip, axis, r.rightUpperArm);

            // Crouch: the legs take most of the height between the
            // comfortable spot and the grips.
            float gripHeight = (leftGrip.y + rightGrip.y) * 0.5f;
            leftFootPin = r.leftFoot != null ? r.leftFoot.position : transform.position;
            rightFootPin = r.rightFoot != null ? r.rightFoot.position : transform.position;
            // Hip height above the feet: how far the pelvis can go down
            // with the legs still folding under it.
            float hipHeight = r.leftThigh != null && r.rightThigh != null
                ? (r.leftThigh.position.y + r.rightThigh.position.y) * 0.5f - (leftFootPin.y + rightFootPin.y) * 0.5f
                : 0.9f * bodyScale;
            maxCrouch = Mathf.Max(0f, hipHeight) * maxCrouchFraction;
            crouch = Mathf.Clamp((comfort.y - gripHeight) * crouchAmount, 0f, maxCrouch);

            // What the knees don't cover, the hips do: bending the torso
            // forward by θ lowers the shoulders by torso × (1 − cos θ).
            // Without it the grips of a cue on the floor were out of reach
            // and FBBIK dragged the whole body down through the floor.
            float torso = r.pelvis != null ? Mathf.Max(0.1f, shoulders.y - r.pelvis.position.y) : 0.5f * bodyScale;
            float remaining = Mathf.Max(0f, comfort.y - gripHeight - crouch);
            hipBend = Mathf.Min(maxHipBend, Mathf.Acos(Mathf.Clamp(1f - remaining / torso, -1f, 1f)) * Mathf.Rad2Deg);

            if (debugLogs)
                Debug.Log($"[CueHolder] {name}: picking up {cue.name} — grips at {s:F2} m from the butt (cue {length:F2} m), " +
                          $"arm {armLength:F2} m (×{bodyScale:F2}), hips {hipHeight:F2} m high, crouch {crouch:F2} m (max {maxCrouch:F2}), hip bend {hipBend:F0}°, " +
                          $"grips {gripHeight - (leftFootPin.y + rightFootPin.y) * 0.5f:F2} m above the feet and {Vector3.ProjectOnPlane((leftGrip + rightGrip) * 0.5f - transform.position, Vector3.up).magnitude:F2} m away, arm pull {armPullWhilePicking:F2}" +
                          $"{(left.regrip ? ", cue reversed: left hand regrips during the lift" : "")}", this);
        }

        // Hand pose on the cue at `point`: the carry grip's wrap around the
        // cue (measured on its InteractionTarget), turned so the palm faces
        // this hand's shoulder, with the cue's direction chosen to stay
        // closest to the hand's current rotation.
        private void SetPickupGrip(ref Hand hand, Vector3 point, Vector3 axis, Transform shoulder)
        {
            Transform carry = hand.carryTarget;
            Vector3 carryAxis = cue.TransformDirection(cueSlide.TipAxis);
            Vector3 onAxis = cue.position + carryAxis * Vector3.Dot(carry.position - cue.position, carryAxis);
            Vector3 radial = carry.position - onAxis;
            float offset = radial.magnitude;
            Vector3 radialDir = offset > 1e-4f ? radial / offset : carry.up;
            Quaternion inHand = Quaternion.LookRotation(Quaternion.Inverse(carry.rotation) * carryAxis, Quaternion.Inverse(carry.rotation) * radialDir);

            Vector3 toShoulder = (shoulder != null ? shoulder.position : viewTransform.position) - point;
            Vector3 palmSide = Vector3.ProjectOnPlane(toShoulder, axis);
            if (palmSide.sqrMagnitude < 1e-6f) palmSide = Vector3.ProjectOnPlane(-viewTransform.forward, axis);
            palmSide.Normalize();

            Quaternion withTip = Quaternion.LookRotation(axis, palmSide) * Quaternion.Inverse(inHand);
            Quaternion againstTip = Quaternion.LookRotation(-axis, palmSide) * Quaternion.Inverse(inHand);
            Quaternion current = hand.effector.bone != null ? hand.effector.bone.rotation : withTip;
            Quaternion rotation = Quaternion.Angle(withTip, current) <= Quaternion.Angle(againstTip, current) ? withTip : againstTip;

            Vector3 position = point + palmSide * offset;
            hand.pickupLocalPosition = cue.InverseTransformPoint(position);
            hand.pickupLocalRotation = Quaternion.Inverse(cue.rotation) * rotation;
        }

        // ---- Phases ----

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
            if (next == Phase.Reaching)
            {
                SetPoser(left, true);
                SetPoser(right, true);
                SetArmPull(true);
            }
            else if (next != Phase.Lifting)
            {
                SetArmPull(false);
                RestoreElbowGoals();
            }
        }

        // FBBIK's arm chains pull the body toward the hands (pull 1 by
        // default, part of it vertical): with the hands on a cue lying far
        // ahead on the floor, that dragged the whole body down into it on
        // top of the crouch. Lowered for the reach and the lift only.
        private void SetArmPull(bool picking)
        {
            if (fullBodyIK == null) return;
            FBIKChain l = fullBodyIK.solver.leftArmChain, r = fullBodyIK.solver.rightArmChain;
            if (picking)
            {
                if (savedLeftArmPull < 0f) { savedLeftArmPull = l.pull; savedRightArmPull = r.pull; }
                l.pull = armPullWhilePicking;
                r.pull = armPullWhilePicking;
            }
            else if (savedLeftArmPull >= 0f)
            {
                l.pull = savedLeftArmPull;
                r.pull = savedRightArmPull;
                savedLeftArmPull = savedRightArmPull = -1f;
            }
        }

        private void Update()
        {
            UpdatePrompt();
            if (phase == Phase.Idle) return;

            // The cue went away without us (destroyed, grabbed elsewhere).
            if (cue == null) { ReleaseNow(); return; }

            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case Phase.Reaching:
                    if (phaseTime >= reachTime) Grab();
                    break;
                case Phase.Lifting:
                    if (phaseTime >= liftTime) FinishLift();
                    break;
                case Phase.Releasing:
                    if (phaseTime >= releaseTime) ReleaseNow();
                    break;
            }
        }

        private void Grab()
        {
            cue.SetParent(carryParent, worldPositionStays: true);
            if (cue.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
            grabLocalPosition = cue.localPosition;
            grabLocalRotation = cue.localRotation;
            // The carry pose is read on the next pre-solve pass (ReadHoldPose):
            // the hold point sits on the spine, and here in Update the bones
            // still hold last frame's IK result — the crouched, bent pickup
            // pose — which put the carried cue (and the hands) below the floor.
            holdPending = true;
            SetPhase(Phase.Lifting);
        }

        private void ReadHoldPose()
        {
            holdLocalPosition = carryParent.InverseTransformPoint(carryPose.HoldPoint.position);
            holdLocalRotation = Quaternion.Inverse(carryParent.rotation) * carryPose.CarryRotation;
            holdPending = false;
        }

        // Cue pose during the lift, written just before the solve (the hands
        // then follow it the same frame).
        private void MoveCueForLift(float e)
        {
            if (holdPending) ReadHoldPose();
            cue.SetLocalPositionAndRotation(
                Vector3.Lerp(grabLocalPosition, holdLocalPosition, e),
                Quaternion.Slerp(grabLocalRotation, holdLocalRotation, e));
        }

        private void FinishLift()
        {
            if (holdPending) ReadHoldPose();
            cue.SetLocalPositionAndRotation(holdLocalPosition, holdLocalRotation);
            SetFeetPinned(0f);
            SetPhase(Phase.Held);
            if (debugLogs) Debug.Log($"[CueHolder] {name}: holding {cue.name}", this);
        }

        private void ReleaseNow()
        {
            if (fullBodyIK != null)
            {
                ZeroHand(left);
                ZeroHand(right);
                SetFeetPinned(0f);
            }
            SetPoser(left, false);
            SetPoser(right, false);
            SetArmPull(false);
            RestoreElbowGoals();
            if (phase != Phase.Releasing && cueGrabbable != null && cueGrabbable.IsHeld)
            {
                // Cut short (disabled mid-reach, cue lost): put the cue back to physics.
                if (cue != null && cue.parent == carryParent) cue.SetParent(null, worldPositionStays: true);
                cueGrabbable.MarkExternallyHeld(false);
                if (cue != null && cue.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;
            }
            phase = Phase.Idle;
            cueGrabbable = null;
            cue = null;
        }

        private static void ZeroHand(Hand hand)
        {
            if (hand.effector == null) return;
            hand.effector.positionWeight = 0f;
            hand.effector.rotationWeight = 0f;
        }

        private void SetPoser(Hand hand, bool on)
        {
            if (hand.poser == null) return;
            if (on)
            {
                // Same calls as FinalIK's InteractionEffector: the carry
                // target holds a copy of the hand's finger hierarchy posed
                // around the cue.
                InteractionTarget t = hand.carryTarget != null ? hand.carryTarget.GetComponent<InteractionTarget>() : null;
                hand.poser.SetPoseRoot(hand.carryTarget, t != null && t.bones != null ? t.bones : new Transform[0], 4f);
                hand.poser.AutoMapping();
                hand.poser.weight = 0f;
            }
            else
            {
                hand.poser.weight = 0f;
            }
        }

        private void SetFeetPinned(float weight)
        {
            if (fullBodyIK == null) return;
            fullBodyIK.solver.leftFootEffector.positionWeight = weight;
            fullBodyIK.solver.rightFootEffector.positionWeight = weight;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ---- IK (just before FBBIK solves) ----

        // ---- Trace (diagnostic) ----
        // Pelvis height before the solve (as the Animator left it) and after
        // (as FBBIK left it), to tell whether the body goes down because of
        // the animation/root or because of the IK.
        private float tracePelvisBefore;
        private float traceNext;
        private float traceUntil;

        private void TraceBeforeSolve()
        {
            Transform pelvis = fullBodyIK.references.pelvis;
            tracePelvisBefore = pelvis != null ? pelvis.position.y : float.NaN;
        }

        private void TraceAfterSolve()
        {
            if (!traceIK || !debugLogs) return;
            if (Time.time >= traceUntil || Time.time < traceNext) return;
            traceNext = Time.time + 0.15f;

            Transform pelvis = fullBodyIK.references.pelvis;
            IKSolverFullBodyBiped s = fullBodyIK.solver;
            Debug.Log($"[CueHolder trace] {name}: {phase} t={phaseTime:F2} | root y {transform.position.y:F2} | pelvis y before IK {tracePelvisBefore:F2} → after IK {(pelvis != null ? pelvis.position.y : float.NaN):F2} " +
                      $"| hands target y L {s.leftHandEffector.position.y:F2} (w {s.leftHandEffector.positionWeight:F2}) R {s.rightHandEffector.position.y:F2} (w {s.rightHandEffector.positionWeight:F2}) " +
                      $"| feet w {s.leftFootEffector.positionWeight:F2} | arm pull {s.leftArmChain.pull:F2}/{s.rightArmChain.pull:F2} | IK weight {s.IKPositionWeight:F2}", this);
        }

        private void ApplyIK()
        {
            TraceBeforeSolve();
            // Traced while the pickup or the release is moving, plus 1 s
            // (not the whole time the cue is held: it flooded the console).
            if (phase == Phase.Reaching || phase == Phase.Lifting || phase == Phase.Releasing) traceUntil = Time.time + 1f;
            UpdateArmBend();
            if (phase == Phase.Idle || cue == null) return;
            // Bones hold the animated pose here: the right moment to read
            // the carry pose (before Posture bends the spine).
            if (phase == Phase.Lifting && holdPending) ReadHoldPose();

            switch (phase)
            {
                case Phase.Reaching:
                {
                    float w = Smooth(phaseTime / Mathf.Max(0.01f, reachTime));
                    Posture(w);
                    DriveHand(ref left, PickupPosition(left), PickupRotation(left), w, clampToReach: true);
                    DriveHand(ref right, PickupPosition(right), PickupRotation(right), w, clampToReach: true);
                    GuideElbow(left, left.effector.position, w);
                    GuideElbow(right, right.effector.position, w);
                    Fingers(Mathf.InverseLerp(fingerCloseStart, 1f, w));
                    break;
                }
                case Phase.Lifting:
                {
                    float e = Smooth(phaseTime / Mathf.Max(0.01f, liftTime));
                    MoveCueForLift(e);
                    Posture(1f - e);
                    LiftHand(ref left, e);
                    LiftHand(ref right, e);
                    GuideElbow(left, left.effector.position, 1f - e);
                    GuideElbow(right, right.effector.position, 1f - e);
                    Fingers(1f);
                    break;
                }
                case Phase.Held:
                    DriveHand(ref left, left.carryTarget.position, left.carryTarget.rotation, 1f);
                    DriveHand(ref right, right.carryTarget.position, right.carryTarget.rotation, 1f);
                    Fingers(1f);
                    break;
                case Phase.Releasing:
                {
                    float w = 1f - Smooth(phaseTime / Mathf.Max(0.01f, releaseTime));
                    DriveHand(ref left, left.lastWorldPosition, left.lastWorldRotation, w);
                    DriveHand(ref right, right.lastWorldPosition, right.lastWorldRotation, w);
                    Fingers(w);
                    break;
                }
            }
        }

        private Vector3 PickupPosition(Hand hand) => cue.TransformPoint(hand.pickupLocalPosition);
        private Quaternion PickupRotation(Hand hand) => cue.rotation * hand.pickupLocalRotation;

        // Slides from the pickup grip to the carry grip, both in cue space
        // (so the hand rides the cue as it moves). A regripping hand lets go
        // over the first 40 % and takes its carry grip over the last 40 %.
        private void LiftHand(ref Hand hand, float e)
        {
            Vector3 carryLocal = cue.InverseTransformPoint(hand.carryTarget.position);
            Quaternion carryLocalRotation = Quaternion.Inverse(cue.rotation) * hand.carryTarget.rotation;
            if (!hand.regrip)
            {
                DriveHand(ref hand,
                    cue.TransformPoint(Vector3.Lerp(hand.pickupLocalPosition, carryLocal, e)),
                    cue.rotation * Quaternion.Slerp(hand.pickupLocalRotation, carryLocalRotation, e), 1f, clampToReach: true);
                return;
            }
            float w = e < 0.4f ? 1f - Smooth(e / 0.4f) : e > 0.6f ? Smooth((e - 0.6f) / 0.4f) : 0f;
            bool second = e > 0.5f;
            DriveHand(ref hand,
                second ? hand.carryTarget.position : PickupPosition(hand),
                second ? hand.carryTarget.rotation : PickupRotation(hand), w, clampToReach: true);
        }

        // clampToReach: the target is kept within the arm's length of the
        // shoulder (as Posture has placed it this frame, crouch included),
        // so a hand never asks FBBIK to drag the body toward it.
        private void DriveHand(ref Hand hand, Vector3 position, Quaternion rotation, float weight, bool clampToReach = false)
        {
            if (clampToReach && hand.shoulder != null)
            {
                Vector3 shoulder = hand.shoulder.position + Vector3.down * crouchNow;
                Vector3 offset = position - shoulder;
                float max = armLength * 0.97f;
                if (offset.magnitude > max) position = shoulder + offset.normalized * max;
            }
            hand.effector.position = position;
            hand.effector.rotation = rotation;
            hand.effector.positionWeight = weight;
            hand.effector.rotationWeight = weight;
        }

        // Knees bend (feet pinned where they stood, body lowered), the torso
        // bends forward at the hips and leans in, by `w` of this pickup's
        // posture. The spine is turned before the hands are placed, so the
        // reach clamp sees where the shoulders really are.
        private void Posture(float w)
        {
            crouchNow = crouch * w;
            Vector3 flatForward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;

            BipedReferences r = fullBodyIK.references;
            if (hipBend > 0f && r.spine != null && r.spine.Length > 0)
            {
                Vector3 bendAxis = Vector3.Cross(Vector3.up, flatForward);
                Quaternion perBone = Quaternion.AngleAxis(hipBend * w / r.spine.Length, bendAxis);
                foreach (Transform bone in r.spine)
                    if (bone != null) bone.rotation = perBone * bone.rotation;
            }

            if (crouch <= 0f) { SetFeetPinned(0f); return; }
            float k = crouch / Mathf.Max(0.01f, maxCrouch);
            fullBodyIK.solver.bodyEffector.positionOffset += Vector3.down * crouchNow + flatForward * (leanForward * bodyScale * k * w);
            fullBodyIK.solver.leftFootEffector.position = leftFootPin;
            fullBodyIK.solver.rightFootEffector.position = rightFootPin;
            SetFeetPinned(w);
        }

        private void Fingers(float w)
        {
            if (left.poser != null) left.poser.weight = w;
            if (right.poser != null) right.poser.weight = w;
        }

        // Arm bend goals: 0 without the cue, blended to Held Arm Bend Weight
        // once held; written only while changing (the punches' own elbow
        // goals must not be overwritten when the hands are empty).
        private void UpdateArmBend()
        {
            float target = phase == Phase.Held ? 1f : 0f;
            if (Mathf.Approximately(armBend, target)) return;
            armBend = Mathf.MoveTowards(armBend, target, Time.deltaTime / Mathf.Max(0.01f, armBendBlendTime));
            fullBodyIK.solver.leftArmChain.bendConstraint.weight = armBend * heldArmBendWeight;
            fullBodyIK.solver.rightArmChain.bendConstraint.weight = armBend * heldArmBendWeight;
        }

        // ---- Prompt, logs, gizmos ----

        private void UpdatePrompt()
        {
            promptCue = null;
            if (!showPrompt || phase != Phase.Idle || handController.HeldObject != null || viewTransform == null) return;
            promptCue = FindCue(out _);
        }

        private void OnGUI()
        {
            if (promptCue == null || viewCamera == null) return;
            Rect view = viewCamera.pixelRect;
            float x = view.x + view.width / 2f, y = Screen.height - view.yMin - view.height * 0.3f;
            GUI.Label(new Rect(x - 80f, y, 160f, 24f), "E : ramasser",
                new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
        }

        private void Log(string message)
        {
            if (debugLogs) Debug.Log($"[CueHolder] {name}: {message}", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || phase == Phase.Idle) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(gizmoComfort, 0.05f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gizmoLeft, 0.04f);
            Gizmos.DrawWireSphere(gizmoRight, 0.04f);
            Gizmos.DrawLine(gizmoLeft, gizmoRight);
        }
    }
}
