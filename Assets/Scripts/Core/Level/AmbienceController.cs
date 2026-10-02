using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UntitledPoolGame.Core
{
    // The level's ambiences and the one in use. Applies an AmbiencePreset to
    // the scene (sky, ambient light, main light, global volume, fog, decor
    // lamps) and switches the Adaptive Probe Volume lighting scenario baked
    // for it, so the same room can change mood while playing (dimensions,
    // lot 6). Placed and filled by the Level Maker.
    public class AmbienceController : MonoBehaviour
    {
        [Tooltip("Ambiances disponibles dans ce niveau (chacune a son scénario d'éclairage précalculé).")]
        public List<AmbiencePreset> presets = new List<AmbiencePreset>();
        [Tooltip("Ambiance appliquée au lancement.")]
        public AmbiencePreset active;
        [Tooltip("Lumière principale (soleil, lune).")]
        public Light mainLight;
        [Tooltip("Volume global de post-process.")]
        public Volume volume;
        public bool applyOnStart = true;

        public static AmbienceController Instance { get; private set; }

        private Coroutine blend;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (applyOnStart && active != null) Apply(active);
        }

        // Everything at once (and the lighting scenario).
        public void Apply(AmbiencePreset preset)
        {
            if (preset == null) return;
            active = preset;

            RenderSettings.skybox = preset.skybox;
            RenderSettings.ambientMode = preset.ambientMode;
            RenderSettings.ambientSkyColor = preset.ambientSky;
            RenderSettings.ambientEquatorColor = preset.ambientEquator;
            RenderSettings.ambientGroundColor = preset.ambientGround;
            RenderSettings.ambientLight = preset.ambientSky;
            RenderSettings.ambientIntensity = preset.ambientIntensity;
            RenderSettings.reflectionIntensity = preset.reflectionIntensity;
            RenderSettings.fog = preset.fog;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogMode = preset.fogMode;
            RenderSettings.fogDensity = preset.fogDensity;
            RenderSettings.fogStartDistance = preset.fogLinearRange.x;
            RenderSettings.fogEndDistance = preset.fogLinearRange.y;

            if (mainLight != null)
            {
                mainLight.color = preset.mainLightColor;
                mainLight.intensity = preset.mainLightIntensity;
                mainLight.transform.rotation = Quaternion.Euler(preset.mainLightRotation);
                mainLight.shadows = preset.mainLightShadows;
                mainLight.shadowStrength = preset.mainLightShadowStrength;
                RenderSettings.sun = mainLight;
            }
            if (volume != null) volume.sharedProfile = preset.volumeProfile;

            foreach (Light light in DecorLights())
            {
                AmbienceLight memory = light.GetComponent<AmbienceLight>();
                if (memory == null) memory = light.gameObject.AddComponent<AmbienceLight>();
                if (!memory.captured) memory.Capture(light);
                light.color = Color.Lerp(memory.baseColor, preset.decorTint, preset.decorTintStrength);
                light.intensity = memory.baseIntensity * preset.decorIntensity;
            }

            DynamicGI.UpdateEnvironment();
            SetScenario(preset.Scenario);
        }

        // While playing: sky, lights and post-process switch at once, the
        // baked indirect lighting blends over the given time.
        public void BlendTo(AmbiencePreset preset, float seconds)
        {
            if (preset == null) return;
            if (blend != null) StopCoroutine(blend);
            blend = StartCoroutine(BlendRoutine(preset, seconds));
        }

        private IEnumerator BlendRoutine(AmbiencePreset preset, float seconds)
        {
            ProbeReferenceVolume apv = ProbeReferenceVolume.instance;
            bool canBlend = apv != null && apv.currentBakingSet != null && seconds > 0f;
            for (float t = 0f; canBlend && t < seconds; t += Time.deltaTime)
            {
                apv.BlendLightingScenario(preset.Scenario, t / seconds);
                yield return null;
            }
            Apply(preset);
            blend = null;
        }

        // Every lamp of the level other than the main light and the
        // players' own (cameras, held objects).
        private IEnumerable<Light> DecorLights()
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light == mainLight || light.type == LightType.Directional) continue;
                if (light.GetComponentInParent<UnityEngine.InputSystem.PlayerInput>() != null) continue;
                yield return light;
            }
        }

        // The APV scenario baked for this ambience, when there is one.
        private static void SetScenario(string scenario)
        {
            ProbeReferenceVolume apv = ProbeReferenceVolume.instance;
            if (apv == null || apv.currentBakingSet == null) return;
            foreach (string s in apv.currentBakingSet.lightingScenarios)
                if (s == scenario)
                {
                    apv.lightingScenario = scenario;
                    return;
                }
        }
    }
}
