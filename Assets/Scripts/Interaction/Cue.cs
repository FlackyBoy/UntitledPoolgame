using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // A pool cue. Marks the LocalGrabbable as a cue (LocalPoolAimController
    // only aims with one, LocalPlayerHandController routes its pickup
    // separately), and gathers what the players need from it, found on the
    // cue itself, so nothing is wired between players and cues in a scene:
    // cues placed in the level and cues spawned at runtime register
    // themselves while enabled (All), and any player can pick up any free
    // cue (LocalCueHolder).
    public class Cue : MonoBehaviour
    {
        private static readonly List<Cue> all = new List<Cue>();

        // Every cue in the scene, placed or spawned. Registered from Awake
        // and removed in OnDestroy — not OnEnable/OnDisable: the component
        // is unchecked on the cue prefabs (it used to be a plain marker, so
        // its checkbox never mattered), which kept every cue out of the list.
        // Whether the cue is actually in play is its GameObject being active
        // (IsFree checks it).
        public static IReadOnlyList<Cue> All => all;

        public LocalGrabbable Grabbable { get; private set; }
        public CueChargeSlide ChargeSlide { get; private set; }
        // The carry grips: where each hand holds the cue, and how (the
        // InteractionTargets' poses, also used to close the fingers).
        public InteractionTarget LeftGrip { get; private set; }
        public InteractionTarget RightGrip { get; private set; }

        // In play (active in the scene) and not in someone's hands (nor
        // being reached for).
        public bool IsFree => gameObject.activeInHierarchy && Grabbable != null && !Grabbable.IsHeld;

        // Has everything LocalCueHolder needs to pick it up.
        public bool IsUsable => Grabbable != null && ChargeSlide != null && LeftGrip != null && RightGrip != null;

        private void Awake()
        {
            all.Add(this);
            Grabbable = GetComponent<LocalGrabbable>();
            ChargeSlide = GetComponent<CueChargeSlide>();
            foreach (InteractionTarget grip in GetComponentsInChildren<InteractionTarget>(true))
            {
                if (grip.effectorType == FullBodyBipedEffector.LeftHand) LeftGrip = grip;
                else if (grip.effectorType == FullBodyBipedEffector.RightHand) RightGrip = grip;
            }

            if (!IsUsable)
                Debug.LogWarning($"[Cue] {name} can't be picked up: missing " +
                                 $"{(Grabbable == null ? "LocalGrabbable " : "")}{(ChargeSlide == null ? "CueChargeSlide " : "")}" +
                                 $"{(LeftGrip == null ? "Left Hand InteractionTarget " : "")}{(RightGrip == null ? "Right Hand InteractionTarget" : "")}", this);
        }

        private void OnDestroy() => all.Remove(this);
    }
}
