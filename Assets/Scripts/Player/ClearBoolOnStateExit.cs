using UnityEngine;

namespace UntitledPoolGame.Player
{
    // Attached directly to an Animator state (via its "Add Behaviour"
    // button), not a regular component on a GameObject — clears the named
    // Bool parameter the instant this state's exit transition fires, in the
    // very same evaluation step. Needed because MMRagdoller sets
    // GetUpFromBack/GetUpFromBelly true to pick a get-up clip but never
    // clears it afterwards (only the next ragdoll cycle does) — left alone,
    // the moment the Animator reaches Idle the same "Any State" condition
    // is still satisfied and immediately replays the get-up clip forever.
    // Clearing it from a normal MonoBehaviour polling GetCurrentAnimatorStateInfo
    // was tried first and was too late: Mecanim can re-evaluate Any State
    // transitions within the same internal step it lands on Idle, before a
    // script's next Update() ever gets a chance to intervene.
    public class ClearBoolOnStateExit : StateMachineBehaviour
    {
        [SerializeField] private string boolParameterName = "GetUpFromBack";

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.SetBool(boolParameterName, false);
        }
    }
}
