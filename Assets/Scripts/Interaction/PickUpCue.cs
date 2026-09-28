using UnityEngine;
using RootMotion;
using RootMotion.FinalIK;
using RootMotion.Demos;

namespace UntitledPoolGame.Interaction
{
    // PickUp2Handed subclass for a rod-shaped object (the pool cue) — see
    // RootMotion's own PickUpBox/PickUpSphere for the box/sphere
    // equivalents. Neither fits a cue: PickUpBox snaps to the object's
    // nearest 90-degree box face (meaningless for a cylinder), PickUpSphere
    // just points the pivot from the hands toward the object (fine for a
    // sphere, which looks the same held at any rotation, but a cue needs a
    // specific "held horizontally, pointing forward" pose regardless of
    // whatever angle it happened to be lying at when picked up).
    //
    // Both pivot (parent of the two hands' InteractionTargets) and
    // holdPoint (what the object's own Transform lerps toward once grabbed
    // — see PickUp2Handed.OnPause/LateUpdate) are set to the SAME fixed
    // rotation, relative to the character's own facing, so the cue always
    // ends up in one predictable held pose.
    public class PickUpCue : PickUp2Handed
    {
        // Tune in Play Mode until the cue ends up held horizontally,
        // pointing forward, then Copy Component -> stop Play Mode ->
        // Paste Component Values to keep the found offset.
        [SerializeField] private Vector3 holdRotationOffset = new Vector3(0f, 0f, 90f);

        // Unused since RotatePivot no longer applies it automatically (see
        // below) — kept only in case a future feature needs a reference
        // "queue facing the player" rotation.
        public Quaternion HeldRotation => interactionSystem.transform.rotation * Quaternion.Euler(holdRotationOffset);

        // Used to force pivot.rotation (= the cue itself) to HeldRotation
        // every time a reach starts — this ran automatically, via
        // PickUp2Handed's own OnInteractionStart subscription, and silently
        // overwrote whatever orientation the cue already had (whether lying
        // on the table, or set up by LocalCuePickupTrigger) the instant
        // StartInteraction() fired. Confirmed via A/B test (2026-09-22 — see
        // CHANGELOG) that the plain "Pick Up" GUI button — StartInteraction
        // with no extra logic — gives a correct result once the real
        // underlying fixes (Twist Axis, Hold Rotation Offset) are in place,
        // so this is left as a no-op: holdPoint is just kept in sync with
        // wherever the cue actually is, matching what the button already
        // does, rather than forcing any particular rotation.
        protected override void RotatePivot()
        {
            if (holdPoint == null) return;

            // Fixed, body-relative held rotation (Hold Rotation Offset above),
            // so the cue ends up identically oriented however it was lying
            // when picked up. Only holdPoint's rotation is set — never
            // pivot's (that's what rotated the hand targets at the start of
            // the reach and caused the original problems, see above).
            if (useFixedHeldRotation)
                holdPoint.rotation = HeldRotation;
            else if (pivot != null)
                holdPoint.rotation = pivot.rotation;
        }

        // Reads the cue's CURRENT rotation relative to the character and logs
        // it in Hold Rotation Offset's own terms. Use: turn Use Fixed Held
        // Rotation off, pick the cue up so it looks right and the body stands
        // straight, right-click this component's header -> "Log held cue
        // rotation offset", copy the logged value into Hold Rotation Offset,
        // then turn Use Fixed Held Rotation back on.
        [ContextMenu("Log held cue rotation offset")]
        private void LogHeldRotationOffset()
        {
            if (interactionSystem == null || obj == null) return;

            Vector3 euler = (Quaternion.Inverse(interactionSystem.transform.rotation) * obj.transform.rotation).eulerAngles;
            Debug.Log($"[PickUpCue] Hold Rotation Offset for the current pose: ({euler.x:F1}, {euler.y:F1}, {euler.z:F1})", this);
        }

        // On: the held rotation is Hold Rotation Offset relative to the
        // character (same every pickup). Off: back to copying whatever
        // Pivot's rotation happens to be when the pickup starts, which
        // follows how the cue was lying.
        [SerializeField] private bool useFixedHeldRotation = true;

        // One hand only for now — see PickUp2Handed.primaryEffector (set it
        // to Right Hand in the Inspector on this component to use the right
        // hand; the cue's single grip InteractionTarget's own Effector Type
        // must match it).
        public bool IsHeld =>
            interactionSystem != null && obj != null &&
            interactionSystem.IsPaused(primaryEffector) &&
            interactionSystem.GetInteractionObject(primaryEffector) == obj;

        public void PickUp()
        {
            if (interactionSystem == null || obj == null) return;
            interactionSystem.StartInteraction(primaryEffector, obj, false);
        }

        // Same as the demo's own "Drop" GUI button (PickUp2Handed.OnGUI).
        public void ReleaseCue()
        {
            if (interactionSystem == null) return;
            interactionSystem.ResumeAll();
        }
    }
}
