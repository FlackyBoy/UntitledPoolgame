using Unity.Cinemachine;
using UnityEngine;

namespace UntitledPoolGame.Player
{
    // Keeps the FPS view on the player's side of walls and ceilings. The eyes
    // are ahead of and above the capsule (31 cm ahead, 10 cm above at full
    // size), so the capsule stopping at a wall doesn't stop them. Goes through
    // the pipeline rather than moving CameraPivot, which other scripts aim from.
    // Added by LocalFpsPlayerController. Idle while Cinemachine is off (aiming)
    // and during a fall (LocalPlayerRagdollController holds its own view).
    public class LocalFpsCameraCollision : CinemachineExtension
    {
        // Share of the way back to the eyes per second, once nothing is in the
        // way: pulled in at once, back out smoothly, so a thin post doesn't
        // make the view jump.
        private const float RecoverPerSecond = 4f;

        private CharacterController body;
        private LocalFpsPlayerController fps;
        private LocalPlayerRagdollController ragdoll;
        private float heightKept = 1f;
        private float reachKept = 1f;

        protected override void Awake()
        {
            base.Awake();
            body = GetComponentInParent<CharacterController>();
            fps = GetComponentInParent<LocalFpsPlayerController>();
            ragdoll = GetComponentInParent<LocalPlayerRagdollController>();
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || body == null) return;
            float radius = MovementSettings.Instance.cameraCollisionRadius * (fps != null ? fps.SizeScale : 1f);
            if (radius <= 0f || (ragdoll != null && ragdoll.IsCameraDetached))
            {
                heightKept = reachKept = 1f;
                return;
            }

            Transform root = body.transform;
            float scale = root.lossyScale.y;
            Vector3 desired = state.GetCorrectedPosition();
            // The centre of the capsule's top sphere: always inside the player's space.
            Vector3 head = root.TransformPoint(body.center) + Vector3.up * Mathf.Max(0f, (body.height * 0.5f - body.radius) * scale);

            // Up to eye level, then out to the eyes: one diagonal cast would
            // also lower the view whenever a wall shortens it.
            Vector3 eyes = head;
            float rise = desired.y - head.y;
            if (rise > 0f)
            {
                heightKept = Follow(heightKept, CameraObstacles.FreeDistance(head, Vector3.up, rise, radius, root) / rise, deltaTime);
                eyes.y += rise * heightKept;
            }
            else
            {
                eyes.y = desired.y;
            }

            Vector3 flat = desired - head;
            flat.y = 0f;
            float length = flat.magnitude;
            if (length > 0.001f)
            {
                float free = CameraObstacles.FreeDistance(eyes, flat / length, length, radius, root);
                reachKept = Follow(reachKept, free / length, deltaTime);
                eyes += flat * reachKept;
            }
            state.PositionCorrection += eyes - desired;
        }

        private static float Follow(float current, float target, float deltaTime)
        {
            if (target <= current || deltaTime < 0f) return target;
            return Mathf.MoveTowards(current, target, RecoverPerSecond * deltaTime);
        }
    }
}
