using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // Offline/split-screen counterpart to CharacterAnimationController — same
    // locomotion-from-CharacterController logic, but no networking at all:
    // each local player's own copy just always drives its own Animator
    // directly, since both split-screen players are meant to see each other
    // (no owner/other-client distinction to make here).
    [RequireComponent(typeof(CharacterController))]
    public class LocalCharacterAnimationController : MonoBehaviour
    {
        // See CharacterAnimationController for why this is a hand-assigned
        // field rather than GetComponentInChildren.
        [SerializeField] private Animator animator;

        // Optional — see the Awake existence checks below. Our own simple
        // Idle/Locomotion controller (PlayerOffline.controller) uses this;
        // richer imported controllers (e.g. RootMotion's demo "Humanoid
        // Third Person Puppet" family) don't have any single generic speed
        // parameter at all — their Idle pose is just the origin of the
        // Forward/Right blend space below, reached naturally at zero input,
        // no separate gating needed.
        [SerializeField] private string speedParameterName = "Speed";

        // Optional bonus — see CharacterAnimationController; harmless if the
        // assigned Animator Controller has no such bool parameter.
        [SerializeField] private string isAimingParameterName = "IsAiming";

        // Optional — true whenever the held object is the pool cue,
        // regardless of whether aim mode is currently entered (IsAiming
        // above covers that narrower case). Drives an idle-with-cue pose
        // for "holding it but not aiming yet".
        [SerializeField] private string isHoldingCueParameterName = "IsHoldingCue";

        // Optional — true whenever ANY object is held, cue included. On its
        // own this doesn't distinguish cue from non-cue; combined with
        // IsHoldingCue in a transition's AND condition
        // (IsHoldingObject == true, IsHoldingCue == false), it picks out
        // "holding some other object" as its own pose, separate from both
        // empty-handed idle and holding-the-cue idle.
        [SerializeField] private string isHoldingObjectParameterName = "IsHoldingObject";

        // AimIK/LimbIK/SecondHandOnGun (FinalIK) — these solve toward the
        // cue's grip sockets every frame regardless of whether anything's
        // actually there, so left enabled while empty-handed they yank the
        // arms toward wherever those sockets sit. Disabled outright
        // (enabled = false) whenever isHoldingCue is false, using Behaviour
        // as the common base type since FinalIK's IK components and
        // SecondHandOnGun aren't related by any narrower shared base.
        [SerializeField] private Behaviour[] cueOnlyIkComponents;

        // Optional — mirrors LocalPoolAimController.ChargeFraction (0 at
        // rest, up to 1 at Max Power) every frame while aiming, so a Blend
        // Tree can pull the cue back progressively as the shot charges,
        // instead of a single fixed "about to shoot" pose.
        [SerializeField] private string shootChargeParameterName = "ShootCharge";

        // Signed local-space velocity, for a 2D locomotion Blend Tree (strafe
        // left/right + walk forward as separate clips instead of one generic
        // "Speed" magnitude). Same optional-parameter pattern as IsAiming —
        // harmless if the assigned controller doesn't have a strafe blend yet.
        [SerializeField] private string moveXParameterName = "MoveX";
        [SerializeField] private string moveYParameterName = "MoveY";

        // Same axes as MoveX/MoveY above, under the parameter names used by
        // RootMotion's "Humanoid Third Person Puppet" demo controller family
        // (Right = strafe, Forward = forward/back) — kept as its own
        // optional pair rather than folded into MoveX/MoveY so a single
        // Animator Controller can only ever match one naming scheme at a
        // time, with no risk of half-matching a mixed one.
        [SerializeField] private string rightParameterName = "Right";
        [SerializeField] private string forwardParameterName = "Forward";

        // Right/Forward's Blend Tree (RootMotion's demo controller) is
        // normalized to a -1..1 direction+magnitude, unlike MoveX/MoveY's
        // Blend Tree above (authored on the same ±5 scale as actual local
        // velocity, see Walk Blend Axis Scale) — local velocity is divided
        // by this before being written to Right/Forward, so 1 should
        // roughly be this character's fastest move speed
        // (LocalFpsPlayerController's Move Speed).
        [SerializeField] private float rightForwardMaxSpeed = 5f;

        // That controller family also has a Turn+Forward "face your
        // movement" locomotion mode as an alternative to real strafing,
        // selected by this bool — forced permanently true once in Awake
        // (never toggled after), since this FPS-style camera-relative
        // character always strafes rather than turning to face its own
        // movement direction (transform yaw is driven directly by
        // look input — see LocalFpsPlayerController — with no code path
        // that ever turns the body to face movement instead, so Grounded
        // Directional's "turn to face movement" premise doesn't hold here).
        // The cue/no-cue leg pose difference the player wants instead lives
        // inside Grounded Strafe itself — see Base Layer's own
        // IsHoldingCue-gated split, not this parameter.
        [SerializeField] private string isStrafingParameterName = "IsStrafing";

        // Same controller family also gates its jump/fall states on this —
        // no jump/fall mechanic exists yet, but driving it for real from
        // CharacterController.isGrounded costs nothing and avoids relying
        // on its default value staying accurate forever.
        [SerializeField] private string groundedParameterName = "OnGround";

        // Matches the Blend Tree's forward/strafe motion field positions
        // (also 5 by default) — used to scale the turn-lean value below onto
        // the same axis range the Blend Tree expects.
        [SerializeField] private float walkBlendAxisScale = 5f;

        // Below this, the Idle state is playing (or about to) — must match
        // the "Speed Less/Greater 0.1" transition conditions in the Animator
        // Controller. Keeps Animator.speed at 1 in Idle instead of freezing
        // it at ~0 while nearly stopped.
        [SerializeField] private float idleSpeedThreshold = 0.1f;

        // Body yaw follows the camera directly (see LocalFpsPlayerController)
        // — with no clip for "turn in place", looking around while standing
        // still used to just snap-rotate the whole body with zero animation
        // reaction. Instead of authoring a dedicated turn clip, a fast turn
        // while otherwise idle leans into the existing strafe pose (lerped
        // in/out, so it reads as a weight shift rather than a snap).
        [SerializeField] private float turnLeanMaxAngularSpeed = 180f;
        [SerializeField] private float turnLeanSmoothing = 6f;

        // Eases every float parameter below toward its target instead of
        // snapping instantly — smooths the Blend Tree's own transitions
        // (e.g. a sudden stop or direction reversal) into the animation
        // instead of a hard jump between poses. Too high a value reads as
        // laggy/unresponsive input, so keep this small; 0 restores the old
        // instant behaviour exactly (Animator.SetFloat's dampTime=0 is a
        // plain assignment, same as the 2-argument overload).
        [SerializeField] private float parameterDampTime = 0.1f;

        private CharacterController controller;
        private LocalPoolAimController poolAimController;
        private LocalPlayerHandController handController;
        private bool hasSpeedParameter;
        private bool hasIsAimingParameter;
        private bool hasIsHoldingCueParameter;
        private bool hasIsHoldingObjectParameter;
        private bool hasShootChargeParameter;
        private bool hasMoveXYParameters;
        private bool hasRightForwardParameters;
        private bool hasGroundedParameter;
        private float previousYaw;
        private float turnLeanSmoothed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            poolAimController = GetComponent<LocalPoolAimController>();
            handController = GetComponent<LocalPlayerHandController>();
            hasSpeedParameter = AnimatorHasFloat(animator, speedParameterName);
            hasIsAimingParameter = AnimatorHasBool(animator, isAimingParameterName);
            hasIsHoldingCueParameter = AnimatorHasBool(animator, isHoldingCueParameterName);
            hasIsHoldingObjectParameter = AnimatorHasBool(animator, isHoldingObjectParameterName);
            hasShootChargeParameter = AnimatorHasFloat(animator, shootChargeParameterName);
            hasMoveXYParameters = AnimatorHasFloat(animator, moveXParameterName) && AnimatorHasFloat(animator, moveYParameterName);
            hasRightForwardParameters = AnimatorHasFloat(animator, rightParameterName) && AnimatorHasFloat(animator, forwardParameterName);
            hasGroundedParameter = AnimatorHasBool(animator, groundedParameterName);
            previousYaw = transform.eulerAngles.y;

            // Set once, never toggled — see the field comment above.
            if (animator != null && AnimatorHasBool(animator, isStrafingParameterName))
                animator.SetBool(isStrafingParameterName, true);
        }

        private static bool AnimatorHasBool(Animator animator, string parameterName)
        {
            if (animator == null) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
                    return true;
            }
            return false;
        }

        private static bool AnimatorHasFloat(Animator animator, string parameterName)
        {
            if (animator == null) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == parameterName)
                    return true;
            }
            return false;
        }

        private void Update()
        {
            if (animator == null) return;

            // Horizontal only — gravity's fall speed shouldn't blend toward
            // a running animation while airborne/falling.
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            float horizontalSpeed = velocity.magnitude;
            bool isStandingStill = horizontalSpeed <= idleSpeedThreshold;

            float currentYaw = transform.eulerAngles.y;
            float angularSpeed = Mathf.DeltaAngle(previousYaw, currentYaw) / Time.deltaTime;
            previousYaw = currentYaw;

            // Unsmoothed — reacts the instant the player stops turning, so
            // the Idle<->Locomotion transition (gated on Speed) reacts
            // immediately too instead of lingering. Only the visual pose fed
            // into the Blend Tree below (turnLeanSmoothed) eases in/out.
            float rawTurnLean = isStandingStill
                ? Mathf.Clamp(angularSpeed / turnLeanMaxAngularSpeed, -1f, 1f) * walkBlendAxisScale
                : 0f;
            turnLeanSmoothed = Mathf.Lerp(turnLeanSmoothed, rawTurnLean, Mathf.Clamp01(turnLeanSmoothing * Time.deltaTime));

            if (hasSpeedParameter)
                animator.SetFloat(speedParameterName, Mathf.Max(horizontalSpeed, Mathf.Abs(rawTurnLean)), parameterDampTime, Time.deltaTime);

            if (hasMoveXYParameters || hasRightForwardParameters)
            {
                // Local space: x = strafe (right positive), z = forward. The
                // player's own transform yaw already IS the movement basis
                // (see LocalFpsPlayerController.HandleMove), so this reduces
                // to exactly the raw Move input scaled by moveSpeed, plus the
                // turn lean on the strafe axis. Summed rather than
                // hard-switched on isStandingStill: since
                // rawTurnLean (and so turnLeanSmoothed) is already forced to
                // 0 the instant isStandingStill goes false, and
                // localVelocity.x is already near 0 whenever isStandingStill
                // is true, a straight sum behaves the same at both extremes
                // as the old either/or branch did — but with no
                // discontinuity at the threshold. The old hard switch
                // visibly flickered Right/MoveX whenever horizontalSpeed sat
                // right at idleSpeedThreshold (e.g. from CharacterController
                // grounding noise while genuinely stationary), jumping
                // between turnLeanSmoothed and ~0 every time isStandingStill
                // flipped.
                Vector3 localVelocity = transform.InverseTransformDirection(velocity);
                float strafeValue = localVelocity.x + turnLeanSmoothed;
                float forwardValue = localVelocity.z;

                if (hasMoveXYParameters)
                {
                    animator.SetFloat(moveXParameterName, strafeValue, parameterDampTime, Time.deltaTime);
                    animator.SetFloat(moveYParameterName, forwardValue, parameterDampTime, Time.deltaTime);
                }

                if (hasRightForwardParameters)
                {
                    // Re-normalized to -1..1 (see Right Forward Max Speed's
                    // field comment) instead of reusing strafeValue/
                    // forwardValue's ±walkBlendAxisScale range above. Same
                    // continuous strafeValue/forwardValue as MoveX/MoveY —
                    // no separate isStandingStill branch here either.
                    float normalizedStrafe = Mathf.Clamp(strafeValue / rightForwardMaxSpeed, -1f, 1f);
                    float normalizedForward = Mathf.Clamp(forwardValue / rightForwardMaxSpeed, -1f, 1f);
                    animator.SetFloat(rightParameterName, normalizedStrafe, parameterDampTime, Time.deltaTime);
                    animator.SetFloat(forwardParameterName, normalizedForward, parameterDampTime, Time.deltaTime);
                }
            }

            if (hasIsAimingParameter)
                animator.SetBool(isAimingParameterName, poolAimController != null && poolAimController.IsAiming);

            {
                bool isHoldingAnyObject = handController != null && handController.HeldObject != null;
                bool isHoldingCue = isHoldingAnyObject && handController.HeldObject.TryGetComponent(out Cue _);

                if (hasIsHoldingCueParameter)
                    animator.SetBool(isHoldingCueParameterName, isHoldingCue);

                if (hasIsHoldingObjectParameter)
                    animator.SetBool(isHoldingObjectParameterName, isHoldingAnyObject);

                foreach (Behaviour ikComponent in cueOnlyIkComponents)
                    if (ikComponent != null) ikComponent.enabled = isHoldingCue;
            }

            if (hasShootChargeParameter)
                animator.SetFloat(shootChargeParameterName, poolAimController != null ? poolAimController.ChargeFraction : 0f, parameterDampTime, Time.deltaTime);

            if (hasGroundedParameter)
                animator.SetBool(groundedParameterName, controller.isGrounded);
        }
    }
}
