using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // Hungry Hole: turns a player's cue ball into a hole. The ball is still
    // what the cue strikes, but hidden, sliding like a puck at a fixed height,
    // and touching the others through an upright cylinder the size of the
    // hole: a ball bigger than the hole bounces off it like a ball would, a
    // smaller one passes over and is swallowed once its centre is inside.
    // A plain black disc on the felt and a ring in the player's colour
    // follow it. Pockets are checked on the
    // hole's centre, not its edge, so a big hole can still skirt them.
    [RequireComponent(typeof(PoolBall))]
    public class HungryHoleCue : MonoBehaviour
    {
        private static readonly List<HungryHoleCue> all = new List<HungryHoleCue>();
        public static IReadOnlyList<HungryHoleCue> All => all;

        // For Feel (GameFeel): owner, where, how big a bite (0..1).
        public static event Action<int, Vector3, float> Swallowed;
        // Eater, eaten, where.
        public static event Action<int, int, Vector3> AteHole;
        // Owner, where, growth in normal-ball radii (negative: it shrank).
        public static event Action<int, Vector3, float> Grew;

        public PoolBall Ball { get; private set; }
        public int Owner { get; private set; }
        // Current radius (eases towards TargetRadius).
        public float Radius { get; private set; }
        public float TargetRadius { get; private set; }
        // A normal ball's radius (this cue ball's): the settings' sizes are
        // multiples of it, whatever the table's scale.
        private float unit;

        private const int Sides = 20;

        private HungryHoleSettings settings;
        private HungryHoleRuleSet rules;
        // Root following the ball on the felt: the disc (scaled) and the ring.
        private Transform hole;
        private Transform disc;
        private static Mesh discMesh;
        private static Material discMaterial;
        private LineRenderer ring;
        private MeshCollider body;
        private Mesh bodyMesh;
        private RigidbodyConstraints savedConstraints;
        private float bodyHeight;
        private float appliedRadius = -1f;
        // Spring towards TargetRadius (overshoots, bounces back).
        private float radiusSpeed;
        // The ring that jumps to the new size, and its animation.
        private LineRenderer growRing;
        private float growRingStart = -1f;
        private float growRingFrom;
        private Color growRingColor;
        // Juice: the hole swells and wobbles back, the ring flashes.
        private float gulpStart = -1f;
        private float gulpStrength;

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        // Called again by Rejouer: back to the starting size.
        public void Init(HungryHoleSettings hungrySettings, HungryHoleRuleSet ruleSet, int owner)
        {
            settings = hungrySettings;
            rules = ruleSet;
            Owner = owner;
            Ball = GetComponent<PoolBall>();
            unit = Ball.Radius / Ball.SizeScale;
            Radius = TargetRadius = ClampRadius(settings.startSize * unit);
            radiusSpeed = 0f;
            growRingStart = -1f;

            if (body == null) Build();
            Apply(force: true);
        }

        private void Build()
        {
            Ball.SetMeshesVisible(false);
            Ball.PuckDeceleration = settings.deceleration;
            savedConstraints = Ball.Rigidbody.constraints;
            Ball.Rigidbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            bodyHeight = Ball.Radius * 2f;

            // A child at world scale, so the mesh is built in metres.
            var bodyObject = new GameObject("HungryHoleBody");
            bodyObject.transform.SetParent(transform, false);
            float scale = transform.lossyScale.x;
            bodyObject.transform.localScale = scale > 1e-6f ? Vector3.one / scale : Vector3.one;
            bodyMesh = new Mesh { name = "HungryHoleBody" };
            body = bodyObject.AddComponent<MeshCollider>();
            body.convex = true;
            Ball.UseBodyCollider(body);
            // The cylinder reaches the felt; the frozen height keeps it up.
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface != null && surface.Felt != null) Physics.IgnoreCollision(body, surface.Felt);

            hole = new GameObject($"HungryHole J{Owner + 1}").transform;
            hole.position = FeltPoint();
            disc = new GameObject("Disc").transform;
            disc.SetParent(hole, false);
            disc.localPosition = Vector3.up * 0.003f;
            disc.gameObject.AddComponent<MeshFilter>().sharedMesh = DiscMesh();
            var discRenderer = disc.gameObject.AddComponent<MeshRenderer>();
            discRenderer.sharedMaterial = DiscMaterial();
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            discRenderer.receiveShadows = false;
            if (settings.ringWidth > 0f)
            {
                ring = RingLine.Create(hole, "PlayerRing", settings.ringWidth, Owner == 0 ? settings.player1Ring : settings.player2Ring);
                ring.useWorldSpace = false;
            }
            if (settings.debugLogs) LogDiscHeight();
            growRing = RingLine.Create(hole, "GrowRing", settings.growRingWidth, Color.clear);
            growRing.useWorldSpace = false;
            growRing.enabled = false;
        }

        // A flat disc of radius 1 facing up, shared by every hole.
        private static Mesh DiscMesh()
        {
            if (discMesh != null) return discMesh;
            const int segments = 64;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // Clockwise seen from above: facing up.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % segments;
                triangles[i * 3 + 2] = 1 + i;
            }
            discMesh = new Mesh { name = "HungryHoleDisc", vertices = vertices, triangles = triangles };
            discMesh.RecalculateNormals();
            discMesh.RecalculateBounds();
            return discMesh;
        }

        // Unlit black, in the opaque queue just after the felt (like TableHole).
        private static Material DiscMaterial()
        {
            if (discMaterial == null)
                discMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.black, renderQueue = 2450 };
            return discMaterial;
        }

        // Back to a plain cue ball (another mode starts).
        public void Remove()
        {
            if (Ball != null)
            {
                Ball.UseBodyCollider(null);
                Ball.PuckDeceleration = 0f;
                Ball.Rigidbody.constraints = savedConstraints;
                Ball.SetMeshesVisible(true);
            }
            if (hole != null) Destroy(hole.gameObject);
            if (body != null) Destroy(body.gameObject);
            if (bodyMesh != null) Destroy(bodyMesh);
            Destroy(this);
        }

        private float ClampRadius(float radius) =>
            Mathf.Clamp(radius, settings.sizeLimits.x * unit, Mathf.Max(settings.sizeLimits.x, settings.sizeLimits.y) * unit);

        // delta in normal-ball radii.
        public void Grow(float delta)
        {
            float before = TargetRadius;
            TargetRadius = ClampRadius(TargetRadius + delta * unit);
            float change = TargetRadius - before;
            if (Mathf.Abs(change) < 1e-5f) return;
            // A kick on top of the spring: it bursts out (or snaps in).
            radiusSpeed += change * 6f;
            growRingStart = Time.time;
            growRingFrom = Radius;
            growRingColor = change > 0f ? settings.goodBiteColor : settings.badBiteColor;
            Grew?.Invoke(Owner, FeltPoint(), change / unit);
        }

        // Damped spring: overshoots the new size and bounces back to it.
        private void Update()
        {
            if (settings == null) return;
            if (Mathf.Abs(TargetRadius - Radius) < 1e-5f && Mathf.Abs(radiusSpeed) < 1e-4f) { Radius = TargetRadius; radiusSpeed = 0f; return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float k = settings.growStiffness;
            float c = 2f * settings.growBounce * Mathf.Sqrt(k);
            radiusSpeed += (k * (TargetRadius - Radius) - c * radiusSpeed) * dt;
            Radius = Mathf.Max(unit * 0.2f, Radius + radiusSpeed * dt);
        }

        // The ring at the new size: out at once (a bit past it), thick and
        // bright, then fading while the hole catches up.
        private void AnimateGrowRing()
        {
            if (growRing == null || growRingStart < 0f) return;
            float x = (Time.time - growRingStart) / Mathf.Max(0.01f, settings.growRingTime);
            if (x >= 1f) { growRingStart = -1f; growRing.enabled = false; return; }
            growRing.enabled = true;
            float jump = 1f - Mathf.Pow(1f - Mathf.Min(1f, x / 0.15f), 3f);
            float overshoot = 1f + settings.growRingOvershoot * Mathf.Sign(TargetRadius - growRingFrom) * (1f - x);
            float r = Mathf.Lerp(growRingFrom, TargetRadius * overshoot, jump);
            for (int i = 0; i < RingLine.Segments; i++)
            {
                float a = i * Mathf.PI * 2f / RingLine.Segments;
                growRing.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.005f, Mathf.Sin(a) * r));
            }
            Color c = growRingColor;
            c.a = 1f - x * x;
            growRing.startColor = growRing.endColor = c;
            growRing.widthMultiplier = settings.growRingWidth * (1f - 0.6f * x);
        }

        private void LateUpdate()
        {
            if (hole == null || Ball == null) return;
            // Hidden while in hand after a scratch: it isn't on the table.
            bool onTable = !Ball.Rigidbody.isKinematic;
            if (hole.gameObject.activeSelf != onTable) hole.gameObject.SetActive(onTable);
            hole.transform.position = FeltPoint();
            float swell = AnimateGulp();
            // The black disc bumps harder than its outline.
            float discScale = Radius * (1f + (swell - 1f) * settings.discBump);
            if (disc != null) disc.localScale = new Vector3(discScale, 1f, discScale);
            SetRing(Radius * swell);
            AnimateGrowRing();
        }

        // Juice for something swallowed, size in normal-ball radii: a gulp of
        // the hole, a flash of its ring, from a certain size a shake of this
        // player's screen, and the Feel moment (sound, rumble…).
        // good: a bite that helps (white), or one that hurts (red).
        public void Gulp(float size, bool good = true)
        {
            biteColor = good ? settings.goodBiteColor : settings.badBiteColor;
            float k = Mathf.Clamp01(size / Mathf.Max(0.1f, settings.shakeFullSize));
            float strength = settings.gulpAmount * Mathf.Lerp(0.5f, 1f, k);
            // A second bite during a gulp doesn't make it smaller.
            if (gulpStart < 0f || strength >= CurrentGulpStrength()) { gulpStart = Time.time; gulpStrength = strength; }

            if (settings.shockwave && hole != null) StartCoroutine(Shockwave(Mathf.Lerp(0.5f, 1f, k)));
            if (settings.beam && hole != null) StartCoroutine(Beam(Mathf.Lerp(0.4f, 1f, k), biteColor));

            // Alone at the keyboard, the one screen plays it for either hole.
            int screen = UnityEngine.InputSystem.PlayerInput.all.Count <= 1 ? -1 : Owner;
            bool shake = size >= settings.shakeMinSize && settings.shakeMagnitude > 0f;
            bool big = size >= settings.bigBiteSize;
            if (shake || big)
                LocalPoolPowerEffectReceiver.PlayImpact(screen, shake ? settings.shakeMagnitude * Mathf.Max(0.4f, k) : 0f, settings.shakeDuration,
                    good ? PlayerColor : settings.badBiteColor, big ? settings.flashAlpha * Mathf.Max(0.5f, k) : 0f, settings.flashDuration);
            if (big && settings.hitstopTime > 0f) StartCoroutine(MeleeHit.Hitstop(settings.hitstopTime, 0.05f));
            Swallowed?.Invoke(Owner, FeltPoint(), k);
            if (settings.debugLogs)
                Debug.Log($"[HungryHole] Gloups J{Owner + 1} ({(good ? "bon morceau" : "mauvais morceau")}) : taille avalée {size:0.0}, le trou gonfle de {strength * 100f:0} %" +
                          $"{(shake ? ", écran secoué" : "")}{(big ? ", flash et arrêt sur image" : "")}.", this);
        }

        private Color PlayerColor => Owner == 0 ? settings.player1Ring : settings.player2Ring;
        // Colour of the last bite (good / bad), for the ring's flash.
        private Color biteColor = Color.white;

        // A column of light shooting up from the hole (white core fading to
        // the player's colour, transparent at the top), and a light on the
        // table. Rises at once, then narrows and fades; follows the hole.
        private IEnumerator Beam(float strength, Color color)
        {
            if (settings.beamPrefab != null)
            {
                yield return BeamFromPrefab(strength, color);
                yield break;
            }
            // A single sharp, capped column.
            MeshRenderer core = BeamPart("HungryHoleBeam", BeamMesh());
            var block = new MaterialPropertyBlock();

            Light glow = null;
            if (settings.beamLightIntensity > 0f && settings.beamLightRange > 0f)
            {
                glow = new GameObject("HungryHoleBeamLight").AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = color;
                glow.range = settings.beamLightRange * Mathf.Lerp(0.6f, 1f, strength);
                glow.shadows = LightShadows.None;
            }

            float height = settings.beamHeight * strength;
            for (float t = 0f; t < settings.beamTime; t += Time.unscaledDeltaTime)
            {
                float x = t / settings.beamTime;
                Vector3 foot = FeltPoint();
                // Up at once (first fifth), holds, then pinches to nothing:
                // it stays sharp, it never goes see-through.
                float rise = Mathf.Min(1f, x / 0.2f);
                float pinch = x < 0.45f ? 1f : 1f - Mathf.Pow((x - 0.45f) / 0.55f, 2f);
                float width = Radius * settings.beamWidth * pinch;
                float h = height * (1f - (1f - rise) * (1f - rise) * (1f - rise));
                core.transform.position = foot;
                core.transform.localScale = new Vector3(width, h, width);
                Color c = color;
                c.a = settings.beamAlpha;
                block.SetColor("_Color", c);
                core.SetPropertyBlock(block);
                if (glow != null)
                {
                    glow.transform.position = foot + Vector3.up * h * 0.3f;
                    glow.intensity = settings.beamLightIntensity * strength * pinch;
                }
                yield return null;
            }
            Destroy(core.gameObject);
            if (glow != null) Destroy(glow.gameObject);
        }

        private static MeshRenderer BeamPart(string name, Mesh mesh)
        {
            var part = new GameObject(name);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = part.AddComponent<MeshRenderer>();
            r.sharedMaterial = BeamMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        // The settings' effect instead of the generated beam: at the hole's
        // centre and its scale, following it, in the bite's colour; its
        // particles stop after Beam Time and finish on their own.
        private IEnumerator BeamFromPrefab(float strength, Color color)
        {
            GameObject prefab = settings.beamPrefab;
            GameObject effect = Instantiate(prefab, FeltPoint(), prefab.transform.rotation);
            effect.transform.localScale = prefab.transform.localScale * (Radius * 2f * settings.beamPrefabScale * Mathf.Lerp(0.7f, 1f, strength));
            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = color;
            }
            for (float t = 0f; t < settings.beamTime; t += Time.unscaledDeltaTime)
            {
                if (effect == null) yield break;
                effect.transform.position = FeltPoint();
                yield return null;
            }
            if (effect == null) yield break;
            foreach (ParticleSystem system in systems)
                if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(effect, 3f);
        }

        private static Mesh beamMesh;
        private static Material beamMaterial;

        // A capped tube of radius 1 and height 1 standing on its base, one
        // even colour.
        private static Mesh BeamMesh()
        {
            if (beamMesh != null) return beamMesh;
            const int sides = 32;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var rim = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(rim);
                vertices.Add(rim + Vector3.up);
                colors.Add(Color.white);
                colors.Add(Color.white);
            }
            for (int i = 0; i < sides; i++)
            {
                int b = i * 2, n = ((i + 1) % sides) * 2;
                triangles.AddRange(new[] { b, b + 1, n, n, b + 1, n + 1 });
            }
            int top = vertices.Count;
            vertices.Add(Vector3.up);
            colors.Add(Color.white);
            for (int i = 0; i < sides; i++)
                triangles.AddRange(new[] { top, ((i + 1) % sides) * 2 + 1, i * 2 + 1 });
            beamMesh = new Mesh { name = "HungryHoleBeam" };
            beamMesh.SetVertices(vertices);
            beamMesh.SetColors(colors);
            beamMesh.SetTriangles(triangles, 0);
            beamMesh.RecalculateBounds();
            return beamMesh;
        }

        // Sprites/Default: unlit, transparent, both faces, vertex colours
        // times its tint (set per beam through a property block).
        private static Material BeamMaterial()
        {
            if (beamMaterial == null) beamMaterial = new Material(Shader.Find("Sprites/Default")) { name = "HungryHoleBeam" };
            return beamMaterial;
        }

        // A ring in the player's colour running out from the hole, fading.
        private IEnumerator Shockwave(float strength)
        {
            LineRenderer wave = RingLine.Create(null, "HungryHoleShockwave", settings.shockwaveWidth, PlayerColor);
            float from = Radius, to = Radius * Mathf.Lerp(1.5f, settings.shockwaveReach, strength);
            for (float t = 0f; t < settings.shockwaveTime; t += Time.unscaledDeltaTime)
            {
                float x = t / settings.shockwaveTime;
                // Centred on the hole as it moves.
                Vector3 center = FeltPoint() + Vector3.up * 0.005f;
                float ease = 1f - (1f - x) * (1f - x);
                RingLine.Set(wave, center, Mathf.Lerp(from, to, ease));
                Color c = PlayerColor;
                c.a = 1f - x;
                wave.startColor = wave.endColor = c;
                wave.widthMultiplier = settings.shockwaveWidth * (1f - x * 0.8f);
                yield return null;
            }
            Destroy(wave.gameObject);
        }

        private float CurrentGulpStrength()
        {
            float x = (Time.time - gulpStart) / settings.gulpTime;
            return x >= 1f ? 0f : gulpStrength * (1f - x);
        }

        // The gulp's swell (1 = none) for this frame; also flashes the ring.
        private float AnimateGulp()
        {
            if (gulpStart < 0f) return 1f;
            float x = (Time.time - gulpStart) / settings.gulpTime;
            Color ringColor = Owner == 0 ? settings.player1Ring : settings.player2Ring;
            if (x >= 1f)
            {
                gulpStart = -1f;
                if (ring != null) { ring.startColor = ring.endColor = ringColor; ring.widthMultiplier = settings.ringWidth; }
                return 1f;
            }
            if (ring != null)
            {
                ring.startColor = ring.endColor = Color.Lerp(biteColor, ringColor, x * 2f);
                ring.widthMultiplier = settings.ringWidth * (1f + settings.ringPulse * (1f - x) * (1f - x));
            }
            // Swells at once, then wobbles back down (a damped sine).
            return 1f + gulpStrength * Mathf.Sin(x * Mathf.PI * 3f) * (1f - x) * (1f - x);
        }

        private void SetRing(float radius)
        {
            if (ring == null) return;
            ring.positionCount = RingLine.Segments;
            for (int i = 0; i < RingLine.Segments; i++)
            {
                float a = i * Mathf.PI * 2f / RingLine.Segments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.004f, Mathf.Sin(a) * radius));
            }
        }

        // Diagnostic: the disc must sit above the felt that's drawn, not only
        // above its collider.
        private void LogDiscHeight()
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return;
            float drawnTop = float.NegativeInfinity;
            foreach (Renderer r in surface.GetComponentsInChildren<Renderer>())
                drawnTop = Mathf.Max(drawnTop, r.bounds.max.y);
            string drawn = float.IsNegativeInfinity(drawnTop) ? "pas de rendu sur l'objet du tapis" : $"{drawnTop:0.0000}";
            Debug.Log($"[HungryHole] Disque noir de J{Owner + 1} à y={disc.position.y:0.0000} ; dessus du tapis : collider {surface.FeltTopY:0.0000}, rendu {drawn}.", this);
        }

        private Vector3 FeltPoint()
        {
            Vector3 p = transform.position;
            PoolTableSurface surface = PoolTableSurface.Instance;
            p.y = surface != null ? surface.FeltTopY : p.y - Ball.Radius;
            return p;
        }

        private void FixedUpdate()
        {
            if (settings == null || Ball.Rigidbody.isKinematic) return;
            Apply(force: false);
            PoolMatchRules match = PoolMatchRules.Instance;
            if (match == null || !match.MatchStarted || match.GameOver) return;

            Vector3 center = transform.position;
            foreach (PoolPocket pocket in PoolPocket.Active)
            {
                if (pocket.IsClosed || HorizontalDistance(center, pocket.transform.position) >= pocket.Radius) continue;
                if (settings.debugLogs) Debug.Log($"[HungryHole] Le trou de J{Owner + 1} tombe dans {pocket.name}.", this);
                Ball.OnPocketed(pocket);
                return;
            }

            EatProps(center);
            EatHoles(center);

            for (int i = PoolBall.Active.Count - 1; i >= 0; i--)
            {
                PoolBall ball = PoolBall.Active[i];
                if (ball.IsCueBall || ball.InTableHole || ball.Rigidbody.isKinematic) continue;
                // Collisions are cleared whenever a collider is switched off
                // (a ball in hand, potted and respotted): set every step.
                bool fits = Fits(ball);
                Physics.IgnoreCollision(body, ball.BodyCollider, fits);
                if (!fits || HorizontalDistance(center, ball.transform.position) >= Radius) continue;
                bool good = rules == null || rules.OnSwallowed(this, ball);
                ball.FallIntoTableHole(FeltPoint(), settings.sinkTime, pocket: true, follow: hole);
                Gulp(ball.SizeScale, good);
            }
        }

        public bool Fits(PoolBall ball) => ball.Radius < Radius;

        private void OnCollisionEnter(Collision collision)
        {
            if (settings == null) return;
            // The last hole to touch a ball gets the credit if it's potted.
            if (collision.collider.TryGetComponent(out PoolBall ball))
            {
                rules?.NoteTouch(ball, Owner);
                return;
            }
            if (!settings.debugLogs) return;
            // Diagnostic: a prop bumped into, and why it wasn't eaten.
            HungryHoleProp prop = collision.collider.GetComponentInParent<HungryHoleProp>();
            if (prop == null) return;
            if (collision.collider == prop.Body)
                Debug.Log($"[HungryHole] Le trou de J{Owner + 1} ({Radius * 100f:0.0} cm) bute sur {prop.name} ({prop.FitRadius * 100f:0.0} cm) : trop gros pour lui.", prop);
            else
                Debug.Log($"[HungryHole] Le trou de J{Owner + 1} bute sur un collider resté sur {prop.name} : {collision.collider.name}.", collision.collider);
        }

        // A hole clearly bigger than the other eats it once over its centre.
        private void EatHoles(Vector3 center)
        {
            foreach (HungryHoleCue other in all)
            {
                if (other == this || other.Ball == null || other.Ball.Rigidbody.isKinematic || other.body == null) continue;
                bool canEat = Radius >= other.Radius * settings.eatHoleRatio;
                // Passes over the smaller hole instead of bumping it.
                Physics.IgnoreCollision(body, other.body, canEat || other.Radius >= Radius * settings.eatHoleRatio);
                if (!canEat || HorizontalDistance(center, other.transform.position) >= Radius) continue;
                Gulp(other.Radius / unit * 1.5f);
                AteHole?.Invoke(Owner, other.Owner, FeltPoint());
                rules?.OnHoleEaten(this, other);
                return;   // the list changes owner state; one per step is plenty
            }
        }

        // Eaten by the other hole: comes back at a share of its size (at once).
        public void ShrinkBy(float factor)
        {
            Radius = TargetRadius = ClampRadius(TargetRadius * factor);
            Apply(force: true);
        }

        // Props: bounced off when too big, eaten once their centre is in.
        private void EatProps(Vector3 center)
        {
            for (int i = HungryHoleProp.All.Count - 1; i >= 0; i--)
            {
                HungryHoleProp prop = HungryHoleProp.All[i];
                if (prop.IsEaten || prop.Body == null) continue;
                bool fits = prop.FitRadius < Radius;
                Physics.IgnoreCollision(body, prop.Body, fits);
                if (!fits || HorizontalDistance(center, prop.Center) >= Radius) continue;
                rules?.OnPropEaten(this, prop);
                prop.Eat(hole, settings.sinkTime, settings.propSpin);
                Gulp(prop.Size);
            }
        }

        // The cylinder at the current radius (the drawing follows in LateUpdate).
        private void Apply(bool force)
        {
            if (!force && Mathf.Abs(appliedRadius - Radius) < 0.0005f) return;
            appliedRadius = Radius;
            BuildCylinder(Radius);
            body.sharedMesh = null;   // forces the collider to take the new mesh
            body.sharedMesh = bodyMesh;

        }

        // Rebuilt at every step while the size springs: the arrays are kept.
        private readonly Vector3[] cylinderVertices = new Vector3[Sides * 2];
        private static int[] cylinderTriangles;
        private bool cylinderTopologySet;

        private void BuildCylinder(float radius)
        {
            float half = bodyHeight * 0.5f;
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                var rim = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                cylinderVertices[i] = rim + Vector3.down * half;
                cylinderVertices[Sides + i] = rim + Vector3.up * half;
            }
            if (cylinderTriangles == null)
            {
                // A convex collider only needs the points; triangles for validity.
                var triangles = new List<int>(Sides * 12);
                for (int i = 0; i < Sides; i++)
                {
                    int n = (i + 1) % Sides;
                    triangles.AddRange(new[] { i, n, Sides + i, n, Sides + n, Sides + i });
                    if (i > 0 && i < Sides - 1)
                    {
                        triangles.AddRange(new[] { 0, i + 1, i });
                        triangles.AddRange(new[] { Sides, Sides + i, Sides + i + 1 });
                    }
                }
                cylinderTriangles = triangles.ToArray();
            }
            bodyMesh.vertices = cylinderVertices;
            if (!cylinderTopologySet) { bodyMesh.triangles = cylinderTriangles; cylinderTopologySet = true; }
            bodyMesh.RecalculateBounds();
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDestroy()
        {
            if (hole != null) Destroy(hole.gameObject);
            if (bodyMesh != null) Destroy(bodyMesh);
        }
    }
}
