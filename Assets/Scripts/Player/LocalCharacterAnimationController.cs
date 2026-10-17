using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // Drives the visible character's Animator from its own CharacterController
    // (locomotion) and from what it's doing (holding, aiming).
    [RequireComponent(typeof(CharacterController))]
    public class LocalCharacterAnimationController : MonoBehaviour
    {
        // Assigned by hand so a prop with its own Animator can't be picked.
        [SerializeField] private Animator animator;

        // The character's Animator, for other components that drive their
        // own parameters (LocalPlayerLocomotion: Jump, JumpLeg, Crouch).
        public Animator Animator => animator;

        // Optional (our simple Idle/Locomotion controller). RootMotion's
        // demo controllers have no speed parameter.
        [SerializeField] private string speedParameterName = "Speed";

        // Optional — harmless if the assigned Animator Controller has no
        // such bool parameter.
        [SerializeField] private string isAimingParameterName = "IsAiming";

        // Optional: the held object is the cue, aiming or not.
        [SerializeField] private string isHoldingCueParameterName = "IsHoldingCue";

        // Optional: any object held. With IsHoldingCue false, it means
        // "holding something else".
        [SerializeField] private string isHoldingObjectParameterName = "IsHoldingObject";

        // Behaviours that only run while the cue is held: IK aiming at the
        // cue's targets would pull empty hands around.
        [SerializeField] private Behaviour[] cueOnlyIkComponents;

        // Optional: the shot's charge (0..1), so a Blend Tree can draw the cue
        // back as it charges.
        [SerializeField] private string shootChargeParameterName = "ShootCharge";

        // Optional: local velocity for a 2D locomotion Blend Tree.
        [SerializeField] private string moveXParameterName = "MoveX";
        [SerializeField] private string moveYParameterName = "MoveY";

        // The same axes under the names of RootMotion's demo controllers
        // (Right, Forward). Kept apart from MoveX/MoveY so a controller only
        // ever matches one naming scheme.
        [SerializeField] private string rightParameterName = "Right";
        [SerializeField] private string forwardParameterName = "Forward";

        // Right/Forward expect -1..1: velocity is divided by this (about the
        // fastest move speed).
        [SerializeField] private float rightForwardMaxSpeed = 5f;

        // Those controllers can also turn the body to face movement instead
        // of strafing. This character always strafes (its yaw follows the
        // look), so it's kept true.
        [SerializeField] private string isStrafingParameterName = "IsStrafing";

        // Same controllers gate jump and fall on it; fed from isGrounded.
        [SerializeField] private string groundedParameterName = "OnGround";

        // Range of the Blend Tree's axes, for the turn lean below.
        [SerializeField] private float walkBlendAxisScale = 5f;

        // Must match the Animator's Speed 0.1 transitions. Keeps the
        // Animator at normal speed in Idle.
        [SerializeField] private float idleSpeedThreshold = 0.1f;

        // There's no turn-in-place clip: turning fast while standing leans
        // into the strafe pose instead of a bare snap.
        [SerializeField] private float turnLeanMaxAngularSpeed = 180f;
        [SerializeField] private float turnLeanSmoothing = 6f;

        // Damping of the float parameters, smooths sudden stops and reversals.
        // Keep small, 0 = instant.
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
        private bool hasIsStrafingParameter;
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

            // Always true — see the field comment above; also re-asserted
            // every frame in Update (parameters can be reset by a rebind).
            hasIsStrafingParameter = AnimatorHasBool(animator, isStrafingParameterName);
            if (hasIsStrafingParameter) animator.SetBool(isStrafingParameterName, true);
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

            // Every frame: Unity resets the parameters when the Animator is
            // rebound (disabled, reactivated), and IsStrafing false would
            // leave it stuck in Grounded Directional.
            if (hasIsStrafingParameter) animator.SetBool(isStrafingParameterName, true);

            // Paused (timeScale 0): the turn speed would be NaN and stay in
            // the smoothing for good.
            if (Time.deltaTime <= 0f)
            {
                previousYaw = transform.eulerAngles.y;
                return;
            }

            // Horizontal only — gravity's fall speed shouldn't blend toward
            // a running animation while airborne/falling.
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            float horizontalSpeed = velocity.magnitude;
            bool isStandingStill = horizontalSpeed <= idleSpeedThreshold;

            float currentYaw = transform.eulerAngles.y;
            float angularSpeed = Mathf.DeltaAngle(previousYaw, currentYaw) / Time.deltaTime;
            previousYaw = currentYaw;

            // Unsmoothed, so Idle/Locomotion reacts at once; only the pose
            // value (turnLeanSmoothed) eases.
            float rawTurnLean = isStandingStill
                ? Mathf.Clamp(angularSpeed / turnLeanMaxAngularSpeed, -1f, 1f) * walkBlendAxisScale
                : 0f;
            turnLeanSmoothed = Mathf.Lerp(turnLeanSmoothed, rawTurnLean, Mathf.Clamp01(turnLeanSmoothing * Time.deltaTime));

            if (hasSpeedParameter)
                animator.SetFloat(speedParameterName, Mathf.Max(horizontalSpeed, Mathf.Abs(rawTurnLean)), parameterDampTime, Time.deltaTime);

            if (hasMoveXYParameters || hasRightForwardParameters)
            {
                // Local velocity (x strafe, z forward) plus the turn lean. A sum
                // rather than a switch on isStandingStill: the switch flickered
                // when the speed sat at the threshold.
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
                    // Normalized to -1..1 for Right/Forward.
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
                bool isHoldingCue = isHoldingAnyObject && handController.HeldObject.IsCue;

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
