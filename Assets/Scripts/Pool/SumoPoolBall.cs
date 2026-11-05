using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // A player's ball in Sumo Pool: rolled from the Move action relative to the
    // view, with a dash charged by holding the button, and a camera that orbits
    // it.
    //
    // The player's body stays alive, for its PlayerInput and its Camera, which
    // the split screen and the HUD use, but does nothing: its scripts are off,
    // its model hidden, and it waits where it stood. Off rather than swapped
    // for a small body as in Pocket Size: nothing of it is needed, and turned
    // back on it's where and how it was. The view goes through a Cinemachine
    // camera of its own, on the player's channel, above the body's cameras.
    public class SumoPoolBall : MonoBehaviour
    {
        private const int CameraPriority = 100;
        // Rolling at dash speed plus a blow: the table's 50 rad/s would clip
        // it, and the ball would slip.
        private const float MaxSpin = 150f;
        // How far a full charge takes the ball's colour towards white.
        private const float ChargeTint = 0.8f;

        // Set by the rules: in Play, and for the survivor until the next round.
        public bool Controllable { get; set; }
        public SumoPoolBall Opponent { get; set; }
        public PoolBall Ball { get; private set; }
        public bool IsBound => input != null;

        private SumoPoolSettings settings;
        private float scale;
        private Rigidbody rb;
        private float savedMaxSpin;
        private Vector3 tableCenter;
        private Renderer ballRenderer;
        private Color ballColor;

        private PlayerInput input;
        private InputAction moveAction, lookAction, dashAction, jumpAction;
        private readonly List<Behaviour> disabledScripts = new List<Behaviour>();
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private CharacterController bodyController;
        private bool bodyControllerWasOn;

        private GameObject cameraObject;
        private float yaw, pitch;
        private Vector3 focus;

        private Vector2 moveInput;
        private bool charging;
        private float chargeTime;
        // A dash waiting for the next physics step, its speed; 0 = none.
        private float queuedDash;
        private float nextDash;
        // Its velocity before this physics step's collisions (OnCollisionEnter).
        private Vector3 velocityBeforeContact;

        public void Prepare(PoolBall ball, SumoPoolSettings settings, float scale, Vector3 tableCenter)
        {
            Ball = ball;
            rb = ball.Rigidbody;
            this.settings = settings;
            this.scale = scale;
            this.tableCenter = tableCenter;
            savedMaxSpin = rb.maxAngularVelocity;
            rb.maxAngularVelocity = MaxSpin;
            ballRenderer = GetComponentInChildren<Renderer>();
            if (ballRenderer != null) ballColor = ballRenderer.material.color;
        }

        public void Bind(PlayerInput player)
        {
            input = player;
            InputActionMap map = player.actions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            lookAction = map.FindAction("Look", throwIfNotFound: true);
            dashAction = map.FindAction("Attack", throwIfNotFound: false);
            jumpAction = map.FindAction("Jump", throwIfNotFound: false);
            PutBodyAway(player.gameObject);
            CreateCamera(player.playerIndex);
            ResetView();
        }

        // The player's body back as it was.
        public void Release()
        {
            StopCharging();
            rb.maxAngularVelocity = savedMaxSpin;
            if (cameraObject != null) Destroy(cameraObject);
            foreach (Behaviour script in disabledScripts)
                if (script != null) script.enabled = true;
            foreach (Renderer r in hiddenRenderers)
                if (r != null) r.enabled = true;
            if (bodyController != null) bodyController.enabled = bodyControllerWasOn;
            disabledScripts.Clear();
            hiddenRenderers.Clear();
            input = null;
        }

        public void BackToStart()
        {
            StopCharging();
            Ball.ResetToSpawn();
            moveInput = Vector2.zero;
            queuedDash = 0f;
            nextDash = 0f;
            velocityBeforeContact = Vector3.zero;
            if (IsBound) ResetView();
        }

        private void PutBodyAway(GameObject body)
        {
            // A cue held would stay in the hidden hands.
            if (body.TryGetComponent(out LocalPlayerHandController hands)) hands.ForceDrop();
            foreach (MonoBehaviour script in body.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null || !script.enabled || !IsGameplayScript(script)) continue;
                script.enabled = false;
                disabledScripts.Add(script);
            }
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
            bodyController = body.GetComponent<CharacterController>();
            if (bodyController == null) return;
            bodyControllerWasOn = bodyController.enabled;
            bodyController.enabled = false;
        }

        // Ours, what reads the player's actions or moves the body. Not
        // PuppetMaster, Cinemachine and the like, and not Core (the devices).
        public static bool IsGameplayScript(MonoBehaviour script)
        {
            string space = script.GetType().Namespace;
            return space == "UntitledPoolGame.Player" || space == "UntitledPoolGame.Interaction" || space == "UntitledPoolGame.Pool";
        }

        private void CreateCamera(int player)
        {
            cameraObject = new GameObject($"Sumo Pool Camera J{player + 1}");
            CinemachineCamera cam = cameraObject.AddComponent<CinemachineCamera>();
            // The channel the body's Brain listens to (LocalFpsPlayerController).
            cam.OutputChannel = (OutputChannels)(1 << (player + 1));
            cam.Priority = CameraPriority;
            LensSettings lens = cam.Lens;
            lens.FieldOfView = settings.cameraFov;
            lens.NearClipPlane = settings.cameraNearClip;
            cam.Lens = lens;
        }

        // Behind the ball, towards the middle of the table.
        private void ResetView()
        {
            Vector3 look = tableCenter - Ball.RespawnPosition;
            yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            pitch = settings.cameraPitch;
            focus = Ball.RespawnPosition;
            PlaceCamera();
        }

        private void Update()
        {
            if (!IsBound) return;
            // Paused: a mouse's delta isn't scaled by the time, it would go on.
            if (Time.timeScale > 0f)
            {
                Vector2 look = lookAction.ReadValue<Vector2>();
                bool isGamepad = lookAction.activeControl?.device is Gamepad;
                float sensitivity = isGamepad ? settings.stickSensitivity * Time.deltaTime : settings.mouseSensitivity;
                yaw += look.x * sensitivity;
                pitch = Mathf.Clamp(pitch - look.y * sensitivity, settings.minCameraPitch, settings.maxCameraPitch);
            }
            bool canAct = Controllable && !rb.isKinematic;
            moveInput = canAct ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (canAct) UpdateCharge();
            else StopCharging();
        }

        // Pressed: starts charging. Released: goes, from the light dash at once
        // to the charged one at Dash Charge Time.
        private void UpdateCharge()
        {
            if (!charging)
            {
                if (Time.time < nextDash || !(Pressed(dashAction) || Pressed(jumpAction))) return;
                charging = true;
                chargeTime = 0f;
                return;
            }
            chargeTime += Time.deltaTime;
            float charge = settings.dashChargeTime > 0f ? Mathf.Clamp01(chargeTime / settings.dashChargeTime) : 1f;
            if (Held(dashAction) || Held(jumpAction))
            {
                SetTint(charge);
                return;
            }
            queuedDash = Mathf.Lerp(settings.dashSpeed, settings.chargedDashSpeed, charge) * scale;
            nextDash = Time.time + settings.dashCooldown;
            StopCharging();
            if (settings.debugLogs) Debug.Log($"[SumoPool] Dash de J{input.playerIndex + 1} : charge {charge:P0}, {queuedDash:0.00} m/s ajoutés à {rb.linearVelocity.magnitude:0.00} m/s.", this);
        }

        private void StopCharging()
        {
            if (!charging) return;
            charging = false;
            SetTint(0f);
        }

        private void SetTint(float charge)
        {
            if (ballRenderer != null) ballRenderer.material.color = Color.Lerp(ballColor, Color.white, charge * ChargeTint);
        }

        private static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();
        private static bool Held(InputAction action) => action != null && action.IsPressed();

        private void FixedUpdate()
        {
            if (!rb.isKinematic) Drive();
            velocityBeforeContact = rb.linearVelocity;
        }

        private void Drive()
        {
            Quaternion view = Quaternion.Euler(0f, yaw, 0f);
            Vector3 wish = view * new Vector3(moveInput.x, 0f, moveInput.y);
            float amount = Mathf.Min(wish.magnitude, 1f);
            bool pushed = amount > 0.01f;
            Vector3 heading = pushed ? wish.normalized : view * Vector3.forward;
            float maxSpeed = settings.maxSpeed * scale;
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;

            float speed = velocity.magnitude;

            if (pushed && speed <= maxSpeed)
            {
                // At rolling speed the ball goes where it's told: sideways speed
                // is taken off as much as forward speed is added, or turning
                // only added to the slide.
                float rate = settings.acceleration * scale;
                Roll(Vector3.MoveTowards(velocity, heading * (amount * maxSpeed), rate * Time.fixedDeltaTime) - velocity);
            }
            else
            {
                Roll(Vector3.MoveTowards(velocity, Vector3.zero, CoastRate(speed) * Time.fixedDeltaTime) - velocity);
                // Thrown by a blow or a dash: the same push as ever against it.
                if (pushed && Vector3.Dot(velocity, heading) < maxSpeed)
                    Roll(heading * (settings.acceleration * scale * amount * Time.fixedDeltaTime));
            }

            if (queuedDash <= 0f) return;
            Roll(heading * queuedDash);
            queuedDash = 0f;
        }

        // How fast a ball nobody pushes slows down, in one curve with no step in
        // it, since a step is felt as the ball stopping dead: light when thrown
        // fast (a push has to carry), strongest at rolling speed, then fading
        // with the speed so the end of a slide tails off. Min Braking finishes
        // it: proportional alone, it would never quite stop.
        private float CoastRate(float speed)
        {
            float maxSpeed = settings.maxSpeed * scale;
            float rate = speed <= maxSpeed
                ? settings.braking * Mathf.InverseLerp(0f, maxSpeed, speed)
                : Mathf.Lerp(settings.braking, settings.rollingFriction, Mathf.InverseLerp(maxSpeed, maxSpeed * 2f, speed));
            return Mathf.Max(settings.minBraking, rate) * scale;
        }

        // Spin with the speed, as a ball really rolling (PoolBall's rolling
        // phase): pushed without, it would slip and friction would eat most of
        // the push.
        private void Roll(Vector3 velocityChange)
        {
            rb.linearVelocity += velocityChange;
            rb.angularVelocity += Vector3.Cross(Vector3.up, velocityChange) / Ball.Radius;
        }

        // The bounce already gives most of the other's speed (a pool collision,
        // nearly elastic). This is on top, for each ball from what the other
        // brought: both get here, and nobody has to work out who hit whom.
        private void OnCollisionEnter(Collision collision)
        {
            if (Opponent == null || collision.rigidbody != Opponent.rb || rb.isKinematic) return;
            Vector3 away = transform.position - Opponent.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-8f) return;
            away.Normalize();
            float brought = Mathf.Max(0f, Vector3.Dot(Opponent.velocityBeforeContact, away));
            Roll(away * (brought * settings.bumpBoost));
        }

        private void LateUpdate()
        {
            if (cameraObject == null) return;
            // Stays above the felt as the ball falls: we watch it go in.
            Vector3 target = transform.position;
            target.y = Mathf.Max(target.y, tableCenter.y + Ball.Radius);
            focus = Vector3.Lerp(focus, target, 1f - Mathf.Exp(-settings.cameraFollow * Time.deltaTime));
            PlaceCamera();
        }

        private void PlaceCamera()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            cameraObject.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * (settings.cameraDistance * scale), rotation);
        }
    }
}
