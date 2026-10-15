using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UntitledPoolGame.Pool
{
    // A hole smashed in the felt by TableHolePower's meteor. The felt itself
    // stays solid: the hole is a dark, torn shape drawn just above it — a
    // new random outline for each impact (lumpy edge, a few longer cracks)
    // — with a scorched rim (a decal projected on the felt, turned and
    // stretched at random, or a plain outline without one) and smoke. A ball
    // whose centre gets inside the outline is handed to PoolMatchRules
    // (HandleBallFellInTableHole), which sinks it and brings it back at the
    // end of the shot. Opened and closed by PoolMatchRules (AddTableHole,
    // and the hole's Duration).
    public class TableHole : MonoBehaviour
    {
        private static readonly List<TableHole> active = new List<TableHole>();
        public static IReadOnlyList<TableHole> Active => active;

        // The outline: its distance from the centre for each of Segments
        // evenly spaced directions (world X/Z, the hole is never rotated).
        private const int Segments = 96;
        private readonly float[] edge = new float[Segments];
        private float maxRadius;

        private TableHolePower power;
        private Transform visual;
        private LineRenderer rim;
        private DecalProjector scorch;
        private GameObject smoke;
        private bool closing;

        public TableHolePower Power => power;
        public float MaxRadius => maxRadius;

        public static TableHole Create(TableHolePower power, Vector3 center)
        {
            var go = new GameObject("TableHole");
            go.transform.position = center;
            TableHole hole = go.AddComponent<TableHole>();
            hole.Build(power);
            return hole;
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private static Material shapeMaterial;

        private void Build(TableHolePower p)
        {
            power = p;
            float radius = p.HoleRadius;

            if (p.HolePrefab != null)
            {
                // A custom model keeps its own look: a round outline of Hole
                // Radius for the falls, the model turned at random.
                for (int i = 0; i < Segments; i++) edge[i] = radius;
                maxRadius = radius;
                GameObject model = Instantiate(p.HolePrefab, transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                model.transform.localScale = p.HolePrefab.transform.localScale * radius * 2f * p.HolePrefabScale;
                visual = model.transform;
            }
            else
            {
                GenerateOutline(p);
                visual = CreateShape(p);
            }

            if (p.ScorchDecal != null)
            {
                // Projected straight down onto the felt, through a thin slab
                // centred on it: the balls rolling by stay unmarked (and the
                // angle fade spares their sides). Turned and stretched at
                // random so no two scorches look alike.
                var scorchObject = new GameObject("HoleScorch");
                scorchObject.transform.SetParent(transform, false);
                scorchObject.transform.localPosition = Vector3.up * (p.ScorchDepth * 0.5f);
                float turn = Random.Range(0f, Mathf.PI * 2f);
                scorchObject.transform.rotation = Quaternion.LookRotation(Vector3.down, new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)));
                scorch = scorchObject.AddComponent<DecalProjector>();
                scorch.material = p.ScorchDecal;
                float size = radius * 2f * p.ScorchScale;
                float stretch = Random.Range(-p.ScorchStretch, p.ScorchStretch);
                scorch.size = new Vector3(size * (1f + stretch), size * (1f - stretch), p.ScorchDepth);
                scorch.pivot = new Vector3(0f, 0f, p.ScorchDepth * 0.5f);
                scorch.fadeFactor = p.ScorchOpacity;
                scorch.startAngleFade = 30f;
                scorch.endAngleFade = 60f;
            }
            else if (p.RimColor.a > 0f && p.RimWidth > 0f)
            {
                rim = RingLine.Create(transform, "HoleRim", p.RimWidth, p.RimColor);
                SetRim(1f);
            }

            if (p.SmokePrefab != null) StartSmoke(p);
        }

        // A lumpy circle (smooth noise all the way round, so it closes up
        // seamlessly) with a few sharp cracks reaching further out.
        private void GenerateOutline(TableHolePower p)
        {
            float radius = p.HoleRadius;
            float seedX = Random.Range(0f, 1000f), seedY = Random.Range(0f, 1000f);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                float broad = Mathf.PerlinNoise(seedX + cos * 1.6f, seedY + sin * 1.6f) - 0.5f;
                float fine = Mathf.PerlinNoise(seedX * 2f + cos * 4f, seedY * 2f + sin * 4f) - 0.5f;
                float jag = Mathf.PerlinNoise(seedX * 3f + cos * 11f, seedY * 3f + sin * 11f) - 0.5f;
                edge[i] = radius * (1f + p.ShapeRoughness * (broad * 1.2f + fine * 0.6f + jag * 0.35f));
            }

            for (int s = 0; s < p.ShapeSpikes; s++)
            {
                float spikeAngle = Random.Range(0f, 360f);
                float width = Random.Range(7f, 15f);   // degrees each side
                float length = radius * p.ShapeSpikeLength * Random.Range(0.5f, 1f);
                for (int i = 0; i < Segments; i++)
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(i * 360f / Segments, spikeAngle));
                    if (d < width) edge[i] += length * Mathf.Pow(1f - d / width, 2f);
                }
            }

            maxRadius = 0f;
            for (int i = 0; i < Segments; i++)
            {
                edge[i] = Mathf.Max(edge[i], radius * 0.5f);
                maxRadius = Mathf.Max(maxRadius, edge[i]);
            }
        }

        // The outline as a flat mesh just above the felt, no collider (a ball
        // sinking below the felt looks like it drops into it), with a soft
        // edge in its vertex colours, so it melts into the scorch around
        // instead of sitting on it like a cut-out:
        //   centre → inner ring: solid hole colour;
        //   inner ring → outline: still opaque, embers glowing at the outline
        //     (hotter or cooler at random along it, Ember Amount);
        //   outline → outer ring: fading out to nothing (Edge Softness).
        // The falls follow the outline itself.
        private Transform CreateShape(TableHolePower p)
        {
            float softness = p.EdgeSoftness;
            const int rings = 3;   // inner, outline, outer
            var vertices = new Vector3[1 + Segments * rings];
            var colors = new Color[vertices.Length];
            var triangles = new List<int>(Segments * 3 * 5);
            Color hole = p.HoleColor;
            Color charred = new Color(0.06f, 0.035f, 0.02f, 0f);
            float emberSeed = Random.Range(0f, 1000f);

            vertices[0] = Vector3.zero;
            colors[0] = hole;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float reach = edge[i];
                // Embers in patches: smooth noise round the outline, cut low.
                float heat = Mathf.Clamp01(Mathf.PerlinNoise(emberSeed + direction.x * 3f, emberSeed + direction.z * 3f) * 1.8f - 0.55f) * p.EmberAmount;

                vertices[1 + i] = direction * reach * Mathf.Max(0f, 1f - softness * 0.5f);
                colors[1 + i] = hole;
                vertices[1 + Segments + i] = direction * reach;
                colors[1 + Segments + i] = Color.Lerp(hole, p.EmberColor, heat);
                vertices[1 + Segments * 2 + i] = direction * reach * (1f + softness);
                colors[1 + Segments * 2 + i] = charred;
            }

            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                // Clockwise seen from above: facing up.
                triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + i);
                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int a = 1 + ring * Segments + i, b = 1 + ring * Segments + next;
                    int c = a + Segments, d = b + Segments;
                    triangles.Add(a); triangles.Add(b); triangles.Add(d);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
            }

            var mesh = new Mesh { name = "HoleShape", vertices = vertices, colors = colors };
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var shape = new GameObject("HoleShape");
            shape.transform.SetParent(transform, false);
            shape.transform.localPosition = Vector3.up * 0.0015f;
            shape.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = shape.AddComponent<MeshRenderer>();
            // Drawn with the opaque geometry (render queue 2450, "AlphaTest"),
            // not as the transparent it would be by default: transparents are
            // sorted by distance, and the hole then came out on top of the
            // smoke rising from it. Now every effect is drawn after it.
            if (shapeMaterial == null) shapeMaterial = new Material(Shader.Find("Sprites/Default")) { renderQueue = 2450 };
            r.sharedMaterial = shapeMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            // Sprites/Default multiplies the vertex colours by its tint (white).
            return shape.transform;
        }

        private void SetRim(float scale)
        {
            Vector3 center = transform.position + Vector3.up * 0.002f;
            rim.positionCount = Segments;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                rim.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * edge[i] * scale);
            }
        }

        // The prefab's smoke with its rate scaled (Smoke Rate), dying out
        // after Smoke Duration if one is set.
        private void StartSmoke(TableHolePower p)
        {
            smoke = Instantiate(p.SmokePrefab, transform.position, p.SmokePrefab.transform.rotation, transform);
            smoke.transform.localScale = p.SmokePrefab.transform.localScale * p.SmokeScale;
            foreach (ParticleSystem system in smoke.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTimeMultiplier *= p.SmokeRate;
                emission.rateOverDistanceMultiplier *= p.SmokeRate;
            }
            if (p.SmokeDuration > 0f) StartCoroutine(StopSmokeAfter(p.SmokeDuration));
        }

        private IEnumerator StopSmokeAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            StopSmoke();
        }

        private void StopSmoke()
        {
            if (smoke == null) return;
            smoke.transform.SetParent(null, true);
            foreach (ParticleSystem system in smoke.GetComponentsInChildren<ParticleSystem>())
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(smoke, 3f);
            smoke = null;
        }

        // How far the outline reaches in that (horizontal) direction.
        public float RadiusToward(Vector3 direction)
        {
            float angle = Mathf.Atan2(direction.z, direction.x);
            if (angle < 0f) angle += Mathf.PI * 2f;
            float f = angle / (Mathf.PI * 2f) * Segments;
            int i = Mathf.FloorToInt(f) % Segments;
            return Mathf.Lerp(edge[i], edge[(i + 1) % Segments], f - Mathf.Floor(f));
        }

        // Whether a horizontal offset from the centre lies inside the outline
        // (extended by margin).
        private bool Contains(Vector3 offset, float margin)
        {
            offset.y = 0f;
            float limit = maxRadius + margin;
            float sqr = offset.sqrMagnitude;
            if (sqr >= limit * limit) return false;
            float reach = RadiusToward(offset) + margin;
            return sqr < reach * reach;
        }

        // Ball centre inside the outline, rolling (not parked or in hand).
        private void FixedUpdate()
        {
            if (closing) return;
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null) return;
            Vector3 center = transform.position;
            for (int i = PoolBall.Active.Count - 1; i >= 0; i--)
            {
                PoolBall ball = PoolBall.Active[i];
                if (ball.InTableHole || ball.Rigidbody.isKinematic) continue;
                if (Contains(ball.transform.position - center, 0f)) rules.HandleBallFellInTableHole(ball, this);
            }
        }

        // Whether a ball of that radius placed at point would overlap a hole
        // (ball in hand, a ball coming back after a fall).
        public static bool Overlaps(Vector3 point, float ballRadius)
        {
            foreach (TableHole hole in active)
                if (!hole.closing && hole.Contains(point - hole.transform.position, ballRadius)) return true;
            return false;
        }

        // Same as PoolPocket.AvoidAllPockets: pushes a placement out of every hole.
        public static Vector3 AvoidAll(Vector3 position, float clearance)
        {
            foreach (TableHole hole in active)
            {
                if (hole.closing) continue;
                Vector3 offset = position - hole.transform.position;
                offset.y = 0f;
                if (!hole.Contains(offset, clearance)) continue;
                float distance = offset.magnitude;
                Vector3 direction = distance > 1e-4f ? offset / distance : Vector3.forward;
                Vector3 pushed = hole.transform.position + direction * (hole.RadiusToward(direction) + clearance);
                position = new Vector3(pushed.x, position.y, pushed.z);
            }
            return position;
        }

        // Shrinks away (smoke left to finish), or at once when instant.
        public void Close(bool instant)
        {
            if (closing) return;
            closing = true;
            StopSmoke();
            if (instant || power == null || power.CloseTime <= 0f) Destroy(gameObject);
            else StartCoroutine(CloseRoutine(power.CloseTime));
        }

        private IEnumerator CloseRoutine(float time)
        {
            Vector3 from = visual != null ? visual.localScale : Vector3.one;
            float scorchFrom = scorch != null ? scorch.fadeFactor : 0f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = 1f - t / time;
                if (visual != null) visual.localScale = new Vector3(from.x * k, from.y, from.z * k);
                if (rim != null) SetRim(k);
                if (scorch != null) scorch.fadeFactor = scorchFrom * k;
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // The generated outline mesh belongs to this hole alone.
            if (visual != null && visual.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && filter.sharedMesh.name == "HoleShape")
                Destroy(filter.sharedMesh);
        }
    }
}
