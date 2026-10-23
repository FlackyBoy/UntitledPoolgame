using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Catch the Hole: brings a TableHole to life. Its size breathes (Perlin
    // noise between Min and Max Radius) and it wanders the felt: one target
    // after another, steered with a limited acceleration so it never turns
    // or starts sharply, sometimes pausing at a target. It stays clear of the
    // cushions, the pockets and the other living holes. CatchTheHole creates
    // and closes them.
    [RequireComponent(typeof(TableHole))]
    public class LivingHole : MonoBehaviour
    {
        private static readonly List<LivingHole> all = new List<LivingHole>();
        public static IReadOnlyList<LivingHole> All => all;

        public TableHole Hole { get; private set; }
        public CatchTheHoleSettings Settings { get; private set; }

        // For the powers (Gonfle, Turbo, Gel…): they scale what the hole does
        // without touching how it moves.
        public float SizeMultiplier { get; set; } = 1f;
        public float SpeedMultiplier { get; set; } = 1f;

        private CatchTheHole owner;
        // Outline radius the hole was generated with: Size = radius / this.
        private float baseRadius;
        private float noiseSeed;
        private float noiseTime;
        private float opened;
        private Vector3 velocity;
        private Vector3 target;
        private bool pauseAtTarget;
        private float pauseLeft;
        private float legTime;

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public void Init(CatchTheHole director, CatchTheHoleSettings settings, float generatedRadius)
        {
            owner = director;
            Settings = settings;
            Hole = GetComponent<TableHole>();
            Hole.Living = this;
            baseRadius = Mathf.Max(0.001f, generatedRadius);
            noiseSeed = Random.Range(0f, 1000f);
            opened = settings.openTime > 0f ? 0f : 1f;
            UpdateSize(0f);
            PickTarget();
        }

        private void Update()
        {
            if (Hole == null || Hole.IsClosing || Settings == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            UpdateSize(dt);
            UpdateMotion(dt);
        }

        // Radius the hole is heading for right now (before opening).
        public float CurrentRadius
        {
            get
            {
                float n = Settings.sizeChangeRate > 0f
                    ? Mathf.InverseLerp(0.25f, 0.75f, Mathf.PerlinNoise(noiseSeed, noiseTime))
                    : 0.5f;
                return Mathf.Lerp(Settings.minRadius, Mathf.Max(Settings.minRadius, Settings.maxRadius), n) * SizeMultiplier;
            }
        }

        private void UpdateSize(float dt)
        {
            noiseTime += dt * Settings.sizeChangeRate;
            if (opened < 1f) opened = Mathf.Min(1f, opened + dt / Mathf.Max(0.01f, Settings.openTime));
            // Eased so a new hole swells out instead of popping.
            float open = opened * opened * (3f - 2f * opened);
            Hole.Size = CurrentRadius * open / baseRadius;
        }

        private void UpdateMotion(float dt)
        {
            float speed = (owner != null ? owner.CruiseSpeed : Settings.baseSpeed) * SpeedMultiplier;
            // A faster hole needs more grip to keep its curves the same shape.
            float accel = Settings.acceleration * Mathf.Max(1f, speed / Mathf.Max(0.001f, Settings.baseSpeed));
            Vector3 position = transform.position;
            Vector3 desired = Vector3.zero;

            if (pauseLeft > 0f)
            {
                pauseLeft -= dt;
                if (pauseLeft <= 0f) PickTarget();
            }
            else
            {
                legTime += dt;
                Vector3 toTarget = target - position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (pauseAtTarget)
                {
                    // Slows into the stop.
                    if (distance < 0.01f) pauseLeft = Random.Range(Settings.pauseTime.x, Mathf.Max(Settings.pauseTime.x, Settings.pauseTime.y));
                    else desired = toTarget / distance * speed * Mathf.Clamp01(distance / Settings.arriveDistance);
                }
                else if (distance < Settings.arriveDistance)
                {
                    // Already on to the next one: the limited acceleration
                    // rounds the corner.
                    PickTarget();
                    toTarget = target - position;
                    toTarget.y = 0f;
                    desired = toTarget.normalized * speed;
                }
                else desired = toTarget / distance * speed;

                if (legTime > Settings.maxLegTime) PickTarget();
            }

            desired += Separation(speed);
            velocity = Vector3.MoveTowards(velocity, desired, accel * dt);

            // Kept off the cushions and pockets; the part of the velocity
            // going into them is lost so it slides along instead of sticking.
            Vector3 next = TablePointPower.ClampToTable(position + velocity * dt, Hole.MaxRadius + Settings.edgeClearance);
            velocity = (next - position) / dt;
            velocity.y = 0f;
            transform.position = next;
        }

        // Pushes away from living holes closer than Hole Gap, edge to edge.
        private Vector3 Separation(float speed)
        {
            Vector3 push = Vector3.zero;
            Vector3 position = transform.position;
            foreach (LivingHole other in all)
            {
                if (other == this || other.Hole == null || other.Hole.IsClosing) continue;
                Vector3 away = position - other.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                float min = Hole.MaxRadius + other.Hole.MaxRadius + Settings.holeGap;
                if (distance >= min) continue;
                Vector3 direction = distance > 1e-4f ? away / distance : Random.insideUnitSphere;
                direction.y = 0f;
                push += direction.normalized * Mathf.Max(speed, Settings.baseSpeed) * (1f - distance / min) * 2f;
            }
            return push;
        }

        // A spot of the felt, preferably far from the other holes and not
        // right next to where it is.
        private void PickTarget()
        {
            legTime = 0f;
            pauseAtTarget = Random.value < Settings.pauseChance;
            float margin = MaxReach(Settings);
            Vector3 best = transform.position;
            float bestScore = float.MinValue;
            for (int i = 0; i < 12; i++)
            {
                Vector3 candidate = RandomFeltPoint(margin);
                Vector3 offset = candidate - transform.position;
                offset.y = 0f;
                float score = Mathf.Min(offset.magnitude, 0.4f) + DistanceToOtherHoles(candidate, this);
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            target = best;
        }

        // Furthest the outline can reach at the largest size (the generated
        // shape has spikes beyond its radius), plus the clearance.
        public static float MaxReach(CatchTheHoleSettings settings) =>
            Mathf.Max(settings.minRadius, settings.maxRadius) * 1.4f + settings.edgeClearance;

        public static Vector3 RandomFeltPoint(float margin)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return Vector3.zero;
            float x = Mathf.Max(0f, surface.HalfLength - margin);
            float z = Mathf.Max(0f, surface.HalfWidth - margin);
            Vector3 local = new Vector3(Random.Range(-x, x), 0f, Random.Range(-z, z));
            return TablePointPower.ClampToTable(surface.transform.position + surface.transform.rotation * local, margin);
        }

        // Distance to the nearest other living hole (capped, so one far away
        // doesn't outweigh everything).
        public static float DistanceToOtherHoles(Vector3 point, LivingHole except)
        {
            float nearest = 1f;
            foreach (LivingHole other in all)
            {
                if (other == except) continue;
                Vector3 offset = point - other.transform.position;
                offset.y = 0f;
                nearest = Mathf.Min(nearest, offset.magnitude);
            }
            return nearest;
        }
    }
}
