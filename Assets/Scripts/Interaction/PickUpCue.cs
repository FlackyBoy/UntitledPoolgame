using RootMotion.FinalIK;
using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Attaches the cue to the character once the InteractionSystem's hands
    // reach it, and brings it into its held pose. Started from
    // LocalCuePickupTrigger (InteractionSystem.TriggerInteraction), released
    // through ReleaseCue.
    //
    // Adapted from FinalIK's demo PickUp2Handed (Plugins/RootMotion/FinalIK/
    // _DEMOS/FBBIK/Scripts) rather than subclassing it, so a FinalIK update
    // can't silently undo the changes this needs: a configurable primary
    // effector (the demo hardcodes the left hand), a fixed body-relative held
    // rotation, and a hold pose that stops chasing holdPoint once reached.
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

        // Whose Start/Pause events drive the cue's own state (parenting,
        // kinematic, held pose). Must be one of the effectors the cue's
        // InteractionTrigger actually starts, or the cue never attaches.
        [SerializeField] private FullBodyBipedEffector primaryEffector = FullBodyBipedEffector.LeftHand;

        // On: the held rotation is Hold Rotation Offset relative to the
        // character, identical however the cue was lying. Off: it copies
        // Pivot's rotation at the moment the pickup starts, which follows how
        // the cue was lying.
        [SerializeField] private bool useFixedHeldRotation = true;
        // Tune with the "Log held cue rotation offset" context menu below.
        [SerializeField] private Vector3 holdRotationOffset = new Vector3(0f, 0f, 90f);

        // The arm bend goals (FBBIK Left/Right Arm chains) only make sense
        // with the cue in hand: without it they kept pulling the elbows
        // toward the cue-holding pose. Weight 0 when not holding, blended up
        // to this value once the hands are on the cue, and back down on
        // release. Only written while blending, so the fists' own elbow
        // goals (LocalUnarmedMelee, hands empty) aren't overwritten.
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

        // True from the moment a hand starts reaching for the cue until it has
        // fully let go of it — not just while actually holding it. Treating the
        // reach as "not held" let a second Interact press during it fall back to
        // a plain LocalGrabbable.Drop() while the reach carried on, leaving the
        // cue attached to the hand with nothing tracking it.
        public bool IsBusy =>
            interactionSystem != null && obj != null &&
            interactionSystem.GetInteractionObject(primaryEffector) == obj &&
            (interactionSystem.IsPaused(primaryEffector) || interactionSystem.IsInInteraction(primaryEffector));

        // Held AND done moving into the held pose — from then on LateUpdate
        // no longer writes the cue's transform, so another script
        // (LocalCueMelee's swing) can move it without being overwritten.
        public bool IsSettled => interactionSystem != null && obj != null && Holding && holdWeight >= 0.999f;

        private Quaternion HeldRotation => interactionSystem.transform.rotation * Quaternion.Euler(holdRotationOffset);

        // Read by LocalCueHolder (the procedural pickup), which reuses this
        // component's carry pose rather than duplicating its settings: where
        // the cue ends up once held, its fixed rotation there, and the
        // transform it's parented to.
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

        // Once held: mid-reach, the hands haven't attached the cue yet, so the
        // interaction is cut short (Stop doesn't fire the pause that would
        // attach it) and it stays where it was lying.
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

        // Only while moving into the held pose: holdPoint sits on the spine and
        // sways with the body, so chasing it for the whole hold made the cue
        // drift further off the longer it was carried. Once there, the cue just
        // rides along as a child of the character (see OnPause), and
        // LocalPoolAimController is free to move it while aiming.
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

        // Logs the cue's current rotation relative to the character, in Hold
        // Rotation Offset's own terms. Use: turn Use Fixed Held Rotation off,
        // pick the cue up so it looks right and the body stands straight,
        // right-click this component's header -> this entry, copy the logged
        // value into Hold Rotation Offset, turn Use Fixed Held Rotation back on.
        [ContextMenu("Log held cue rotation offset")]
        private void LogHeldRotationOffset()
        {
            if (interactionSystem == null || obj == null) return;

            Vector3 euler = (Quaternion.Inverse(interactionSystem.transform.rotation) * obj.transform.rotation).eulerAngles;
            Debug.Log($"[PickUpCue] Hold Rotation Offset for the current pose: ({euler.x:F1}, {euler.y:F1}, {euler.z:F1})", this);
        }
    }
}
