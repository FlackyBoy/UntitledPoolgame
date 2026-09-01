using System.Collections;
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

        private void Reset()
        {
            GetComponent<SphereCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PoolBall ball)) return;
            ball.OnPocketed();
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
        // popping out of existence mid-flight — that hard cut was the
        // "abrupt" part, not the light.
        private IEnumerator AuraWindDownRoutine(GameObject aura)
        {
            yield return new WaitForSeconds(settings.risingAuraActiveDuration);
            if (aura == null) yield break;

            foreach (ParticleSystem system in aura.GetComponentsInChildren<ParticleSystem>())
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            Destroy(aura, settings.risingAuraFadeOutBuffer);
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
