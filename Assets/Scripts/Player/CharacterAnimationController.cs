using Unity.Netcode;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // Drives the visible character's locomotion animation from the owner's
    // own CharacterController — see LocalCharacterAnimationController for the
    // offline/split-screen counterpart. Only the owner runs this (enabled =
    // IsOwner, same convention as FpsPlayerController); a NetworkAnimator
    // component (added in the Inspector, pointed at the same Animator)
    // replicates the resulting parameter values to everyone else's copy of
    // this player, so non-owners see the animation play without needing to
    // run this script themselves — CharacterController.velocity only means
    // anything on the instance actually calling Move() on it.
    [RequireComponent(typeof(CharacterController))]
    public class CharacterAnimationController : NetworkBehaviour
    {
        // Assigned by hand in the Inspector to the character model's own
        // Animator, rather than found via GetComponentInChildren — same
        // reasoning as PoolAimController's cameraTransform field: avoids
        // silently grabbing the wrong Animator if the rig ever has more than
        // one (e.g. a held prop with its own).
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameterName = "Speed";

        // Optional bonus — only actually set if the assigned Animator
        // Controller has a bool parameter by this name (checked once below).
        // Not every character pack ships an aiming pose yet; leaving this
        // unwired there is harmless, not an error.
        [SerializeField] private string isAimingParameterName = "IsAiming";

        // Signed local-space velocity, for a 2D locomotion Blend Tree (strafe
        // left/right + walk forward as separate clips instead of one generic
        // "Speed" magnitude). Same optional-parameter pattern as IsAiming —
        // harmless if the assigned controller doesn't have a strafe blend yet.
        [SerializeField] private string moveXParameterName = "MoveX";
        [SerializeField] private string moveYParameterName = "MoveY";

        // Matches the Blend Tree's forward/strafe motion field positions
        // (also 5 by default) — used to scale the turn-lean value below onto
        // the same axis range the Blend Tree expects.
        [SerializeField] private float walkBlendAxisScale = 5f;

        // Below this, the Idle state is playing (or about to) — must match
        // the "Speed Less/Greater 0.1" transition conditions in the Animator
        // Controller. Keeps Animator.speed at 1 in Idle instead of freezing
        // it at ~0 while nearly stopped.
        [SerializeField] private float idleSpeedThreshold = 0.1f;

        // Body yaw follows the camera directly (see FpsPlayerController) —
        // with no clip for "turn in place", looking around while standing
        // still used to just snap-rotate the whole body with zero animation
        // reaction. Instead of authoring a dedicated turn clip, a fast turn
        // while otherwise idle leans into the existing strafe pose (lerped
        // in/out, so it reads as a weight shift rather than a snap).
        [SerializeField] private float turnLeanMaxAngularSpeed = 180f;
        [SerializeField] private float turnLeanSmoothing = 6f;

        private CharacterController controller;
        private PoolAimController poolAimController;
        private bool hasIsAimingParameter;
        private bool hasMoveXYParameters;
        private float previousYaw;
        private float turnLeanSmoothed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            poolAimController = GetComponent<PoolAimController>();
            hasIsAimingParameter = AnimatorHasBool(animator, isAimingParameterName);
            hasMoveXYParameters = AnimatorHasFloat(animator, moveXParameterName) && AnimatorHasFloat(animator, moveYParameterName);
            previousYaw = transform.eulerAngles.y;
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

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;
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

            animator.SetFloat(speedParameterName, Mathf.Max(horizontalSpeed, Mathf.Abs(rawTurnLean)));

            if (hasMoveXYParameters)
            {
                // Local space: x = strafe (right positive), z = forward. The
                // player's own transform yaw already IS the movement basis
                // (see FpsPlayerController.HandleMove), so this reduces to
                // exactly the raw Move input scaled by moveSpeed. While
                // standing still, MoveX is driven by the turn lean instead.
                Vector3 localVelocity = transform.InverseTransformDirection(velocity);
                animator.SetFloat(moveXParameterName, isStandingStill ? turnLeanSmoothed : localVelocity.x);
                animator.SetFloat(moveYParameterName, isStandingStill ? 0f : localVelocity.z);
            }

            if (hasIsAimingParameter)
                animator.SetBool(isAimingParameterName, poolAimController != null && poolAimController.IsAiming);
        }
    }
}
