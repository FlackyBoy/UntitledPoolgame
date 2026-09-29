using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Routes the cue's pickup/release through FinalIK's InteractionSystem
    // (the hands reach for InteractionTargets on the cue, then PickUpCue
    // attaches it) instead of LocalGrabbable's plain snap-to-hand used for
    // every other object. Also gated behind whose turn it is (CanUseCueNow),
    // so the player who isn't up can't steal it and hold the other one's
    // turn hostage.
    [RequireComponent(typeof(LocalPlayerHandController))]
    public class LocalCuePickupTrigger : MonoBehaviour
    {
        // Bookkeeping token only — LocalPlayerHandController.HeldObject is
        // typed LocalGrabbable, so the cue still needs one, but its own
        // PickUp()/Drop() are never called for the cue (see MarkExternallyHeld
        // on LocalGrabbable). The actual attach/release goes through pickUpCue.
        [SerializeField] private LocalGrabbable cueGrabbable;

        // Owns the FBBIK reach-and-grab (InteractionSystem) — see
        // PickUpCue.cs.
        [SerializeField] private PickUpCue pickUpCue;

        private LocalPlayerHandController handController;

        public LocalGrabbable CueGrabbable => cueGrabbable;
        public PickUpCue PickUpCue => pickUpCue;

        private void Awake()
        {
            handController = GetComponent<LocalPlayerHandController>();
        }

        // Called by LocalPlayerHandController.TryPickUp(). Returns true if
        // it actually picked up the cue (so the caller knows not to also
        // fall through to its own generic pickup logic).
        //
        // Range/facing/camera gating is declared on the cue's own
        // InteractionTrigger component (Ranges -> Character Position /
        // Camera Position) and evaluated by FinalIK itself via
        // GetClosestTriggerIndex(). TriggerInteraction() then starts exactly
        // the effectors listed in that trigger's Interactions.
        public bool TryStartPickup()
        {
            if (cueGrabbable == null || pickUpCue == null || pickUpCue.InteractionSystem == null || pickUpCue.IsBusy) return false;
            if (handController != null && !handController.CanUseCueNow()) return false;

            var interactionSystem = pickUpCue.InteractionSystem;
            int index = interactionSystem.GetClosestTriggerIndex();
            if (index < 0) return false;
            if (!interactionSystem.TriggerEffectorsReady(index)) return false;
            if (!interactionSystem.TriggerInteraction(index, false)) return false;

            cueGrabbable.MarkExternallyHeld(true);
            if (handController != null) handController.NotifyExternallyHeld(cueGrabbable);
            return true;
        }

        // Called by LocalPlayerHandController.Drop() instead of a plain
        // LocalGrabbable.Drop() whenever the held object is this cue. Also
        // covers a hand still reaching for it (IsBusy), which is cut short.
        public bool TryReleaseIfCue(LocalGrabbable grabbable)
        {
            if (grabbable != cueGrabbable || pickUpCue == null || !pickUpCue.IsBusy) return false;

            pickUpCue.ReleaseCue();
            cueGrabbable.MarkExternallyHeld(false);
            return true;
        }
    }
}
