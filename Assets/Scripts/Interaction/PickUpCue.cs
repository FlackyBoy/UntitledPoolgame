using RootMotion.FinalIK;
using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Attaches the cue once the InteractionSystem's hands reach it and brings
    // it to its held pose. Started by LocalCuePickupTrigger, released by
    // ReleaseCue. Adapted from FinalIK's PickUp2Handed demo rather than
    // inherited, so a FinalIK update can't undo our changes (primary
    // effector, fixed held rotation, hold pose that stops once reached).
    public class PickUpCue : MonoBehaviour
    {
        [SerializeField] private InteractionSystem interactionSystem;
        [SerializeField] private InteractionObject obj;
        // Parent of the cue's InteractionTargets — its rotation is the held
        // rotation when Use Fixed Held Rotation is off.
        [SerializeField] private Transform pivot;
        // Where the cue ends up once held (its rotation is overwritten at the
        // start of each pickup, see OnStart).
        [SerializeField] private Transform holdPoint;
        // Smoothing time of the move into the held pose — higher feels heavier.
        [SerializeField] private float pickUpTime = 0.3f;

        // Its events drive the cue's state. Must be an effector the cue's
        // InteractionTrigger starts, or the cue never attaches.
        [SerializeField] private FullBodyBipedEffector primaryEffector = FullBodyBipedEffector.LeftHand;

        // On: held rotation = Hold Rotation Offset, however the cue was lying.
        // Off: copies Pivot's rotation when the pickup starts.
        [SerializeField] private bool useFixedHeldRotation = true;
        // Tune with the "Log held cue rotation offset" context menu below.
        [SerializeField] private Vector3 holdRotationOffset = new Vector3(0f, 0f, 90f);

        // Arm bend goals only matter with the cue in hand (they pulled the
        // elbows otherwise): 0 without it, blended to this value once held.
        // Written only while blending, so the punches' goals aren't overwritten.
        [Header("Arm bend goals")]
        [Tooltip("Poids des bend goals des bras (chaînes Left/Right Arm du FBBIK) quand la queue est tenue. Sans la queue, il est à 0.")]
        [SerializeField] private float heldArmBendWeight = 1f;
        [Tooltip("Durée (secondes) du fondu du poids des bend goals à la prise et au lâcher de la queue.")]
        [SerializeField] private float armBendBlendTime = 0.2f;

        private float armBend;
        private float holdWeight;
        private float holdWeightVel;
        private Vector3 pickUpPosition;
        private Quaternion pickUpRotation;

        public InteractionSystem InteractionSystem => interactionSystem;

        // From the start of the reach until fully let go: a second Interact
        // during the reach left the cue stuck to the hand.
        public bool IsBusy =>
            interactionSystem != null && obj != null &&
            interactionSystem.GetInteractionObject(primaryEffector) == obj &&
            (interactionSystem.IsPaused(primaryEffector) || interactionSystem.IsInInteraction(primaryEffector));

        // Held and in pose: LateUpdate stops writing the cue, so the swing can
        // move it.
        public bool IsSettled => interactionSystem != null && obj != null && Holding && holdWeight >= 0.999f;

        private Quaternion HeldRotation => interactionSystem.transform.rotation * Quaternion.Euler(holdRotationOffset);

        // Carry pose, reused by LocalCueHolder.
        public Transform HoldPoint => holdPoint;
        public Quaternion CarryRotation => HeldRotation;
        public Transform CarryParent => interactionSystem != null ? interactionSystem.transform : null;

        private bool Holding =>
            IsHoldingWith(FullBodyBipedEffector.LeftHand) || IsHoldingWith(FullBodyBipedEffector.RightHand);

        private bool IsHoldingWith(FullBodyBipedEffector effector) =>
            interactionSystem.IsPaused(effector) && interactionSystem.GetInteractionObject(effector) == obj;

        private void Start()
        {
            if (interactionSystem == null)
            {
                Debug.LogWarning("[PickUpCue] No Interaction System assigned — the cue can't be picked up.", this);
                return;
            }

            interactionSystem.OnInteractionStart += OnStart;
            interactionSystem.OnInteractionPause += OnPause;
            interactionSystem.OnInteractionResume += OnDrop;

            // Not holding yet: arms free of the cue's bend goals.
            armBend = 0f;
            SetArmBendWeight(0f);
        }

        private void SetArmBendWeight(float weight)
        {
            if (interactionSystem.ik == null) return;
            interactionSystem.ik.solver.leftArmChain.bendConstraint.weight = weight;
            interactionSystem.ik.solver.rightArmChain.bendConstraint.weight = weight;
        }

        // Blends toward 1 while the hands hold the cue, 0 otherwise; writes
        // only while the value is changing.
        private void UpdateArmBend()
        {
            float target = Holding ? 1f : 0f;
            if (Mathf.Approximately(armBend, target)) return;
            armBend = Mathf.MoveTowards(armBend, target, Time.deltaTime / Mathf.Max(0.01f, armBendBlendTime));
            SetArmBendWeight(armBend * heldArmBendWeight);
        }

        private void OnDestroy()
        {
            if (interactionSystem == null) return;

            interactionSystem.OnInteractionStart -= OnStart;
            interactionSystem.OnInteractionPause -= OnPause;
            interactionSystem.OnInteractionResume -= OnDrop;
        }

        // Mid-reach the cue isn't attached yet: the interaction is cut short and
        // the cue stays where it was.
        public void ReleaseCue()
        {
            if (interactionSystem == null) return;

            if (Holding) interactionSystem.ResumeAll();
            else interactionSystem.StopAll();
        }

        private void OnStart(FullBodyBipedEffector effectorType, InteractionObject interactionObject)
        {
            if (effectorType != primaryEffector || interactionObject != obj || holdPoint == null) return;

            if (useFixedHeldRotation)
                holdPoint.rotation = HeldRotation;
            else if (pivot != null)
                holdPoint.rotation = pivot.rotation;
            else
                holdPoint.rotation = obj.transform.rotation;
        }

        // Fired when the hand has reached the cue.
        private void OnPause(FullBodyBipedEffector effectorType, InteractionObject interactionObject)
        {
            if (effectorType != primaryEffector || interactionObject != obj) return;

            obj.transform.parent = interactionSystem.transform;
            if (obj.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;

            pickUpPosition = obj.transform.position;
            pickUpRotation = obj.transform.rotation;
            holdWeight = 0f;
            holdWeightVel = 0f;
        }

        private void OnDrop(FullBodyBipedEffector effectorType, InteractionObject interactionObject)
        {
            if (Holding || interactionObject != obj) return;

            obj.transform.parent = null;
            if (obj.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;
        }

        // Only while moving into the held pose: holdPoint sways with the body
        // and chasing it made the cue drift. Then it rides along as a child.
        private void LateUpdate()
        {
            if (interactionSystem == null || obj == null) return;
            UpdateArmBend();
            if (holdPoint == null) return;
            if (!Holding || holdWeight >= 0.999f) return;

            holdWeight = Mathf.SmoothDamp(holdWeight, 1f, ref holdWeightVel, pickUpTime);
            obj.transform.SetPositionAndRotation(
                Vector3.Lerp(pickUpPosition, holdPoint.position, holdWeight),
                Quaternion.Lerp(pickUpRotation, holdPoint.rotation, holdWeight));
        }

        // Logs the cue's rotation in Hold Rotation Offset terms. Turn Use Fixed
        // Held Rotation off, pick the cue up so it looks right, run this from
        // the component's menu, copy the value, turn the option back on.
        [ContextMenu("Log held cue rotation offset")]
        private void LogHeldRotationOffset()
        {
            if (interactionSystem == null || obj == null) return;

            Vector3 euler = (Quaternion.Inverse(interactionSystem.transform.rotation) * obj.transform.rotation).eulerAngles;
            Debug.Log($"[PickUpCue] Hold Rotation Offset for the current pose: ({euler.x:F1}, {euler.y:F1}, {euler.z:F1})", this);
        }
    }
}
