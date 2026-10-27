using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    [RequireComponent(typeof(SphereCollider))]
    public class PoolPocket : MonoBehaviour
    {
        // Shared by the six pockets, which aren't prefab instances of one
        // another.
        private static PoolPotEffectSettings settings;

        // Reused on every pot.
        private Light haloLight;
        private Coroutine haloRoutine;

        // Every pocket on the table.
        private static readonly List<PoolPocket> active = new List<PoolPocket>();
        public static IReadOnlyList<PoolPocket> Active => active;

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        // The pocket's own trigger, fetched once (Awake): Radius is read for
        // every pocket on each ball-in-hand placement frame.
        private SphereCollider trigger;
        public float Radius => (trigger != null ? trigger : trigger = GetComponent<SphereCollider>()).radius * transform.lossyScale.x;

        // Pushes a placement spot out of every pocket. A cue ball placed
        // inside a pocket's trigger got potted again as its collider came
        // back, without a foul, and stayed frozen.
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

        // "Coin haut-droite", "Milieu gauche"..., from the position relative to
        // the table (haut/bas along its length, gauche/droite across).
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
            // Before SetClosed can add the blocker, another SphereCollider.
            trigger = GetComponent<SphereCollider>();
            if (settings == null)
                settings = PoolSettingsLoader.LoadOrDefault<PoolPotEffectSettings>("PoolPotEffectSettings");

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

        // Steady light on the pocket being pointed at (power pick, 8 call),
        // unless a pot's halo is already running on it.
        public void SetSelectionHighlight(bool highlighted)
        {
            if (haloRoutine != null) return;
            haloLight.enabled = highlighted;
            haloLight.intensity = highlighted ? settings.selectionHighlightIntensity : 0f;
        }

        // Pulsing ring while pointed at. A count, since both players can
        // point at the same pocket.
        private int targetCount;
        private LineRenderer targetRing;
        private float targetRingRadius;

        public void SetTargeted(bool targeted)
        {
            targetCount = Mathf.Max(0, targetCount + (targeted ? 1 : -1));
            bool on = targetCount > 0;
            if (on && targetRing == null)
            {
                targetRing = RingLine.Create(transform, "TargetRing", settings.targetRingWidth, settings.targetRingColor);
                targetRingRadius = Radius * settings.targetRingRadiusScale;
            }
            if (targetRing != null) targetRing.enabled = on;
            SetSelectionHighlight(on);
        }

        // The ring's pulse, shared with the table-point preview (LocalPocketPointer).
        public static float RingPulse() => settings == null ? 1f :
            1f + settings.targetRingPulseAmount * Mathf.Sin(Time.unscaledTime * settings.targetRingPulseRate * Mathf.PI * 2f);

        // Only does anything while a player is pointing at this pocket.
        private void Update()
        {
            if (targetCount == 0 || targetRing == null) return;
            RingLine.Set(targetRing, transform.position + Vector3.up * settings.targetRingHeight, targetRingRadius * RingPulse());
        }

        private void Reset()
        {
            GetComponent<SphereCollider>().isTrigger = true;
        }

        // Close Pocket: a solid sphere over the trigger so balls bounce off,
        // a cap to show it, and a check in OnTriggerEnter in case a fast ball
        // gets through.
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

        // The cap prefab as is, or a flat red cylinder sized to the pocket.
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

        // Off while a mode plays its own pot effect (Power Wash Pool's
        // splash): no halo, no rising aura, no shake of both screens.
        public static bool DefaultPotEffects { get; set; } = true;

        private void OnTriggerEnter(Collider other)
        {
            if (IsClosed) return;
            if (!other.TryGetComponent(out PoolBall ball)) return;
            ball.OnPocketed(this);
            if (DefaultPotEffects) PlayHalo();
        }

        private void PlayHalo()
        {
            if (haloRoutine != null) StopCoroutine(haloRoutine);
            haloRoutine = StartCoroutine(HaloRoutine());

            if (settings.risingAuraPrefab != null)
            {
                GameObject aura = Instantiate(settings.risingAuraPrefab, transform.position, settings.risingAuraPrefab.transform.rotation);
                aura.transform.localScale = settings.risingAuraPrefab.transform.localScale * settings.risingAuraScale;

                // simulationSpeed can be set after Play On Awake has started.
                foreach (ParticleSystem system in aura.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = system.main;
                    main.simulationSpeed *= settings.risingAuraSpeedMultiplier;
                }

                StartCoroutine(AuraWindDownRoutine(aura));
            }
        }

        // Stops emission so the particles in flight fade out.
        private IEnumerator AuraWindDownRoutine(GameObject aura)
        {
            yield return new WaitForSeconds(settings.risingAuraActiveDuration);
            if (aura == null) yield break;

            ParticleSystem[] systems = aura.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem system in systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // Waits until no system has particles left (a fixed delay cut
            // them off). Each system is checked, IsAlive only looks at its own
            // children. risingAuraFadeOutBuffer is a ceiling.
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
                // Quadratic ease, softer than linear.
                float t = Mathf.Clamp01(elapsed / settings.haloDuration);
                haloLight.intensity = settings.haloIntensity * (1f - t * t);
                yield return null;
            }
            haloLight.enabled = false;
            haloRoutine = null;
        }

        // Always drawn, to compare all six with a model's real pockets.
        private void OnDrawGizmos()
        {
            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, collider.radius * transform.lossyScale.x);
        }
    }
}
