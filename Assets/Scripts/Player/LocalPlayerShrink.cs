using System.Collections.Generic;
using RootMotion.Dynamics;
using Unity.Cinemachine;
using UnityEngine;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Player
{
    // Shrinks a player (Pocket Size): the whole prefab is scaled from its
    // root, which carries the CharacterController, the camera pivot and the
    // animated model. What doesn't follow the transform scale is set by hand
    // and put back on Restore: the controller's skin and step (world units),
    // the cameras' near clip, and the movement (LocalFpsPlayerController.
    // SizeScale). The controller collides with the layers it's given (the
    // balls), and a blow throws it (Knock).
    //
    // The ragdoll is switched off while shrunk (PuppetMaster Disabled mode,
    // the animated model alone is shown): rescaled at runtime, its joints
    // kept their full-size anchors, the limbs ended up tens of centimetres
    // apart on a 14 cm body and spun all over (logs of 30/10), whatever was
    // tried (PuppetMaster's anchor update, setting the anchors again,
    // masses, solver passes). It comes back on at full size (Disabled to
    // Active resets the muscles onto the model).
    [DisallowMultipleComponent]
    public class LocalPlayerShrink : MonoBehaviour
    {
        private static readonly List<LocalPlayerShrink> shrunk = new List<LocalPlayerShrink>();

        public float Scale { get; private set; } = 1f;
        public bool IsShrunk => Scale < 0.999f;

        private CharacterController controller;
        private LocalFpsPlayerController fps;
        private LocalPlayerHandController hands;
        private PuppetMaster puppet;
        private bool handsWereEnabled;

        // Authored values, read on the first shrink.
        private bool captured;
        private Vector3 baseScale;
        private float baseSkin, baseStep, baseMinMove;
        private LayerMask baseInclude;
        private PuppetMaster.Mode basePuppetMode;
        private readonly List<(CinemachineCamera cam, float near)> cameras = new List<(CinemachineCamera, float)>();

        // Where the player stood before shrinking: Restore puts them back.
        private Vector3 homePosition;
        private Quaternion homeRotation;

        // A blow from outside (a ball), in m/s, fading out.
        private Vector3 knock;
        private float knockDamping = 4f;

        public static LocalPlayerShrink For(GameObject player)
        {
            if (!player.TryGetComponent(out LocalPlayerShrink shrink)) shrink = player.AddComponent<LocalPlayerShrink>();
            return shrink;
        }

        // Everyone back to full size where they stood (a new match, or the
        // mode ending).
        public static void RestoreAll()
        {
            for (int i = shrunk.Count - 1; i >= 0; i--)
                if (shrunk[i] != null) shrunk[i].Restore();
            shrunk.Clear();
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            fps = GetComponent<LocalFpsPlayerController>();
            hands = GetComponent<LocalPlayerHandController>();
            puppet = GetComponentInChildren<PuppetMaster>(true);
        }

        private void OnDestroy() => shrunk.Remove(this);

        private void Capture()
        {
            if (captured) return;
            captured = true;
            baseScale = transform.localScale;
            if (controller != null)
            {
                baseSkin = controller.skinWidth;
                baseStep = controller.stepOffset;
                baseMinMove = controller.minMoveDistance;
                baseInclude = controller.includeLayers;
            }
            if (puppet != null) basePuppetMode = puppet.mode;
            cameras.Clear();
            foreach (CinemachineCamera cam in GetComponentsInChildren<CinemachineCamera>(true))
                cameras.Add((cam, cam.Lens.NearClipPlane));
        }

        // scale < 1: shrunk to that size. collideWith: layers the shrunk
        // body bumps into on top of its own (the balls, which normal bodies
        // pass through: PhysicsLayerSetup). nearClip: the cameras' near
        // plane, in metres. jumpHeight: at normal size, for a player without
        // the R&D locomotion.
        public void Shrink(float scale, LayerMask collideWith, float nearClip, float jumpHeight)
        {
            Capture();
            if (!IsShrunk)
            {
                homePosition = transform.position;
                homeRotation = transform.rotation;
                // Nothing held at this size (the cue would dwarf the body),
                // and nothing picked up: the reach is in metres, the cues
                // lying around the table would still be within it. The powers
                // have their own component (LocalPoolPowerController).
                if (hands != null)
                {
                    hands.ForceDrop();
                    handsWereEnabled = hands.enabled;
                    hands.enabled = false;
                }
                // Before the scale changes (see the class comment).
                if (puppet != null) puppet.mode = PuppetMaster.Mode.Disabled;
            }

            Scale = scale;
            transform.localScale = baseScale * scale;
            if (controller != null)
            {
                controller.skinWidth = baseSkin * scale;
                controller.stepOffset = baseStep * scale;
                controller.minMoveDistance = 0f;
                controller.includeLayers = baseInclude | collideWith;
            }
            SetNearClip(nearClip);
            if (fps != null)
            {
                fps.SizeScale = scale;
                fps.SpeedMultiplier = 1f;
                fps.JumpHeight = jumpHeight;
            }
            knock = Vector3.zero;
            if (!shrunk.Contains(this)) shrunk.Add(this);
        }

        // Full size again, back where they stood before shrinking.
        public void Restore()
        {
            shrunk.Remove(this);
            if (!captured || !IsShrunk) return;
            Scale = 1f;
            transform.localScale = baseScale;
            if (controller != null)
            {
                controller.skinWidth = baseSkin;
                controller.stepOffset = baseStep;
                controller.minMoveDistance = baseMinMove;
                controller.includeLayers = baseInclude;
            }
            if (hands != null) hands.enabled = handsWereEnabled;
            foreach ((CinemachineCamera cam, float near) in cameras)
                if (cam != null) SetNear(cam, near);
            if (fps != null)
            {
                fps.SizeScale = 1f;
                fps.SpeedMultiplier = 1f;
                fps.JumpHeight = 0f;
            }
            knock = Vector3.zero;
            Teleport(homePosition, homeRotation);
            // Back at full size, where it was: the muscles are reset onto
            // the model as the ragdoll comes back on.
            if (puppet != null) puppet.mode = basePuppetMode;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            if (fps != null) fps.SpeedMultiplier = multiplier;
        }

        // The CharacterController fights a jump this big: off for the move.
        // An active puppet follows at once (PuppetMaster otherwise drags its
        // ragdoll across the room to the new spot).
        public void Teleport(Vector3 feet, Quaternion rotation)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            transform.SetPositionAndRotation(feet, rotation);
            if (controller != null) controller.enabled = wasEnabled;
            if (puppet != null && puppet.isActive) puppet.Teleport(feet, rotation, true);
            knock = Vector3.zero;
        }

        // Thrown by a blow (m/s), fading at damping per second.
        public void Knock(Vector3 velocity, float damping)
        {
            knock = velocity;
            knockDamping = damping;
        }

        // The controller's capsule in world space, for hit tests.
        public void GetCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            float s = transform.lossyScale.y;
            Vector3 center = transform.TransformPoint(controller.center);
            radius = controller.radius * s;
            float half = Mathf.Max(0f, controller.height * s * 0.5f - radius);
            bottom = center - Vector3.up * half;
            top = center + Vector3.up * half;
        }

        private void SetNearClip(float near)
        {
            foreach ((CinemachineCamera cam, float _) in cameras)
                if (cam != null) SetNear(cam, near);
        }

        private static void SetNear(CinemachineCamera cam, float near)
        {
            LensSettings lens = cam.Lens;
            lens.NearClipPlane = near;
            cam.Lens = lens;
        }

        private void Update()
        {
            if (knock.sqrMagnitude < 1e-8f || controller == null || !controller.enabled) return;
            controller.Move(knock * Time.deltaTime);
            knock *= Mathf.Exp(-knockDamping * Time.deltaTime);
        }
    }
}
