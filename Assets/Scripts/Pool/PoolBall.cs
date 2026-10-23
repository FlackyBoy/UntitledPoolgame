using System;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // 8-ball groups from Number. Power balls carry their own component
    // (PowerBall), the rules don't need to know.
    public enum BallGroup
    {
        Cue,
        Solid,
        Eight,
        Stripe,
    }

    // Felt friction in two phases, coupling linear and angular velocity
    // through the contact point (needed for spin to do anything):
    //   1) slip: friction at the contact pulls them together, which turns
    //      leftover spin into motion (follow, draw);
    //   2) roll: a small constant deceleration brings the ball to a stop.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class PoolBall : MonoBehaviour
    {
        [SerializeField] private bool isCueBall;
        // 1-15 for object balls, ignored for the cue ball (isCueBall covers
        // that identity already). Used to compute Group for the rules system.
        [SerializeField] private int number;

        public bool IsCueBall => isCueBall;
        public int Number => number;
        public Rigidbody Rigidbody => rb;
        public float Radius => radius;

        public BallGroup Group
        {
            get
            {
                if (isCueBall) return BallGroup.Cue;
                if (number == 8) return BallGroup.Eight;
                return number <= 7 ? BallGroup.Solid : BallGroup.Stripe;
            }
        }

        // Every ball still on the table (a potted ball disables itself).
        private static readonly List<PoolBall> active = new List<PoolBall>();
        public static IReadOnlyList<PoolBall> Active => active;

        public static bool AnyMoving()
        {
            foreach (PoolBall ball in active)
            {
                Vector3 v = ball.rb.linearVelocity;
                if (new Vector3(v.x, 0f, v.z).magnitude > settings.sleepVelocityThreshold)
                    return true;
            }
            return false;
        }

        // Same threshold as AnyMoving, for one ball.
        public bool IsRolling
        {
            get
            {
                Vector3 v = rb.linearVelocity;
                return new Vector3(v.x, 0f, v.z).magnitude > settings.sleepVelocityThreshold;
            }
        }

        // The cue ball shared by both players. Speed Pool has one per player
        // (CueOwner): this returns the first, look them up through Active.
        public static PoolBall FindCueBall()
        {
            foreach (PoolBall ball in active)
                if (ball.isCueBall) return ball;
            return null;
        }

        // Speed Pool: the player this cue ball belongs to. -1 = shared (every
        // other mode).
        public int CueOwner { get; private set; } = -1;

        // Where the table put it (rack, head spot), whatever a mode did since.
        public Vector3 SpawnPosition => tableSpawnPosition;
        // Where it comes back after a scratch (a mode may have moved it).
        public Vector3 RespawnPosition => spawnPosition;

        // Speed Pool: this cue ball becomes the player's, tinted, starting
        // (and coming back after a scratch) at spawn.
        public void MakePlayerCueBall(int player, Vector3 spawn, Color tint)
        {
            CueOwner = player;
            spawnPosition = spawn;
            ResetToSpawn();
            Renderer meshRenderer = GetComponentInChildren<Renderer>();
            if (meshRenderer != null) meshRenderer.material.color = tint;
        }

        // Raised just before a potted ball goes away. The pocket is null when
        // it fell off the table.
        public static event Action<PoolBall, PoolPocket> Pocketed;

        // First ball the cue ball touches after ArmContactTracking (first
        // contact fouls). (cueBall, otherBall).
        public static event Action<PoolBall, PoolBall> CueBallFirstContact;

        private static PoolPhysicsSettings settings;

        // Solid sphere: I = 0.4 m r^2. Everything here is per unit mass.
        private const float SphereInertiaFactor = 0.4f;
        private const float SlipThreshold = 0.02f;

        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private float radius;
        private Vector3 spawnPosition;
        private Vector3 tableSpawnPosition;
        private Quaternion spawnRotation;
        private bool contactTrackingArmed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            radius = sphereCollider.radius * transform.lossyScale.x;
            spawnPosition = tableSpawnPosition = transform.position;
            spawnRotation = transform.rotation;

            if (settings == null)
                settings = PoolSettingsLoader.LoadOrDefault<PoolPhysicsSettings>("PoolPhysicsSettings");
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        // Speed Pool: the player whose shot last set this ball moving, through
        // their cue ball or a ball it hit (-1 = nobody yet). Tells who potted
        // a ball, for the HUD's message.
        public int LastShooter { get; private set; } = -1;
        private float lastShotTime = -1f;

        // Speed Pool: a copy added to its group (SpeedPoolSettings.extraBallsPerPlayer).
        public bool IsExtra { get; private set; }

        public void MarkExtra(Vector3 spawn)
        {
            IsExtra = true;
            spawnPosition = tableSpawnPosition = spawn;
            ResetToSpawn();
        }

        // Called when a shot is struck.
        public void ArmContactTracking()
        {
            contactTrackingArmed = true;
            if (CueOwner < 0) return;
            LastShooter = CueOwner;
            lastShotTime = Time.time;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.TryGetComponent(out PoolBall other)) return;

            // The more recent shot wins: a ball set moving by A's shot is A's.
            // Both balls get this call; whichever runs second sees no change.
            if (other.lastShotTime > lastShotTime)
            {
                LastShooter = other.LastShooter;
                lastShotTime = other.lastShotTime;
            }

            if (!isCueBall || !contactTrackingArmed) return;
            contactTrackingArmed = false;
            CueBallFirstContact?.Invoke(this, other);
        }

        // Ball in hand: kinematic and without collider while it's placed.
        public void BeginBallInHand()
        {
            // Already kinematic (second foul): Unity warns when setting its
            // velocity.
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            sphereCollider.enabled = false;
        }

        public void PlaceAt(Vector3 worldPosition)
        {
            rb.position = worldPosition;
            transform.position = worldPosition;
        }

        public void EndBallInHand()
        {
            sphereCollider.enabled = true;
            rb.isKinematic = false;
        }

        // Table hole: the ball sinks and hides but stays in play (enabled, in
        // Active) until PoolMatchRules brings it back.
        public bool InTableHole { get; private set; }
        public bool IsSinking => tableHoleRoutine != null;
        private Coroutine tableHoleRoutine;
        private Renderer[] renderers;

        public void FallIntoTableHole(Vector3 holeCenter, float sinkTime)
        {
            BeginBallInHand();
            InTableHole = true;
            if (tableHoleRoutine != null) StopCoroutine(tableHoleRoutine);
            tableHoleRoutine = StartCoroutine(SinkRoutine(holeCenter, Mathf.Max(0.01f, sinkTime)));
        }

        private System.Collections.IEnumerator SinkRoutine(Vector3 holeCenter, float time)
        {
            Vector3 from = transform.position;
            Vector3 over = new Vector3(holeCenter.x, from.y, holeCenter.z);
            Vector3 under = over + Vector3.down * radius * 3f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                // First to the centre, then down (speeding up).
                Vector3 p = k < 0.4f ? Vector3.Lerp(from, over, k / 0.4f) : Vector3.Lerp(over, under, Mathf.Pow((k - 0.4f) / 0.6f, 2f));
                PlaceAt(p);
                yield return null;
            }
            SetVisible(false);
            tableHoleRoutine = null;
        }

        public void ReturnFromTableHole(Vector3 position)
        {
            if (tableHoleRoutine != null) { StopCoroutine(tableHoleRoutine); tableHoleRoutine = null; }
            InTableHole = false;
            SetVisible(true);
            PlaceAt(position);
            EndBallInHand();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
                // Lights, lines and particles on the ball (power glow) keep theirs.
                if (r is MeshRenderer || r is SkinnedMeshRenderer) r.enabled = visible;
        }

        private void FixedUpdate()
        {
            // Parked: its height is driven by the placement code.
            if (rb.isKinematic) return;

            // Off the table after jumping a rail: treated as potted (for the
            // cue ball, a scratch). Real rules call it a foul for an object
            // ball; not modelled.
            if (transform.position.y < spawnPosition.y - settings.offTableDropThreshold)
            {
                OnPocketed();
                return;
            }

            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 contactOffset = Vector3.down * radius;
            Vector3 slipVelocity = horizontalVelocity + Vector3.Cross(rb.angularVelocity, contactOffset);
            slipVelocity.y = 0f;

            bool isSlipping = slipVelocity.magnitude > SlipThreshold;

            if (!isSlipping && horizontalVelocity.magnitude < settings.sleepVelocityThreshold)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                rb.angularVelocity = Vector3.zero;
                return;
            }

            if (isSlipping)
                ApplySlideFriction(contactOffset, slipVelocity);
            else
                ApplyRollingResistance();
        }

        // Slows the slip at the contact point, which changes the spin too.
        private void ApplySlideFriction(Vector3 contactOffset, Vector3 slipVelocity)
        {
            // With the coupling, a change dv in velocity reduces the slip by
            // 3.5 dv. Clamping against the raw slip overshot and oscillated.
            const float SlipResponseFactor = 1f + 1f / SphereInertiaFactor; // 3.5
            float maxDeltaSpeed = slipVelocity.magnitude / SlipResponseFactor;
            float deltaSpeed = Mathf.Min(settings.slideFriction * Time.fixedDeltaTime, maxDeltaSpeed);
            Vector3 deltaLinear = -slipVelocity.normalized * deltaSpeed;

            rb.linearVelocity += deltaLinear;
            rb.angularVelocity += Vector3.Cross(contactOffset, deltaLinear) / (SphereInertiaFactor * radius * radius);
        }

        // Rolling: slows translation and rotation together, keeping v = w x r
        // (the slide formula would keep reintroducing slip).
        private void ApplyRollingResistance()
        {
            Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (horizontal.sqrMagnitude < 0.0000001f) return;

            float speed = horizontal.magnitude;
            Vector3 dirHat = horizontal / speed;
            float deltaSpeed = Mathf.Min(settings.friction * Time.fixedDeltaTime, speed);

            rb.linearVelocity -= dirHat * deltaSpeed;

            Vector3 rollAxis = Vector3.Cross(Vector3.up, dirHat);
            rb.angularVelocity -= rollAxis * (deltaSpeed / radius);
        }

        public void OnPocketed(PoolPocket pocket = null)
        {
            Pocketed?.Invoke(this, pocket);

            if (isCueBall)
            {
                // The cue ball comes back in hand at its spawn spot.
                rb.position = spawnPosition;
                rb.rotation = spawnRotation;
                BeginBallInHand();
                return;
            }

            gameObject.SetActive(false);
        }

        // Back to the rack position, potted or not ("Rejouer").
        public void ResetToSpawn()
        {
            LastShooter = -1;
            lastShotTime = -1f;
            if (InTableHole) ReturnFromTableHole(spawnPosition);
            gameObject.SetActive(true);
            rb.isKinematic = false;
            sphereCollider.enabled = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = spawnPosition;
            rb.rotation = spawnRotation;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
    }
}
