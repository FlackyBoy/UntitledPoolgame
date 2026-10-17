using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // The launcher's gestures, so the other player sees them in the room:
    // - while picking a pocket or a point of the table, the arm stretches
    //   toward it and the index points;
    // - once a power is sent, a thumbs up in front of the chest.
    // FBBIK's hand effector before it solves (arm), then HandPose on the
    // fingers after. A cue in hand stays in its carry pose. Settings in
    // PoolPowerSpawnSettings (Geste); added by LocalPlayerHandController.
    [RequireComponent(typeof(PlayerInput))]
    public class LocalPowerGesture : MonoBehaviour
    {
        private enum Pose { None, Point, ThumbsUp }

        private PlayerInput playerInput;
        private FullBodyBipedIK fullBodyIK;
        private Animator animator;
        private LocalPoolPowerController powers;
        private PoolPowerSpawnSettings cfg;

        // The pose shown (kept while fading out), its weight 0..1.
        private Pose pose;
        private float weight;
        private float thumbsUpLeft;
        private Vector3 pointTarget;
        private bool hasPointTarget;
        private bool hooked;
        private bool loggedBones;
        private bool loggedThumbsUp;

        // Arm lengths the offsets are written for (same reference as
        // LocalObjectHands).
        private const float ReferenceArmLength = 0.6f;

        // Elbow guide (otherwise the elbow goes into the chest), its bend
        // goal saved on Hook and restored on Unhook.
        // What the gesture wrote on the effector last frame, so it isn't
        // read back as the cue grip (the weight then fed on itself).
        private float writtenWeight = -1f;
        private Vector3 writtenPosition;

        private Transform elbowGoal;
        private Transform savedBendGoal;
        private float savedBendWeight;

        private bool Right => cfg.gestureWithRightHand;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            fullBodyIK = GetComponentInChildren<FullBodyBipedIK>(true);
            LocalCharacterAnimationController animControl = GetComponent<LocalCharacterAnimationController>();
            animator = animControl != null && animControl.Animator != null ? animControl.Animator : GetComponentInChildren<Animator>(true);
            powers = GetComponent<LocalPoolPowerController>();
            cfg = PoolSettingsLoader.LoadOrDefault<PoolPowerSpawnSettings>("PoolPowerSpawnSettings");
        }

        private void OnEnable() => PoolMatchRules.PowerActivated += OnPowerActivated;

        private void OnDisable()
        {
            PoolMatchRules.PowerActivated -= OnPowerActivated;
            Unhook();
            weight = thumbsUpLeft = 0f;
            pose = Pose.None;
        }

        private void OnPowerActivated(int player, PoolPower power, bool waitsForOpponentTurn)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (!cfg.thumbsUp || rules == null || rules.GetEffectivePlayerIndex(playerInput.playerIndex) != player) return;
            thumbsUpLeft = cfg.thumbsUpDuration;
        }

        private void Update()
        {
            if (fullBodyIK == null || animator == null || !animator.isHuman) return;

            Pose wanted = Pose.None;
            if (cfg.pointAtTarget && powers != null && powers.ChoosingPower != null)
            {
                // Only while the pointer is on a valid spot; off it, the arm
                // comes down and points again when it's back.
                if (powers.TryGetPickTarget(out Vector3 target))
                {
                    pointTarget = target;
                    hasPointTarget = true;
                    wanted = Pose.Point;
                }
            }
            else
            {
                if (thumbsUpLeft > 0f)
                {
                    thumbsUpLeft -= Time.deltaTime;
                    // Fade out within its duration, not after.
                    if (thumbsUpLeft > cfg.gestureLowerTime) wanted = Pose.ThumbsUp;
                }
            }

            // Switching straight from one pose to the other (the pick
            // confirmed → thumbs up): the arm travels there at full weight.
            if (wanted != Pose.None) pose = wanted;
            // Raised in Gesture Blend Time, lowered in Gesture Lower Time.
            bool raising = wanted != Pose.None;
            float step = Time.deltaTime / Mathf.Max(0.01f, raising ? cfg.gestureBlendTime : cfg.gestureLowerTime);
            weight = Mathf.MoveTowards(weight, raising ? 1f : 0f, step);

            if (weight > 0f && !hooked) Hook();
            else if (weight <= 0f && hooked) { Unhook(); pose = Pose.None; hasPointTarget = false; }

            if (hooked && !loggedBones)
            {
                // The hand shapes depend on the rig's fingers (the player's
                // has an index and a thumb only): said once, for diagnosis.
                loggedBones = true;
                Debug.Log($"[PowerGesture] {name} : os trouvés pour la main {(Right ? "droite" : "gauche")} — {HandPose.Describe(animator, Right)}.", this);
            }
        }

        // Added last to the solver's callbacks, so the gesture wins over
        // whatever drove that hand before (the cue grips, an object).
        private void Hook()
        {
            fullBodyIK.solver.OnPreUpdate -= DriveArm;
            fullBodyIK.solver.OnPostUpdate -= ShapeHand;
            fullBodyIK.solver.OnPreUpdate += DriveArm;
            fullBodyIK.solver.OnPostUpdate += ShapeHand;
            IKConstraintBend bend = Chain.bendConstraint;
            savedBendGoal = bend.bendGoal;
            savedBendWeight = bend.weight;
            hooked = true;
            loggedThumbsUp = false;
        }

        private void Unhook()
        {
            if (!hooked || fullBodyIK == null) return;
            fullBodyIK.solver.OnPreUpdate -= DriveArm;
            fullBodyIK.solver.OnPostUpdate -= ShapeHand;
            // Whoever drives this hand sets its weight again every frame;
            // with nobody, it must not stay pinned where the gesture left it.
            Effector.positionWeight = 0f;
            writtenWeight = -1f;
            IKConstraintBend bend = Chain.bendConstraint;
            bend.bendGoal = savedBendGoal;
            bend.weight = savedBendWeight;
            hooked = false;
        }

        private IKEffector Effector => Right ? fullBodyIK.solver.rightHandEffector : fullBodyIK.solver.leftHandEffector;
        private FBIKChain Chain => Right ? fullBodyIK.solver.rightArmChain : fullBodyIK.solver.leftArmChain;

        // The chest's frame, from the shoulders: the root's axes don't match
        // the body on every rig (the thumbs up ended up inside the chest).
        private Vector3 BodyRight
        {
            get
            {
                Transform l = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform r = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Vector3 v = l != null && r != null ? r.position - l.position : transform.right;
                v.y = 0f;
                return v.sqrMagnitude > 1e-6f ? v.normalized : transform.right;
            }
        }

        private Vector3 BodyForward => Vector3.Cross(BodyRight, Vector3.up);

        // Toward the target, kept in a cone in front of the body (wider on
        // the hand's side) so the arm never goes through the body.
        private Vector3 PointDirection(Vector3 from)
        {
            Vector3 dir = hasPointTarget ? pointTarget - from : BodyForward;
            if (dir.sqrMagnitude < 1e-6f) return BodyForward;
            dir.Normalize();

            Vector3 body = BodyForward;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            float pitch = Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg;
            // Positive = to the right of the body.
            float yaw = flat.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(body, flat, Vector3.up) : 0f;
            Vector4 limits = cfg.pointLimits;   // out, in, up, down
            yaw = Right ? Mathf.Clamp(yaw, -limits.y, limits.x) : Mathf.Clamp(yaw, -limits.x, limits.y);
            pitch = Mathf.Clamp(pitch, -limits.w, limits.z);
            float bodyYaw = Mathf.Atan2(body.x, body.z) * Mathf.Rad2Deg;
            return Quaternion.Euler(-pitch, bodyYaw + yaw, 0f) * Vector3.forward;
        }

        // Toward the body's middle, for the hand in use.
        private Vector3 Inward => Right ? -BodyRight : BodyRight;

        // Before FBBIK solves: where the hand goes.
        private void DriveArm()
        {
            if (weight <= 0f) return;
            FBIKChain chain = Chain;
            Vector3 shoulder = chain.nodes[0].transform.position;
            float armLength = Vector3.Distance(shoulder, chain.nodes[1].transform.position)
                              + Vector3.Distance(chain.nodes[1].transform.position, chain.nodes[2].transform.position);
            float scale = armLength / ReferenceArmLength;
            Vector3 goal;
            if (pose == Pose.Point)
                goal = shoulder + PointDirection(shoulder) * (armLength * cfg.pointReach);
            else
            {
                Vector3 o = cfg.thumbsUpOffset * scale;
                goal = shoulder + BodyForward * o.x + Vector3.up * o.y + Inward * o.z;
            }
            GuideElbow(chain, shoulder, goal, scale);

            IKEffector effector = Effector;
            // Start from what already drives the hand (the cue grip), or the
            // animated hand.
            bool ownLeftover = Mathf.Approximately(effector.positionWeight, writtenWeight) && effector.position == writtenPosition;
            bool driven = !ownLeftover && effector.positionWeight > 0.01f;
            Vector3 from = driven ? effector.position : chain.nodes[2].transform.position;
            // Along an arc in front of the body at full weight: blending the
            // weight moved the hand straight through the belly.
            float t = Mathf.SmoothStep(0f, 1f, weight);
            effector.position = ArcPoint(shoulder, from, goal, t, armLength);
            effector.positionWeight = driven ? Mathf.Lerp(effector.positionWeight, 1f, Mathf.Clamp01(weight * 4f)) : 1f;
            writtenWeight = effector.positionWeight;
            writtenPosition = effector.position;
            // The hand's rotation is ours (after the solve), not a grip's.
            effector.rotationWeight *= 1f - weight;
        }

        // Between two hand positions around the shoulder, passing in front of
        // the body. t 0 = from, 1 = to.
        private Vector3 ArcPoint(Vector3 shoulder, Vector3 from, Vector3 to, float t, float armLength)
        {
            Vector3 a = from - shoulder, b = to - shoulder;
            float da = a.magnitude, db = b.magnitude;
            if (da < 1e-4f || db < 1e-4f) return Vector3.Lerp(from, to, t);
            Vector3 front = (BodyForward - Inward * 0.35f - Vector3.up * 0.35f).normalized;
            Vector3 dir = t < 0.5f
                ? Vector3.Slerp(a / da, front, t * 2f)
                : Vector3.Slerp(front, b / db, t * 2f - 1f);
            // Not closer to the shoulder than half an arm on the way: the
            // fist stays clear of the chest at the turn.
            float distance = Mathf.Max(Mathf.Lerp(da, db, t), armLength * 0.5f * Mathf.Sin(t * Mathf.PI));
            return shoulder + dir * distance;
        }

        // Thumbs up: elbow under the shoulder, close to the body (out to the
        // side it stuck out like a wing). Pointing: arm nearly straight,
        // elbow facing down. Scaled by the gesture's weight.
        private void GuideElbow(FBIKChain chain, Vector3 shoulder, Vector3 hand, float scale)
        {
            if (cfg.gestureElbowOut <= 0f) return;
            if (elbowGoal == null)
            {
                elbowGoal = new GameObject("PowerGestureElbowGoal").transform;
                elbowGoal.SetParent(transform, false);
            }
            if (pose == Pose.ThumbsUp)
            {
                Vector3 o = cfg.thumbsUpElbowOffset * scale;
                elbowGoal.position = shoulder + BodyForward * o.x + Vector3.up * o.y + Inward * o.z;
            }
            else elbowGoal.position = (shoulder + hand) * 0.5f - Vector3.up * (0.25f * scale);
            chain.bendConstraint.bendGoal = elbowGoal;
            chain.bendConstraint.weight = Mathf.Lerp(savedBendWeight, cfg.gestureElbowOut, weight);
        }

        // After FBBIK: the hand turned and the fingers shaped.
        private void ShapeHand()
        {
            if (weight <= 0f) return;
            Transform hand = animator.GetBoneTransform(Right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (hand == null) return;
            float w = weight;
            if (pose == Pose.Point)
            {
                // Palm down, as one points at something on a table.
                HandPose.Orient(animator, Right, PointDirection(hand.position), Vector3.down, w);
                float f = cfg.pointFingerCurl * w;
                HandPose.Curl(animator, Right, cfg.pointIndexCurl * w, f, f, f, cfg.pointThumbCurl * w);
            }
            else
            {
                // Fist forward, palm toward the body's middle: the thumb,
                // on top, points up.
                HandPose.Orient(animator, Right, BodyForward, Inward, w);
                float f = cfg.thumbsUpFingerCurl * w;
                HandPose.Curl(animator, Right, f, f, f, f, cfg.thumbsUpThumbCurl * w);
                HandPose.AimThumb(animator, Right, Vector3.up, w);
                if (w >= 1f && !loggedThumbsUp) LogThumbsUp(hand);
            }
        }

        // Hand and elbow in the chest's frame; a negative "devant" = inside or
        // behind the chest.
        private float ThumbFromUp()
        {
            Transform root = animator.GetBoneTransform(Right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            Transform tip = animator.GetBoneTransform(Right ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);
            return root != null && tip != null ? Vector3.Angle(tip.position - root.position, Vector3.up) : -1f;
        }

        // The fingers (index chain): wrist → last joint, against the body's
        // forward — a closed fist puts it far from forward.
        private float FingersFromForward()
        {
            Transform hand = animator.GetBoneTransform(Right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform tip = animator.GetBoneTransform(Right ? HumanBodyBones.RightIndexDistal : HumanBodyBones.LeftIndexDistal);
            return hand != null && tip != null ? Vector3.Angle(tip.position - hand.position, BodyForward) : -1f;
        }

        private void LogThumbsUp(Transform hand)
        {
            loggedThumbsUp = true;
            if (!cfg.gestureDebugLogs) return;
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (chest == null) return;
            Transform elbow = Chain.nodes[1].transform;
            Vector3 f = BodyForward, i = Inward;
            string Where(Vector3 p)
            {
                Vector3 d = p - chest.position;
                return $"devant {Vector3.Dot(d, f):F2} m, vers le milieu {Vector3.Dot(d, i):F2} m, hauteur {d.y:F2} m";
            }
            Debug.Log($"[PowerGesture] {name} pouce levé, par rapport au buste ({chest.name}) — main : {Where(hand.position)} · coude : {Where(elbow.position)} " +
                      $"· cible de la main : {Where(Effector.position)} · longueur du bras {Vector3.Distance(Chain.nodes[0].transform.position, elbow.position) + Vector3.Distance(elbow.position, hand.position):F2} m " +
                      $"· pouce {ThumbFromUp():F0}° de la verticale · doigts {FingersFromForward():F0}° de l'avant.", this);
        }
    }
}
