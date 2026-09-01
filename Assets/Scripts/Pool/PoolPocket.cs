using System.Collections;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    [RequireComponent(typeof(SphereCollider))]
    public class PoolPocket : MonoBehaviour
    {
        [Header("Pot halo")]
        // Matches the warm gold the pot flash used to use before it moved
        // here from LocalPoolPowerEffectReceiver's flat screen tint — a
        // world-space light burst reads as more "coming from the pocket"
        // than a full-screen overlay did.
        [SerializeField] private Color haloColor = new Color(1f, 0.85f, 0.35f);
        // Point light intensity is on a project-specific scale (depends on
        // whether URP's Physical Light Units is on) — this default assumes
        // it's off; bump it up if the halo doesn't read against the table
        // lighting once tested in-editor.
        [SerializeField] private float haloIntensity = 8f;
        [SerializeField] private float haloRange = 0.6f;
        [SerializeField] private float haloDuration = 0.35f;

        // Rising light-rays visual on top of the point-light flash — CFXR3
        // Magic Aura A (Runic) from the Cartoon FX Remaster pack already in
        // the project (Assets/Plugins/JMO Assets/.../Magic Misc). Its own
        // particle systems have Stop Action set to None (not Destroy), so
        // this script cleans up the spawned instance itself.
        [SerializeField] private GameObject risingAuraPrefab;
        // How long the aura actively emits new particles before winding
        // down — not the same as "how long it stays visible": after this,
        // emission stops (ParticleSystem.Stop(StopEmitting)) but particles
        // already in flight keep playing out their own lifetime, which is
        // what actually gives it a soft finish instead of an abrupt cut.
        [SerializeField] private float risingAuraActiveDuration = 1.2f;
        // Extra time kept alive after emission stops, for the last emitted
        // particles to finish fading before the GameObject is destroyed —
        // needs to comfortably cover the longest particle lifetime in the
        // prefab's systems, not the emission duration.
        [SerializeField] private float risingAuraFadeOutBuffer = 1f;
        // The prefab's own scale/pace was built for a much bigger, slower
        // effect than a pool pocket needs — shrunk and sped up per-instance
        // (transform scale + each ParticleSystem's simulationSpeed) rather
        // than editing the shared prefab asset itself.
        [SerializeField] private float risingAuraScale = 0.4f;
        [SerializeField] private float risingAuraSpeedMultiplier = 1.8f;

        // A persistent child light, reused every pot instead of
        // instantiating/destroying one each time — pockets get triggered
        // often enough over a match that the churn isn't worth it.
        private Light haloLight;
        private Coroutine haloRoutine;

        private void Awake()
        {
            haloLight = GetComponentInChildren<Light>(true);
            if (haloLight == null)
            {
                GameObject lightObject = new GameObject("PotHaloLight");
                lightObject.transform.SetParent(transform, worldPositionStays: false);
                lightObject.transform.localPosition = Vector3.zero;
                haloLight = lightObject.AddComponent<Light>();
                haloLight.type = LightType.Point;
            }

            haloLight.color = haloColor;
            haloLight.range = haloRange;
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

            if (risingAuraPrefab != null)
            {
                GameObject aura = Instantiate(risingAuraPrefab, transform.position, risingAuraPrefab.transform.rotation);
                aura.transform.localScale = risingAuraPrefab.transform.localScale * risingAuraScale;

                // simulationSpeed is read every frame, not just at Play() —
                // safe to set right after Instantiate even though the
                // particle systems (Play On Awake) already started this
                // same frame.
                foreach (ParticleSystem system in aura.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = system.main;
                    main.simulationSpeed *= risingAuraSpeedMultiplier;
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
            yield return new WaitForSeconds(risingAuraActiveDuration);
            if (aura == null) yield break;

            foreach (ParticleSystem system in aura.GetComponentsInChildren<ParticleSystem>())
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            Destroy(aura, risingAuraFadeOutBuffer);
        }

        private IEnumerator HaloRoutine()
        {
            haloLight.enabled = true;
            float elapsed = 0f;
            while (elapsed < haloDuration)
            {
                elapsed += Time.deltaTime;
                // Eased (quadratic) rather than linear — holds closer to
                // full brightness early on, then tapers off softly instead
                // of dimming at a constant, slightly clinical rate.
                float t = Mathf.Clamp01(elapsed / haloDuration);
                haloLight.intensity = haloIntensity * (1f - t * t);
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
