using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Player
{
    // Minimal from-scratch FPS controller: movement is always relative to the
    // player body's own transform (transform.right / transform.forward), and
    // the body's yaw IS the mouse/gamepad look yaw — so there is no separate
    // "camera reference" to fall out of sync, and strafe is guaranteed correct
    // by construction. Only the owning client simulates and has an active
    // camera; other instances are driven purely by NetworkTransform.
    [RequireComponent(typeof(CharacterController))]
    public class FpsPlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -20f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float gamepadSensitivity = 150f; // now scaled by deltaTime, needs to be much larger than the old un-scaled value
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Input (whole asset — actions looked up by name)")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";

        private CharacterController controller;
        private Camera playerCamera;
        private AudioListener audioListener;
        private InputAction moveAction;
        private InputAction lookAction;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraPivot != null)
            {
                playerCamera = cameraPivot.GetComponentInChildren<Camera>(true);
                audioListener = cameraPivot.GetComponentInChildren<AudioListener>(true);
            }

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
            lookAction = map.FindAction(lookActionName, throwIfNotFound: true);
        }

        public override void OnNetworkSpawn()
        {
            enabled = IsOwner;

            if (playerCamera != null) playerCamera.gameObject.SetActive(IsOwner);
            if (audioListener != null) audioListener.enabled = IsOwner;

            // Hide the player's own visible body from their own camera — it's
            // still enabled for everyone else looking at this player. Excludes
            // LineRenderer/TrailRenderer, which are gameplay effects (e.g. the
            // pool aim trajectory preview), not the avatar mesh.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is LineRenderer || renderer is TrailRenderer) continue;
                renderer.enabled = !IsOwner;
            }

            if (!IsOwner) return;

            moveAction.Enable();
            lookAction.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            moveAction.Disable();
            lookAction.Disable();
        }

        private void Update()
        {
            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            bool isGamepad = lookAction.activeControl?.device is Gamepad;

            // Mouse delta is already a per-frame quantity (how far it moved
            // since last frame), so it's used as-is. A gamepad stick reports a
            // continuous rate (how far it's pushed), which needs to be scaled
            // by deltaTime to become frame-rate-independent — without this,
            // look speed scales with FPS and feels wildly over-sensitive.
            float sensitivity = isGamepad ? gamepadSensitivity * Time.deltaTime : mouseSensitivity;

            transform.Rotate(Vector3.up * look.x * sensitivity);

            pitch = Mathf.Clamp(pitch - look.y * sensitivity, minPitch, maxPitch);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 move = transform.right * input.x + transform.forward * input.y;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;
            else
                verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
