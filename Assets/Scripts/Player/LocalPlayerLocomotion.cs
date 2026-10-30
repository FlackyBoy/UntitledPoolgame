using System.Collections.Generic;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // R&D movement (party brawler). While enabled it replaces
    // LocalFpsPlayerController's move (that one keeps the look). R&D player
    // variant only for now.
    // - Momentum: acceleration and deceleration (less control in the air),
    //   sprint while held.
    // - Jump with coyote time and a short buffer, driving the airborne states.
    // - Ledge climbing between Ledge Min and Max Height, hands on the edge.
    // - Floppy body with empty hands (weaker arm muscles, pelvis leaning
    //   against acceleration); normal stiffness while holding, aiming or
    //   striking.
    // - View roll when strafing, bob while walking.
    // - Dazed (LocalHitGauge): slower, drifting, swaying. Guarding: slower,
    //   no sprint.
    // Tuned in MovementSettings.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class LocalPlayerLocomotion : MonoBehaviour
    {
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string jumpActionName = "Jump";

        [Tooltip("Logs [Move] : sauts, rebords trouvés ou refusés, escalades.")]
        [SerializeField] private bool traceMovement;

        // ---------- State, read by other systems (combat, animation) ----------

        public Vector3 Velocity => horizontal + Vector3.up * verticalSpeed;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsClimbing => climbTime >= 0f;

        private MovementSettings cfg;
        private CharacterController controller;
        private LocalFpsPlayerController fps;
        private LocalPlayerHandController hands;
        private LocalPoolAimController aim;
        private LocalUnarmedMelee unarmed;
        private LocalCueMelee cueMelee;
        private Animator animator;
        private FullBodyBipedIK ik;
        private PuppetMaster puppet;
        private InputAction moveAction, sprintAction, jumpAction;

        private Vector3 horizontal;
        private float verticalSpeed;
        private float lastGroundedTime = -10f, jumpPressedTime = -10f;
        private float climbTime = -1f;
        private Vector3 climbStart, climbEnd, climbHandLeft, climbHandRight;
        private float jumpLeg = 1f;

        // Floppy body: the authored muscle props, restored when stiff.
        private readonly List<(Muscle muscle, float weight, float pin)> armMuscles = new List<(Muscle, float, float)>();
        private readonly List<(Muscle muscle, float weight)> spineMuscles = new List<(Muscle, float)>();
        private float relax;                   // 0 = authored stiffness, 1 = fully relaxed
        private Vector3 lean, lastHorizontal;

        // View offsets, removed again before the next ones are added.
        private Vector3 appliedCameraOffset;
        private float bobPhase;

        // Dazed: the hit gauge may be added at runtime (first blow taken),
        // so it's looked for again — once a second, not twice a frame.
        private LocalHitGauge gauge;
        private float nextGaugeLookup;
        private float Dazed
        {
            get
            {
                if (gauge == null && Time.time >= nextGaugeLookup)
                {
                    nextGaugeLookup = Time.time + 1f;
                    gauge = GetComponent<LocalHitGauge>();
                }
                return gauge != null ? gauge.Dazed : 0f;
            }
        }

        private bool hasJump, hasJumpLeg;

        // Size and speed powers (LocalFpsPlayerController.MoveScale).
        private float MoveScale => fps != null ? fps.MoveScale : 1f;

        private void Awake()
        {
            cfg = MovementSettings.Instance;
            controller = GetComponent<CharacterController>();
            fps = GetComponent<LocalFpsPlayerController>();
            hands = GetComponent<LocalPlayerHandController>();
            aim = GetComponent<LocalPoolAimController>();
            unarmed = GetComponent<LocalUnarmedMelee>();
            cueMelee = GetComponent<LocalCueMelee>();
            LocalCharacterAnimationController animControl = GetComponent<LocalCharacterAnimationController>();
            animator = animControl != null ? animControl.Animator : null;
            ik = GetComponentInChildren<FullBodyBipedIK>(true);
            puppet = GetComponentInChildren<PuppetMaster>(true);

            var input = GetComponent<PlayerInput>();
            InputActionMap map = input != null ? input.actions.FindActionMap(actionMapName) : null;
            moveAction = map?.FindAction(moveActionName);
            sprintAction = map?.FindAction(sprintActionName);
            jumpAction = map?.FindAction(jumpActionName);

            hasJump = HasParameter("Jump", AnimatorControllerParameterType.Float);
            hasJumpLeg = HasParameter("JumpLeg", AnimatorControllerParameterType.Float);

            if (puppet != null)
                foreach (Muscle m in puppet.muscles)
                {
                    if (m == null) continue;
                    if (m.props.group == Muscle.Group.Arm || m.props.group == Muscle.Group.Hand)
                        armMuscles.Add((m, m.props.muscleWeight, m.props.pinWeight));
                    else if (m.props.group == Muscle.Group.Spine || m.props.group == Muscle.Group.Head)
                        spineMuscles.Add((m, m.props.muscleWeight));
                }
        }

        private void OnEnable()
        {
            if (ik != null) ik.solver.OnPreUpdate += BeforeIK;
        }

        // Logs what was found on the player: always if something's missing,
        // otherwise with Trace Movement.
        private void Start()
        {
            bool missing = fps == null || moveAction == null || sprintAction == null || jumpAction == null;
            if (!missing && !traceMovement) return;
            string message = $"[Move] Déplacement R&D sur « {name} » — FPS {(fps != null ? "ok" : "ABSENT")}, " +
                             $"actions Move {Found(moveAction)} / Sprint {Found(sprintAction)} / Jump {Found(jumpAction)}, " +
                             $"Animator {(animator != null ? "ok" : "absent")}, FBBIK {(ik != null ? "ok" : "absent")}, " +
                             $"PuppetMaster {(puppet != null ? $"{armMuscles.Count} muscles de bras" : "absent")}.";
            if (missing) Debug.LogWarning(message, this);
            else Debug.Log(message, this);
        }

        private static string Found(InputAction a) => a == null ? "ABSENTE" : a.enabled ? "ok" : "désactivée";

        private float lastInactiveLog = -10f;

        private void LogInactive(PoolMatchRules rules)
        {
            if (!traceMovement || Time.time - lastInactiveLog < 2f) return;
            lastInactiveLog = Time.time;
            string why = rules != null && !rules.MatchStarted ? "la partie n'a pas commencé"
                : fps == null ? "pas de LocalFpsPlayerController"
                : !fps.enabled ? "LocalFpsPlayerController désactivé (visée, chute…)"
                : "CharacterController désactivé";
            Debug.Log($"[Move] « {name} » : déplacement R&D en pause — {why}.");
        }

        private void OnDisable()
        {
            if (ik != null) ik.solver.OnPreUpdate -= BeforeIK;
            relax = 0f;
            ApplyStiffness();
            RemoveCameraOffset();
        }

        private bool HasParameter(string name, AnimatorControllerParameterType type)
        {
            if (animator == null) return false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name && p.type == type) return true;
            return false;
        }

        // ---------- Frame ----------

        private void Update()
        {
            cfg = MovementSettings.Instance;
            float dt = Time.deltaTime;

            // Same gates as LocalFpsPlayerController: still before the match
            // starts, and nothing while it's switched off (aiming, ragdoll).
            PoolMatchRules rules = PoolMatchRules.Instance;
            bool active = (rules == null || rules.MatchStarted) && fps != null && fps.enabled && controller.enabled;
            if (!active)
            {
                LogInactive(rules);
                horizontal = Vector3.zero;
                verticalSpeed = 0f;
                climbTime = -1f;
                UpdateStiffness(dt, false);
                return;
            }

            if (jumpAction != null && jumpAction.WasPressedThisFrame())
            {
                jumpPressedTime = Time.time;
                Trace($"saut appuyé — au sol {controller.isGrounded}, vitesse verticale {verticalSpeed:0.0}, escalade {IsClimbing}");
            }

            if (IsClimbing) { UpdateClimb(dt); UpdateStiffness(dt, false); return; }

            IsGrounded = controller.isGrounded;
            if (IsGrounded) lastGroundedTime = Time.time;

            Vector2 input = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            // Guarding (R&D): slower, no sprint.
            bool blocking = unarmed != null && unarmed.IsBlocking;
            IsSprinting = !blocking && sprintAction != null && sprintAction.IsPressed() && input.y > 0.1f;
            // Pocket Size: a shrunk body moves, jumps and falls at its own scale.
            float size = fps != null ? fps.SizeScale : 1f;
            float gravity = cfg.gravity * size;

            {
                // Momentum: towards the wanted velocity at acceleration
                // when speeding up, at deceleration when slowing or turning.
                Vector3 wanted = (transform.right * input.x + transform.forward * input.y) * (IsSprinting ? cfg.sprintSpeed : cfg.walkSpeed) * MoveScale;
                if (blocking) wanted *= CombatSettings.Instance.blockMoveSpeed;
                float dazed = Dazed;
                if (dazed > 0f)
                {
                    // Slower, and the legs wander: a slow sideways drift
                    // (two sines, so it doesn't read as a regular sway).
                    CombatSettings combat = CombatSettings.Instance;
                    wanted *= Mathf.Lerp(1f, combat.dazedSpeed, dazed);
                    float drift = Mathf.Sin(Time.time * 1.7f) * 0.7f + Mathf.Sin(Time.time * 0.9f + 1.3f) * 0.3f;
                    wanted += transform.right * (drift * combat.dazedStagger * dazed);
                }
                bool speedingUp = Vector3.Dot(wanted, horizontal) >= horizontal.sqrMagnitude * 0.99f && wanted.sqrMagnitude > 0.01f;
                float rate = (speedingUp ? cfg.acceleration : cfg.deceleration) * MoveScale;
                if (!IsGrounded) rate *= cfg.airControl;
                horizontal = Vector3.MoveTowards(horizontal, wanted, rate * dt);

                if (Time.time - jumpPressedTime <= cfg.jumpBuffer)
                {
                    if (TryFindLedge(out Vector3 top, out Vector3 wallNormal)) { jumpPressedTime = -10f; StartClimb(top, wallNormal); return; }
                    if (Time.time - lastGroundedTime <= cfg.coyoteTime && verticalSpeed <= 0.1f)
                    {
                        jumpPressedTime = -10f;
                        lastGroundedTime = -10f;
                        verticalSpeed = Mathf.Sqrt(2f * cfg.jumpHeight * size * -gravity);
                        jumpLeg = -jumpLeg;
                        Trace("saut");
                    }
                }
            }

            if (IsGrounded && verticalSpeed < 0f) verticalSpeed = -2f * size;
            else verticalSpeed += gravity * dt;

            CollisionFlags flags = controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            // Running into a wall eats the speed going into it.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 moved = controller.velocity;
                moved.y = 0f;
                if (moved.sqrMagnitude < horizontal.sqrMagnitude) horizontal = Vector3.Lerp(horizontal, moved, 0.5f);
            }

            UpdateAnimator();
            UpdateStiffness(dt, HandsFree());
            UpdateLean(dt);
        }

        private void LateUpdate() => UpdateCamera(Time.deltaTime);

        // ---------- Ledge climbing ----------

        // A ledge in front: a wall within reach, a flat top between Ledge Min
        // and Max Height above the feet, and room to stand on it.
        private bool TryFindLedge(out Vector3 top, out Vector3 wallNormal)
        {
            top = wallNormal = Vector3.zero;
            float radius = controller.radius;
            Vector3 feet = transform.position + controller.center - Vector3.up * (controller.height / 2f);
            Vector3 forward = transform.forward;
            float reach = radius + cfg.ledgeReach;

            // The wall, felt at mid-height of the ledge range.
            Vector3 chest = feet + Vector3.up * Mathf.Lerp(cfg.ledgeMinHeight, cfg.ledgeMaxHeight, 0.4f);
            if (!Physics.Raycast(chest, forward, out RaycastHit wall, reach, ~0, QueryTriggerInteraction.Ignore) || IsOwnCollider(wall.collider))
                return false;
            if (Mathf.Abs(wall.normal.y) > 0.4f) return false;
            wallNormal = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;

            // Its top, found from above, just behind the wall's face.
            Vector3 above = wall.point - wallNormal * (radius * 0.8f + 0.05f);
            above.y = feet.y + cfg.ledgeMaxHeight + 0.15f;
            float span = cfg.ledgeMaxHeight - cfg.ledgeMinHeight + 0.15f;
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit hit, span, ~0, QueryTriggerInteraction.Ignore) || IsOwnCollider(hit.collider))
            {
                Trace("rebord refusé : pas de dessus plat à portée");
                return false;
            }
            if (hit.normal.y < 0.7f) { Trace("rebord refusé : dessus trop penché"); return false; }
            float height = hit.point.y - feet.y;
            if (height < cfg.ledgeMinHeight || height > cfg.ledgeMaxHeight) return false;

            // Room for the body once on top.
            Vector3 stand = hit.point - wallNormal * (radius + 0.1f);
            Vector3 bottom = stand + Vector3.up * (radius + 0.05f);
            Vector3 head = stand + Vector3.up * (controller.height - radius);
            if (Physics.CheckCapsule(bottom, head, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
            {
                Trace("rebord refusé : pas la place de se tenir debout dessus");
                return false;
            }
            top = hit.point;
            return true;
        }

        private bool IsOwnCollider(Collider c) => c != null && c.transform.IsChildOf(transform);

        private void StartClimb(Vector3 top, Vector3 wallNormal)
        {
            climbTime = 0f;
            climbStart = transform.position;
            Vector3 feetToRoot = transform.position - (transform.position + controller.center - Vector3.up * (controller.height / 2f));
            climbEnd = top - wallNormal * (controller.radius + 0.1f) + feetToRoot + Vector3.up * 0.02f;
            Vector3 side = Vector3.Cross(Vector3.up, wallNormal).normalized * 0.22f;
            Vector3 edge = top + wallNormal * 0.05f;
            climbHandLeft = edge + side;
            climbHandRight = edge - side;
            horizontal = Vector3.zero;
            verticalSpeed = 0f;
            Trace($"escalade d'un rebord de {top.y - (transform.position.y + controller.center.y - controller.height / 2f):0.00} m");
        }

        // Up first (hoisting on the hands), then over the edge.
        private void UpdateClimb(float dt)
        {
            climbTime += dt / cfg.climbDuration;
            float k = Mathf.Clamp01(climbTime);
            float up = Smooth(Mathf.Clamp01(k / 0.65f));
            float over = Smooth(Mathf.Clamp01((k - 0.45f) / 0.55f));
            Vector3 p = new Vector3(
                Mathf.Lerp(climbStart.x, climbEnd.x, over),
                Mathf.Lerp(climbStart.y, climbEnd.y, up),
                Mathf.Lerp(climbStart.z, climbEnd.z, over));
            // Moved directly: the controller would catch on the edge.
            controller.enabled = false;
            transform.position = p;
            controller.enabled = true;
            if (k >= 1f) climbTime = -1f;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // ---------- Body ----------

        private bool HandsFree() =>
            (hands == null || (hands.HeldObject == null && !hands.CueBusy))
            && (aim == null || (!aim.IsAiming && !aim.IsPlacementViewActive))
            && (unarmed == null || !unarmed.IsAttacking)
            && (cueMelee == null || !cueMelee.IsSwinging)
            && !IsClimbing;

        private void UpdateStiffness(float dt, bool relaxed)
        {
            // Striking: stiff at once. Blending back over a few frames left
            // the arm (shown by PuppetMaster) lagging behind a 0.07 s punch.
            bool striking = (unarmed != null && (unarmed.IsAttacking || unarmed.IsGuarding)) || (cueMelee != null && cueMelee.IsSwinging);
            relax = striking ? 0f : Mathf.MoveTowards(relax, relaxed ? 1f : 0f, cfg.stiffnessBlendSpeed * dt);
            ApplyStiffness();
        }

        // ---------- View kick (a punch leaving) ----------

        private float kickRoll, kickPush, kickDuration, kickTime = -1f;

        // A short roll (degrees) and push forward (metres) of the view, out
        // and back over duration seconds.
        public void KickView(float roll, float push, float duration)
        {
            kickRoll = roll;
            kickPush = push;
            kickDuration = Mathf.Max(0.02f, duration);
            kickTime = 0f;
        }

        private float ViewKick(float dt)
        {
            if (kickTime < 0f) return 0f;
            kickTime += dt;
            float k = kickTime / kickDuration;
            if (k >= 1f) { kickTime = -1f; return 0f; }
            // Snaps out in the first fifth, eases back over the rest.
            return k < 0.2f ? Smooth(k / 0.2f) : 1f - Smooth((k - 0.2f) / 0.8f);
        }

        private void ApplyStiffness()
        {
            foreach (var (m, weight, pin) in armMuscles)
            {
                m.props.muscleWeight = Mathf.Lerp(weight, weight * cfg.relaxedArmStrength, relax);
                m.props.pinWeight = Mathf.Lerp(pin, pin * cfg.relaxedArmPin, relax);
            }
            foreach (var (m, weight) in spineMuscles)
                m.props.muscleWeight = Mathf.Lerp(weight, weight * cfg.relaxedSpineStrength, relax);
        }

        // The pelvis is pushed against the acceleration: leans back when
        // speeding up, forward when braking, outward in a turn.
        private void UpdateLean(float dt)
        {
            Vector3 accel = dt > 0f ? (horizontal - lastHorizontal) / dt : Vector3.zero;
            lastHorizontal = horizontal;
            Vector3 target = HandsFree() && IsGrounded ? -accel * (cfg.bodyLean / 10f) : Vector3.zero;
            target = Vector3.ClampMagnitude(target, cfg.bodyLean * 2f);
            lean = Vector3.Lerp(lean, target, 1f - Mathf.Exp(-cfg.bodyLeanSmoothing * dt));
        }

        private void BeforeIK()
        {
            if (ik == null) return;
            IKSolverFullBodyBiped solver = ik.solver;
            solver.bodyEffector.positionOffset += lean;

            // Hands on the ledge while hoisting, let go near the end.
            if (IsClimbing)
            {
                float k = Mathf.Clamp01(climbTime);
                float w = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
                solver.leftHandEffector.position = climbHandLeft;
                solver.rightHandEffector.position = climbHandRight;
                solver.leftHandEffector.positionWeight = w;
                solver.rightHandEffector.positionWeight = w;
                handsOnLedge = true;
            }
            else if (handsOnLedge)
            {
                solver.leftHandEffector.positionWeight = 0f;
                solver.rightHandEffector.positionWeight = 0f;
                handsOnLedge = false;
            }
        }

        private bool handsOnLedge;

        // ---------- Animation and view ----------

        private void UpdateAnimator()
        {
            if (animator == null) return;
            if (hasJump) animator.SetFloat("Jump", IsGrounded ? 0f : verticalSpeed);
            if (hasJumpLeg) animator.SetFloat("JumpLeg", jumpLeg);
        }

        private void UpdateCamera(float dt)
        {
            Transform pivot = fps != null ? fps.CameraPivot : null;
            if (pivot == null) return;
            RemoveCameraOffset();
            if (fps == null || !fps.enabled || !isActiveAndEnabled) return;

            Vector3 offset = Vector3.zero;
            if (IsGrounded)
            {
                float speed01 = Mathf.Clamp01(horizontal.magnitude / (Mathf.Max(0.1f, cfg.walkSpeed) * MoveScale));
                bobPhase += dt * cfg.headBobFrequency * Mathf.PI * 2f * Mathf.Max(speed01, 0.001f) * (IsSprinting ? 1.3f : 1f);
                offset.y += Mathf.Sin(bobPhase * 2f) * cfg.headBob * speed01;
                offset.x += Mathf.Sin(bobPhase) * cfg.headBob * 0.5f * speed01;
            }
            float kick = ViewKick(dt);
            offset.z += kickPush * kick;
            pivot.localPosition += offset;
            appliedCameraOffset = offset;

            // Rolls into a strafe (on top of the pitch the FPS controller
            // writes every frame), and with a punch leaving.
            float strafe = Vector3.Dot(horizontal, transform.right) / (Mathf.Max(0.1f, cfg.sprintSpeed) * MoveScale);
            float roll = -strafe * cfg.strafeCameraRoll + kickRoll * kick;
            // Dazed: the view sways (roll, and a little pitch).
            float dazed = Dazed, swayPitch = 0f;
            if (dazed > 0f)
            {
                float sway = CombatSettings.Instance.dazedViewSway * dazed;
                roll += Mathf.Sin(Time.time * 1.3f) * sway;
                swayPitch = Mathf.Sin(Time.time * 0.8f + 0.7f) * sway * 0.4f;
            }
            pivot.localRotation = Quaternion.Euler(fps.Pitch + swayPitch, 0f, roll);
        }

        private void RemoveCameraOffset()
        {
            Transform pivot = fps != null ? fps.CameraPivot : null;
            if (pivot != null && appliedCameraOffset != Vector3.zero) pivot.localPosition -= appliedCameraOffset;
            appliedCameraOffset = Vector3.zero;
        }

        private void Trace(string message)
        {
            if (traceMovement) Debug.Log($"[Move] {name} : {message}");
        }
    }
}
