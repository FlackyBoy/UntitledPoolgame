using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    [RequireComponent(typeof(SphereCollider))]
    public class PoolPocket : MonoBehaviour
    {
        // Halo light + rising aura tuning lives in PoolPotEffectSettings
        // (Resources-loaded, shared by all 6 pockets — same pattern as
        // PoolPhysicsSettings) instead of a private copy of each field per
        // pocket. Unlike the player prefabs, the 6 PoolPocket instances
        // aren't prefab instances of one another (each generated
        // individually by PoolTableBuilder), so a shared asset is the only
        // way to tune them all at once.
        private static PoolPotEffectSettings settings;

        // A persistent child light, reused every pot instead of
        // instantiating/destroying one each time — pockets get triggered
        // often enough over a match that the churn isn't worth it.
        private Light haloLight;
        private Coroutine haloRoutine;

        // Live registry of every pocket on the table (same pattern as
        // PoolBall.Active) — lets the 8-ball call-shot prompt list the
        // available pockets without a hand-wired Inspector reference.
        private static readonly List<PoolPocket> active = new List<PoolPocket>();
        public static IReadOnlyList<PoolPocket> Active => active;

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        public float Radius => GetComponent<SphereCollider>().radius * transform.lossyScale.x;

        // Ball-in-hand placement (and the 8-ball pocket selector) only clamp
        // to the table's outer rectangle (PoolTableSurface.ClampToPlayArea) —
        // nothing stopped a ball from being placed directly on/inside a
        // pocket's own trigger radius. Its collider is disabled the whole
        // time it's being placed (PoolBall.BeginBallInHand), so nothing
        // fires while sliding it around — but the instant the collider is
        // re-enabled on confirm, physics discovers the overlap and pockets
        // it AGAIN, this time via a path that never calls
        // PoolMatchRules.RegisterFoul(): PoolBall.OnPocketed()'s cue-ball
        // branch just re-kinematic-izes it directly. The match-level
        // BallInHand flag never gets set, so nothing ever offers the player
        // a way to place it again — a permanently frozen cue ball that
        // ignores every future shot. Pushing placement candidates out of
        // every pocket's radius (plus clearance, typically the ball's own
        // radius) up front avoids the scenario entirely.
        public static Vector3 AvoidAllPockets(Vector3 position, float clearance)
        {
            foreach (PoolPocket pocket in active)
            {
                Vector3 pocketPosition = pocket.transform.position;
                Vector3 offset = position - pocketPosition;
                offset.y = 0f;

                float minDistance = pocket.Radius + clearance;
                float distance = offset.magnitude;
                if (distance >= minDistance) continue;

                Vector3 direction = distance > 0.0001f ? offset / distance : Vector3.forward;
                Vector3 pushed = pocketPosition + direction * minDistance;
                position = new Vector3(pushed.x, position.y, pushed.z);
            }
            return position;
        }

        // Human-readable location for the call-shot UI ("Coin haut-gauche",
        // "Milieu droite"...) computed from this pocket's position relative
        // to PoolTableSurface rather than a stored index — works regardless
        // of scene load order, and regardless of which table asset/rotation
        // generated these pockets. Mirrors the "haut/bas" (table's long
        // axis) vs. "gauche/droite" (short axis) vocabulary already used
        // elsewhere for this table (see PoolTableBuilder/CHANGELOG).
        public string DescribeLocation()
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return "Poche";

            Vector3 local = Quaternion.Inverse(surface.transform.rotation) * (transform.position - surface.transform.position);
            string side = local.z >= 0f ? "droite" : "gauche";

            bool isMiddle = Mathf.Abs(local.x) < surface.HalfLength * 0.5f;
            if (isMiddle) return $"Milieu {side}";

            string end = local.x >= 0f ? "haut" : "bas";
            return $"Coin {end}-{side}";
        }

        private void Awake()
        {
            if (settings == null)
            {
                settings = Resources.Load<PoolPotEffectSettings>("PoolPotEffectSettings");
                if (settings == null)
                {
                    Debug.LogWarning("PoolPotEffectSettings asset not found in Assets/Resources — using fallback defaults. Run Tools > Pool > Ensure Config Assets Exist to create it.");
                    settings = ScriptableObject.CreateInstance<PoolPotEffectSettings>();
                }
            }

            haloLight = GetComponentInChildren<Light>(true);
            if (haloLight == null)
            {
                GameObject lightObject = new GameObject("PotHaloLight");
                lightObject.transform.SetParent(transform, worldPositionStays: false);
                lightObject.transform.localPosition = Vector3.zero;
                haloLight = lightObject.AddComponent<Light>();
                haloLight.type = LightType.Point;
            }

            haloLight.color = settings.haloColor;
            haloLight.range = settings.haloRange;
            haloLight.intensity = 0f;
            haloLight.enabled = false;
        }

        // While the current player is calling a pocket for the 8 (see
        // LocalPoolAimController/PoolAimController's top-down HandleCallPocket),
        // whichever pocket is nearest the selector lights up steadily — reuses
        // this same haloLight rather than a second one, but skipped while a
        // real pot's decay/aura (PlayHalo/HaloRoutine) is already running on
        // it so the two never fight over the same light's intensity.
        public void SetSelectionHighlight(bool highlighted)
        {
            if (haloRoutine != null) return;
            haloLight.enabled = highlighted;
            haloLight.intensity = highlighted ? settings.selectionHighlightIntensity : 0f;
        }

        private void Reset()
        {
            GetComponent<SphereCollider>().isTrigger = true;
        }

        // ClosePocketPower: a solid (non-trigger) collider co-located with
        // the trigger sphere above, so a ball rolling toward a closed
        // pocket bounces off it instead of ever reaching deep enough to
        // overlap the trigger — plus a plain placeholder cap so the closure
        // is visible, and an explicit guard in OnTriggerEnter below as a
        // second line of defense in case a fast shot ever tunnels through
        // the blocker in one physics step.
        private SphereCollider closedBlocker;
        private GameObject closedVisual;

        public bool IsClosed { get; private set; }

        public void SetClosed(bool closed)
        {
            if (IsClosed == closed) return;
            IsClosed = closed;

            if (closedBlocker == null)
            {
                SphereCollider trigger = GetComponent<SphereCollider>();
                closedBlocker = gameObject.AddComponent<SphereCollider>();
                closedBlocker.isTrigger = false;
                closedBlocker.radius = trigger.radius;
                closedBlocker.center = trigger.center;
            }
            closedBlocker.enabled = closed;

            if (closedVisual == null) closedVisual = CreateClosedVisual();
            closedVisual.SetActive(closed);
        }

        // A custom prefab (settings.closedPocketCapPrefab) is used as-is —
        // not tinted, not rescaled, trusted to already look like a cap —
        // same convention as PoolPowerSpawnSettings' crate prefabs. Falls
        // back to a flat red placeholder cylinder sized off this pocket's
        // own radius when none is assigned.
        private GameObject CreateClosedVisual()
        {
            if (settings.closedPocketCapPrefab != null)
            {
                GameObject visual = Instantiate(settings.closedPocketCapPrefab, transform);
                visual.name = "ClosedCap";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = settings.closedPocketCapPrefab.transform.localScale * settings.closedPocketCapScale;
                return visual;
            }

            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placeholder.name = "ClosedCap";
            placeholder.transform.SetParent(transform, worldPositionStays: false);
            placeholder.transform.localPosition = Vector3.zero;
            float diameter = GetComponent<SphereCollider>().radius * 2f;
            placeholder.transform.localScale = new Vector3(diameter, diameter * 0.075f, diameter);
            Destroy(placeholder.GetComponent<Collider>()); // purely visual — closedBlocker above handles the actual physics
            placeholder.GetComponent<Renderer>().material.color = Color.red;
            return placeholder;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsClosed) return;
            if (!other.TryGetComponent(out PoolBall ball)) return;
            ball.OnPocketed(this);
            PlayHalo();
        }

        private void PlayHalo()
        {
            if (haloRoutine != null) StopCoroutine(haloRoutine);
            haloRoutine = StartCoroutine(HaloRoutine());

            if (settings.risingAuraPrefab != null)
            {
                GameObject aura = Instantiate(settings.risingAuraPrefab, transform.position, settings.risingAuraPrefab.transform.rotation);
                aura.transform.localScale = settings.risingAuraPrefab.transform.localScale * settings.risingAuraScale;

                // simulationSpeed is read every frame, not just at Play() —
                // safe to set right after Instantiate even though the
                // particle systems (Play On Awake) already started this
                // same frame.
                foreach (ParticleSystem system in aura.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = system.main;
                    main.simulationSpeed *= settings.risingAuraSpeedMultiplier;
                }

                StartCoroutine(AuraWindDownRoutine(aura));
            }
        }

        // Stopping emission (rather than an immediate Destroy) lets whatever
        // particles are already in flight finish their own fade instead of
        // popping out of existence mid-flight.
        private IEnumerator AuraWindDownRoutine(GameObject aura)
        {
            yield return new WaitForSeconds(settings.risingAuraActiveDuration);
            if (aura == null) yield break;

            ParticleSystem[] systems = aura.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem system in systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // Poll until every system in the hierarchy actually reports no
            // particles left, rather than guessing a fixed delay before
            // Destroy() — a fixed guess is exactly what caused the "plays
            // then cuts off abruptly" bug: whenever the real particle
            // lifetime ran longer than the guess, Destroy() fired while
            // particles were still fully visible. IsAlive() only looks at a
            // system's OWN Transform children, so every system is checked
            // individually rather than just the root. risingAuraFadeOutBuffer
            // is now a safety ceiling (in case something never reports dead)
            // instead of a fixed wait — normally this finishes well before
            // hitting it.
            float safetyDeadline = Time.time + settings.risingAuraFadeOutBuffer;
            bool anyAlive = true;
            while (anyAlive && aura != null && Time.time < safetyDeadline)
            {
                anyAlive = false;
                foreach (ParticleSystem system in systems)
                {
                    if (system != null && system.IsAlive())
                    {
                        anyAlive = true;
                        break;
                    }
                }
                yield return null;
            }

            if (aura != null) Destroy(aura);
        }

        private IEnumerator HaloRoutine()
        {
            haloLight.enabled = true;
            float elapsed = 0f;
            while (elapsed < settings.haloDuration)
            {
                elapsed += Time.deltaTime;
                // Eased (quadratic) rather than linear — holds closer to
                // full brightness early on, then tapers off softly instead
                // of dimming at a constant, slightly clinical rate.
                float t = Mathf.Clamp01(elapsed / settings.haloDuration);
                haloLight.intensity = settings.haloIntensity * (1f - t * t);
                yield return null;
            }
            haloLight.enabled = false;
            haloRoutine = null;
        }

        // Always-on (not just when selected) so all 6 pockets can be compared
        // to a custom table's real pocket openings at once — the generated
        // positions are only an idealized-rectangle approximation and often
        // need nudging/resizing to line up with a specific model.
        private void OnDrawGizmos()
        {
            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, collider.radius * transform.lossyScale.x);
        }
    }
}
