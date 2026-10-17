using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Cue pickup through FinalIK's InteractionSystem (the hands reach for the
    // cue's InteractionTargets, then PickUpCue attaches it). Only on one's
    // turn (CanUseCueNow).
    [RequireComponent(typeof(LocalPlayerHandController))]
    public class LocalCuePickupTrigger : MonoBehaviour
    {
        // Only so HeldObject has something: the cue's PickUp/Drop are never
        // called, pickUpCue does the work.
        [SerializeField] private LocalGrabbable cueGrabbable;

        // Owns the FBBIK reach-and-grab (InteractionSystem) — see
        // PickUpCue.cs.
        [SerializeField] private PickUpCue pickUpCue;

        [Tooltip("Écrit dans la console pourquoi une prise de la queue est refusée (zone de prise hors de portée, orientation, mains occupées…).")]
        [SerializeField] private bool debugLogs = true;

        private LocalPlayerHandController handController;

        public LocalGrabbable CueGrabbable => cueGrabbable;
        public PickUpCue PickUpCue => pickUpCue;

        private void Awake()
        {
            handController = GetComponent<LocalPlayerHandController>();
        }

        // Called by LocalPlayerHandController.TryPickUp; true if the cue was
        // taken. Range and facing are set on the cue's InteractionTrigger and
        // checked by FinalIK.
        public bool TryStartPickup()
        {
            if (cueGrabbable == null || pickUpCue == null || pickUpCue.InteractionSystem == null || pickUpCue.IsBusy) return false;
            if (handController != null && !handController.CanUseCueNow()) return false;

            var interactionSystem = pickUpCue.InteractionSystem;
            int index = interactionSystem.GetClosestTriggerIndex();
            if (index < 0) { LogRefusal(interactionSystem, "no pickup zone in range"); return false; }
            if (!interactionSystem.TriggerEffectorsReady(index)) { LogRefusal(interactionSystem, "hands not ready"); return false; }
            if (!interactionSystem.TriggerInteraction(index, false)) { LogRefusal(interactionSystem, "TriggerInteraction refused"); return false; }

            cueGrabbable.MarkExternallyHeld(true);
            if (handController != null) handController.NotifyExternallyHeld(cueGrabbable);
            return true;
        }

        // Diagnostic: why E near the cue did nothing. Replays FinalIK's
        // character-position test and logs what it depends on (the trigger's
        // flattened forward is zero when the cue stands upright).
        private void LogRefusal(RootMotion.FinalIK.InteractionSystem interactionSystem, string reason)
        {
            if (!debugLogs) return;
            string details = "";
            var trigger = cueGrabbable != null ? cueGrabbable.GetComponentInChildren<RootMotion.FinalIK.InteractionTrigger>() : null;
            if (trigger != null)
            {
                Transform character = interactionSystem.transform;
                Vector3 flatForward = Vector3.ProjectOnPlane(trigger.transform.forward, Vector3.up);
                Vector3 toCue = Vector3.ProjectOnPlane(trigger.transform.position - character.position, Vector3.up);
                details = $" | cue trigger: forward flattened length {flatForward.magnitude:F2} (0 = upright, rejected), " +
                          $"horizontal distance {toCue.magnitude:F2} m, angle between facing and cue {Vector3.Angle(character.forward, toCue):F0}°";
                for (int i = 0; i < trigger.ranges.Length; i++)
                {
                    var cp = trigger.ranges[i].characterPosition;
                    bool ok = cp.IsInRange(character, trigger.transform, out float error);
                    details += $" | range {i}: {(ok ? "OK" : "rejected")} (needs {Mathf.Max(0f, cp.offset.magnitude - cp.radius):F2}–{cp.offset.magnitude + cp.radius:F2} m, facing within {cp.maxAngle:F0}°)";
                    if (ExpectedFacing(cp, character, trigger.transform, out Vector3 expected))
                    {
                        float turn = Vector3.SignedAngle(character.forward, expected, Vector3.up);
                        float fromCue = Vector3.SignedAngle(toCue, expected, Vector3.up);
                        details += $"; FinalIK wants you to face {Mathf.Abs(turn):F0}° to the {(turn >= 0f ? "right" : "left")} of your current facing, " +
                                   $"i.e. {Mathf.Abs(fromCue):F0}° to the {(fromCue >= 0f ? "right" : "left")} of the cue's pickup point (Angle Offset {cp.angleOffset:F0})";
                    }
                }
            }
            Debug.Log($"[CuePickup] {name}: pickup refused — {reason}; pickup zones in range: {interactionSystem.triggersInRange.Count}{details}", this);
        }

        // Same maths as InteractionTrigger.CharacterPosition.IsInRange, for the
        // diagnostic only.
        private static bool ExpectedFacing(RootMotion.FinalIK.InteractionTrigger.CharacterPosition cp, Transform character, Transform trigger, out Vector3 facing)
        {
            facing = Vector3.zero;
            Vector3 forward = trigger.forward;
            if (cp.fixYAxis) forward.y = 0f;
            if (forward == Vector3.zero) return false;
            Vector3 up = cp.fixYAxis ? Vector3.up : trigger.up;
            Quaternion triggerRotation = Quaternion.LookRotation(forward, up);
            Vector3 position = trigger.position + triggerRotation * cp.offset3D;

            Vector3 d = triggerRotation * cp.direction3D;
            Vector3 upD = up;
            Vector3.OrthoNormalize(ref upD, ref d);

            if (cp.orbit)
            {
                Vector3 toCharacter = character.position - trigger.position;
                Vector3 upC = up;
                Vector3.OrthoNormalize(ref upC, ref toCharacter);
                Vector3 toPosition = position - trigger.position;
                if (toPosition == Vector3.zero) toPosition = Vector3.forward;
                Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(toPosition, up)) * toCharacter;
                d = Quaternion.AngleAxis(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, up) * d;
            }
            facing = d;
            return true;
        }

        // Called by Drop when the held object is this cue; also cuts a reach
        // short.
        public bool TryReleaseIfCue(LocalGrabbable grabbable)
        {
            if (grabbable != cueGrabbable || pickUpCue == null || !pickUpCue.IsBusy) return false;

            pickUpCue.ReleaseCue();
            cueGrabbable.MarkExternallyHeld(false);
            return true;
        }
    }
}
