using UnityEngine;

namespace UntitledPoolGame.Player
{
    // Keeps this Transform (the animated model — PuppetMaster's Animated
    // Target, a child of the player root that owns the CharacterController)
    // locked onto its parent at a fixed authored local offset, every frame,
    // with light smoothing to absorb small physics jitter. Same idiom as
    // RootMotion's own demo CharacterAnimationBase.SmoothFollow, which
    // exists for exactly this parent(movement)/child(Animator) split —
    // confirmed by reading that script directly, not guessed.
    //
    // LocalPlayerRagdollController disables this while the ragdoll is
    // unpinned/getting up, so PuppetMaster can freely reposition this
    // Transform on its own natively — then re-enables it via ResyncNow()
    // (not just .enabled = true) once it has moved the parent to match
    // wherever the ragdoll actually landed, so the next LateUpdate doesn't
    // lerp back in from a stale pre-ragdoll position.
    public class LocalCharacterModelFollow : MonoBehaviour
    {
        [SerializeField] private float followSpeed = 20f;

        private Vector3 localRestPosition;
        private Quaternion localRestRotation;
        private Vector3 lastPosition;
        private Quaternion lastRotation;

        public Vector3 LocalRestPosition => localRestPosition;
        public Quaternion LocalRestRotation => localRestRotation;

        private void Awake()
        {
            localRestPosition = transform.localPosition;
            localRestRotation = transform.localRotation;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }

        // Snaps straight to the authored rest offset and resets the
        // smoothing cache to match — call this right before re-enabling
        // after a period where something else (PuppetMaster) was driving
        // this Transform directly, so LateUpdate resumes smoothing from
        // the current pose instead of lerping in from wherever it was
        // before this component got disabled.
        public void ResyncNow()
        {
            transform.localPosition = localRestPosition;
            transform.localRotation = localRestRotation;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            if (transform.parent == null) return;

            Vector3 targetPosition = transform.parent.TransformPoint(localRestPosition);
            Quaternion targetRotation = transform.parent.rotation * localRestRotation;

            transform.SetPositionAndRotation(
                Vector3.Lerp(lastPosition, targetPosition, Time.deltaTime * followSpeed),
                Quaternion.Slerp(lastRotation, targetRotation, Time.deltaTime * followSpeed));

            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }
    }
}
