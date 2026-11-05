using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A table ball in Hide and seek that can be made to roll. The hiding player's
    // ball and the decoys all go through this, with the same speed and the same
    // acceleration: how a ball moves must never tell who is driving it.
    public class HideAndSeekBall : MonoBehaviour
    {
        // Share of the speed a ball starts at. Below PoolBall's sleep threshold,
        // a push smaller than it would be wiped out every physics step.
        private const float StartShare = 0.2f;

        public PoolBall Ball { get; private set; }

        private Rigidbody rb;
        private float speed, acceleration;
        private Vector3 wish;
        private float wishUntil;

        public void Prepare(PoolBall ball, float speed, float acceleration)
        {
            Ball = ball;
            rb = ball.Rigidbody;
            this.speed = speed;
            this.acceleration = acceleration;
            Stop();
        }

        // wish: horizontal direction, its length the share of full speed.
        public void Drive(Vector3 wish, float duration = float.PositiveInfinity)
        {
            this.wish = wish;
            wishUntil = Time.time + duration;
        }

        public void Stop() => wish = Vector3.zero;

        public bool IsDriven => wish.sqrMagnitude > 0f && Time.time < wishUntil;

        // A knock from outside (the table shaking).
        public void Kick(Vector3 velocityChange)
        {
            if (!rb.isKinematic) Roll(velocityChange);
        }

        private void FixedUpdate()
        {
            if (rb.isKinematic || !IsDriven) return;
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;
            float step = acceleration * Time.fixedDeltaTime;
            float start = speed * StartShare;
            if (velocity.sqrMagnitude < start * start) step = Mathf.Max(step, start);
            Roll(Vector3.MoveTowards(velocity, wish * speed, step) - velocity);
        }

        // Spin with the speed, as a ball really rolling: pushed without, it would
        // slip and friction would eat most of the push.
        private void Roll(Vector3 velocityChange)
        {
            rb.linearVelocity += velocityChange;
            rb.angularVelocity += Vector3.Cross(Vector3.up, velocityChange) / Ball.Radius;
        }
    }
}
