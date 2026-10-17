using UnityEngine;

namespace UntitledPoolGame.Player
{
    // Keeps the animated model (PuppetMaster's Animated Target, child of the
    // root) on its parent at its authored offset, with light smoothing, like
    // RootMotion's CharacterAnimationBase.SmoothFollow. Disabled by
    // LocalPlayerRagdollController during the ragdoll, then brought back
    // with ResyncNow.
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

        // Snaps to the rest offset and resets the smoothing. Call before
        // re-enabling, or it lerps in from the old position.
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
