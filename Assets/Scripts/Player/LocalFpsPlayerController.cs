using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // Minimal FPS controller. The body's yaw is the look yaw and movement is
    // relative to it, so strafing can't get out of sync with the camera.
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class LocalFpsPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -20f;

        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float gamepadSensitivity = 150f; // now scaled by deltaTime, needs to be much larger than the old un-scaled value
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";

        private CharacterController controller;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private float pitch;
        private float verticalVelocity;

        // Multiplies look sensitivity (powers set it, 1 = normal).
        public float SensitivityMultiplier { get; set; } = 1f;

        // Set by Inverted Controls.
        public bool InvertLook { get; set; }

        // Degrees, positive = looking down (camera pivot's local X).
        public float Pitch => pitch;

        public Transform CameraPivot => cameraPivot;

        // Pocket Size (LocalPlayerShrink): the body is this size, so speeds,
        // jump and gravity are scaled to match (1 = normal).
        public float SizeScale { get; set; } = 1f;
        // Speed powers on top of the size (1 = normal).
        public float SpeedMultiplier { get; set; } = 1f;
        public float MoveScale => SizeScale * SpeedMultiplier;
        // Jump height at normal size, in metres. 0 = no jump: this simple
        // controller only jumps when a mode asks (the R&D locomotion has its own).
        public float JumpHeight { get; set; }

        // R&D movement (momentum, jump, dive, ledges): when present and
        // enabled it moves the body, and this controller only handles the look.
        private LocalPlayerLocomotion locomotion;

        // Turns the view from code (LocalCueMelee swings it back toward the
        // target on a strike), on top of the player's own look input.
        public void AddLook(float yawDegrees, float pitchDegrees)
        {
            transform.Rotate(Vector3.up * yawDegrees);
            pitch = Mathf.Clamp(pitch + pitchDegrees, minPitch, maxPitch);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();
            locomotion = GetComponent<LocalPlayerLocomotion>();

            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
            lookAction = map.FindAction(lookActionName, throwIfNotFound: true);
            jumpAction = map.FindAction(jumpActionName, throwIfNotFound: false);

            // Only one AudioListener at a time (player 0's), or Unity warns
            // every frame.
            if (cameraPivot != null)
            {
                AudioListener audioListener = cameraPivot.GetComponentInChildren<AudioListener>(true);
                if (audioListener != null) audioListener.enabled = playerInput.playerIndex == 0;
            }

            // Each Brain only follows the cameras of its own player: one
            // Cinemachine channel per player index (with the default mask every
            // screen followed the same camera).
            OutputChannels channel = (OutputChannels)(1 << (playerInput.playerIndex + 1));
            CinemachineBrain brain = GetComponentInChildren<CinemachineBrain>(true);
            if (brain != null) brain.ChannelMask = channel;
            foreach (CinemachineVirtualCameraBase vcam in GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
                vcam.OutputChannel = channel;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Frozen while the mode-select screen is up. No PoolMatchRules (no
            // table) means nothing to wait for.
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null && !rules.MatchStarted) return;

            HandleLook();
            if (locomotion == null || !locomotion.isActiveAndEnabled) HandleMove();
        }

        private void HandleLook()
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            if (InvertLook) look = -look;
            bool isGamepad = lookAction.activeControl?.device is Gamepad;

            // Mouse delta is already per frame; a stick is a rate, scaled by
            // deltaTime.
            float sensitivity = (isGamepad ? gamepadSensitivity * Time.deltaTime : mouseSensitivity) * SensitivityMultiplier;

            transform.Rotate(Vector3.up * look.x * sensitivity);

            pitch = Mathf.Clamp(pitch - look.y * sensitivity, minPitch, maxPitch);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 move = transform.right * input.x + transform.forward * input.y;

            float size = SizeScale;
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f * size;
            else
                verticalVelocity += gravity * size * Time.deltaTime;
            if (JumpHeight > 0f && controller.isGrounded && jumpAction != null && jumpAction.WasPressedThisFrame())
                verticalVelocity = Mathf.Sqrt(2f * JumpHeight * size * -gravity * size);

            Vector3 velocity = move * (moveSpeed * MoveScale) + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
