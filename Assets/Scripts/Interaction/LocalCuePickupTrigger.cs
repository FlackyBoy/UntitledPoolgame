using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Gates the cue's pickup behind Interact (E) — only within pickupRange
    // AND roughly facing the cue, unlike LocalGrabbable's plain proximity-
    // only pickup used for every other object — and behind whose turn it
    // is (CanUseCueNow), so the player who isn't up can't steal it and hold
    // the other one's turn hostage.
    //
    // 2026-09-22: tried a plain parent-with-fixed-local-offset (instant
    // pickup, no reach) as an alternative to InteractionSystem — abandoned
    // in turn (single-hand only; no clean way to also grip a second point
    // further down the cue for a two-handed hold). Back on InteractionSystem
    // (PickUp2Handed/PickUpCue — see that class): the two-hand reach targets
    // InteractionTargets on the cue itself, so both hands land wherever
    // those targets are placed, however the cue ends up oriented once
    // released. See CHANGELOG for the known open issues on this path
    // (orientation depending on approach angle, holdPoint drift — the
    // holdWeight guard in PickUp2Handed.LateUpdate already fixes the drift
    // one).
    [RequireComponent(typeof(LocalPlayerHandController))]
    public class LocalCuePickupTrigger : MonoBehaviour
    {
        // Bookkeeping token only — LocalPlayerHandController.HeldObject is
        // typed LocalGrabbable, so the cue still needs one, but its own
        // PickUp()/Drop() are never called for the cue (see MarkExternallyHeld
        // on LocalGrabbable). The actual attach/release goes through pickUpCue.
        [SerializeField] private LocalGrabbable cueGrabbable;

        // Owns the two-hand FBBIK reach-and-grab (InteractionSystem) —
        // see PickUpCue.cs.
        [SerializeField] private PickUpCue pickUpCue;

        private LocalPlayerHandController handController;

        public LocalGrabbable CueGrabbable => cueGrabbable;

        private void Awake()
        {
            handController = GetComponent<LocalPlayerHandController>();
        }

        // Called by LocalPlayerHandController.TryPickUp(). Returns true if
        // it actually picked up the cue (so the caller knows not to also
        // fall through to its own generic pickup logic).
        //
        // Range/facing/camera gating is no longer done here — it's declared
        // on the cue's own InteractionTrigger component (Ranges ->
        // Character Position / Camera Position) and evaluated by FinalIK
        // itself via GetClosestTriggerIndex(). TriggerInteraction() then
        // starts exactly the effectors listed in that trigger's Interactions
        // (Right Hand only, currently) — same StartInteraction() call
        // PickUp2Handed/PickUpCue already listens to, so the reparent-on-
        // pause logic in PickUpCue still fires as before.
        public bool TryStartPickup()
        {
            if (cueGrabbable == null || pickUpCue == null || pickUpCue.interactionSystem == null || pickUpCue.IsHeld) return false;
            if (handController != null && !handController.CanUseCueNow()) return false;

            var interactionSystem = pickUpCue.interactionSystem;
            int index = interactionSystem.GetClosestTriggerIndex();
            if (index < 0) return false;
            if (!interactionSystem.TriggerEffectorsReady(index)) return false;
            if (!interactionSystem.TriggerInteraction(index, false)) return false;

            cueGrabbable.MarkExternallyHeld(true);
            if (handController != null) handController.NotifyExternallyHeld(cueGrabbable);
            return true;
        }

        // Called by LocalPlayerHandController.Drop() instead of a plain
        // LocalGrabbable.Drop() whenever the held object is this cue — kept
        // as its own method (rather than just letting the generic Drop()
        // handle it) in case the cue needs its own release behaviour again
        // later (a throw, a melee swing, etc.).
        public bool TryReleaseIfCue(LocalGrabbable grabbable)
        {
            if (grabbable != cueGrabbable || pickUpCue == null || !pickUpCue.IsHeld) return false;

            pickUpCue.ReleaseCue();
            cueGrabbable.MarkExternallyHeld(false);
            return true;
        }
    }
}
