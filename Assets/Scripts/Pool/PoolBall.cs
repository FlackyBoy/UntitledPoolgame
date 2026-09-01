using System;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Standard 8-ball ball groups, computed from Number. Kept deliberately
    // minimal (no "Special" case) — a future special/power ball is identified
    // by its own marker component (same pattern as Cue.cs), not by extending
    // this enum, so the base rules logic never needs to know about powers.
    public enum BallGroup
    {
        Cue,
        Solid,
        Eight,
        Stripe,
    }

    // Two-phase felt friction, physically coupled between linear and angular
    // velocity via the ball/table contact point — needed for spin (topspin,
    // backspin, english) to actually do anything:
    //   1) Slip phase — linear and angular velocity don't yet match "rolling
    //      without slipping". Friction at the contact point pulls them toward
    //      each other. This is what converts a cue strike's leftover spin back
    //      into motion after a stun (follow/draw) — without this coupling, spin
    //      has no way to ever turn into movement.
    //   2) Roll phase — once slip is gone, a smaller constant deceleration
    //      (real rolling resistance) brings the ball to a smooth, bounded stop.
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

        // Live registry of every enabled ball, so other systems (rules, future
        // powers) can query "what's still on the table" without a manual
        // reference to every ball. A pocketed ball disables itself, which
        // removes it here automatically via OnDisable.
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

        // There's only ever one cue ball on the table — this exact
        // "loop Active looking for IsCueBall" pattern used to be copy-pasted
        // in PoolMatchRules.RegisterFoul and both aim controllers' ball-in-hand
        // placement code.
        public static PoolBall FindCueBall()
        {
            foreach (PoolBall ball in active)
                if (ball.isCueBall) return ball;
            return null;
        }

        // Raised right before a pocketed ball deactivates/respawns — subscribe
        // here for scoring, turn rules, or (later) special-ball powers, instead
        // of modifying this class directly.
        public static event Action<PoolBall> Pocketed;

        // Raised once, the first time the cue ball touches another ball after
        // ArmContactTracking() was called for the current shot — the "first
        // contact" rule fouls (wrong ball hit first, or no contact at all) key
        // off this. (cueBall, otherBall).
        public static event Action<PoolBall, PoolBall> CueBallFirstContact;

        private static PoolPhysicsSettings settings;

        // Moment of inertia of a solid sphere is I = 0.4 * m * r^2; working in
        // per-unit-mass terms (impulse/mass = velocity change) throughout, so
        // this factor alone (without mass) relates a linear velocity change at
        // the contact point to the resulting angular one.
        private const float SphereInertiaFactor = 0.4f;
        private const float SlipThreshold = 0.02f;

        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private float radius;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool contactTrackingArmed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            radius = sphereCollider.radius * transform.lossyScale.x;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            if (settings == null)
            {
                settings = Resources.Load<PoolPhysicsSettings>("PoolPhysicsSettings");
                if (settings == null)
                {
                    Debug.LogWarning("PoolPhysicsSettings asset not found in Assets/Resources — using fallback defaults. Run Tools > Pool > Ensure Config Assets Exist to create it.");
                    settings = ScriptableObject.CreateInstance<PoolPhysicsSettings>();
                }
            }
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        // Called by the aim controllers right when a shot is struck — arms
        // first-contact tracking for that shot so CueBallFirstContact fires on
        // whatever the cue ball touches next (or never, if it misses everything).
        public void ArmContactTracking() => contactTrackingArmed = true;

        private void OnCollisionEnter(Collision collision)
        {
            if (!isCueBall || !contactTrackingArmed) return;
            if (!collision.gameObject.TryGetComponent(out PoolBall other)) return;

            contactTrackingArmed = false;
            CueBallFirstContact?.Invoke(this, other);
        }

        // Ball-in-hand: parks the ball in a kinematic, non-colliding "picked
        // up" state (wherever it currently sits) while the fouled-against
        // player chooses where to put it back down — see PoolMatchRules.
        // BallInHand and the aim controllers' placement mode.
        public void BeginBallInHand()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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

        private void FixedUpdate()
        {
            // Kinematic while parked for ball-in-hand — its Y is being driven
            // explicitly by the placement code, not falling, so the off-table
            // check below has nothing meaningful to compare against.
            if (rb.isKinematic) return;

            // A ball that jumps a rail on a hard shot and lands off the table
            // would otherwise just fall forever (or through the floor) with
            // nothing ever putting it back into play. Treated the same as
            // being pocketed — for the cue ball that's exactly a scratch
            // (reposition + ball-in-hand); for any other ball it's removed
            // like a normal pot. Real rules would call an off-table object
            // ball a foul rather than a legal pot, but that distinction isn't
            // modeled here — a simplification worth revisiting alongside the
            // rest of the casual foul rules if it turns out to matter.
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

        // Slide/grip friction: decelerates the SLIP velocity at the contact
        // point, coupling linear and angular velocity together via the impulse's
        // torque (I = 0.4*m*r^2 for a solid sphere). This is what converts a cue
        // strike's leftover spin into renewed motion after a stun.
        private void ApplySlideFriction(Vector3 contactOffset, Vector3 slipVelocity)
        {
            // Because of the linear/angular coupling below, decelerating the slip
            // velocity by Δv actually reduces the slip itself by (1 + 1/0.4)·Δv =
            // 3.5·Δv (a known factor for a solid sphere) — not just Δv. Clamping
            // against the raw slip magnitude (ignoring that factor) overshoots
            // past zero and out the other side, which is exactly what produced
            // the sawtooth speed/slip oscillation that never settled.
            const float SlipResponseFactor = 1f + 1f / SphereInertiaFactor; // 3.5
            float maxDeltaSpeed = slipVelocity.magnitude / SlipResponseFactor;
            float deltaSpeed = Mathf.Min(settings.slideFriction * Time.fixedDeltaTime, maxDeltaSpeed);
            Vector3 deltaLinear = -slipVelocity.normalized * deltaSpeed;

            rb.linearVelocity += deltaLinear;
            rb.angularVelocity += Vector3.Cross(contactOffset, deltaLinear) / (SphereInertiaFactor * radius * radius);
        }

        // Rolling resistance: once the ball is genuinely rolling without slip,
        // slow translation AND rotation down TOGETHER, keeping v = ω × r intact
        // (unlike slide friction's contact-torque formula, which would overcorrect
        // spin here and keep reintroducing slip instead of settling).
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

        public void OnPocketed()
        {
            Pocketed?.Invoke(this);

            if (isCueBall)
            {
                // The cue ball respawns instead of disappearing — there's only
                // ever one, and the player needs it back to keep shooting. A
                // scratch is always a foul, so it goes straight into ball-in-hand
                // parked at its spawn spot, ready for the fouled-against player
                // to move it wherever they actually want before their shot.
                rb.position = spawnPosition;
                rb.rotation = spawnRotation;
                BeginBallInHand();
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
