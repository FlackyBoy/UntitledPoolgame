using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // Offline split-screen counterpart to FpsPlayerController — same movement
    // math (body yaw driven directly by mouse/stick, so strafe stays correct
    // by construction), but a plain MonoBehaviour instead of a NetworkBehaviour:
    // no Netcode involved at all for local split-screen play. Reads its actions
    // from this player's own PlayerInput instance (assigned/paired by
    // PlayerInputManager), not a shared asset reference.
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

        private CharacterController controller;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private float pitch;
        private float verticalVelocity;

        // 1 = normal. Multiplied into look sensitivity every frame — read by
        // whatever's currently active (e.g. LocalPoolPowerEffectReceiver
        // applying VisionImpairPower's debuff); this controller doesn't know
        // or care why it's been changed.
        public float SensitivityMultiplier { get; set; } = 1f;

        // Same idea as SensitivityMultiplier above — read by whatever's
        // currently active (InvertedControlsPower via
        // LocalPoolPowerEffectReceiver); this controller doesn't know or
        // care why it's been set.
        public bool InvertLook { get; set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();

            InputActionMap map = playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
            lookAction = map.FindAction(lookActionName, throwIfNotFound: true);

            // Unlike the online controller, both split-screen players' cameras
            // stay active (each renders its own half of the screen) — but only
            // one AudioListener may be enabled at a time, or Unity warns every
            // frame ("There are 2 audio listeners in the scene"), flooding the
            // console. Arbitrarily always player 0's.
            if (cameraPivot != null)
            {
                AudioListener audioListener = cameraPivot.GetComponentInChildren<AudioListener>(true);
                if (audioListener != null) audioListener.enabled = playerInput.playerIndex == 0;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Stay put during the pre-match mode-select screen — without
            // this, the player could already walk/look around (and even
            // pick up the cue) before anyone had actually clicked "Commencer
            // la partie". Same permissive-null convention as
            // CanShootNow()/CanPlayerShoot elsewhere: no PoolMatchRules at
            // all (a scene without a table) means nothing to wait for, not
            // "stay frozen forever".
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null && !rules.MatchStarted) return;

            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            if (InvertLook) look = -look;
            bool isGamepad = lookAction.activeControl?.device is Gamepad;

            // Mouse delta is already a per-frame quantity (how far it moved
            // since last frame), so it's used as-is. A gamepad stick reports a
            // continuous rate (how far it's pushed), which needs to be scaled
            // by deltaTime to become frame-rate-independent — without this,
            // look speed scales with FPS and feels wildly over-sensitive.
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

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;
            else
                verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
