using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Core
{
    // The air of an ambience: dust floating in the whole room and soft haze
    // drifting near the floor, two world-space particle systems sized to the
    // level once, shared by both split-screen cameras. Created and updated by
    // AmbienceController.Apply, in play only (which re-applies on every
    // Inspector tweak of the ambience in use).
    public class AmbienceParticles : MonoBehaviour
    {
        // Particles per cubic metre (dust) and per square metre (haze) at
        // intensity 1, and the caps that keep a huge level affordable.
        private const float DustPerCubicMetre = 0.35f;
        private const float HazePerSquareMetre = 0.06f;
        private const int MaxDust = 3000;
        private const int MaxHaze = 300;
        private const float MaxAreaSide = 80f;

        private AmbiencePreset preset;
        private ParticleSystem dust, haze;
        private Bounds area;
        private int shownSignature;

        public static AmbienceParticles For(AmbienceController owner)
        {
            AmbienceParticles particles = owner.GetComponentInChildren<AmbienceParticles>();
            if (particles != null) return particles;
            var go = new GameObject("Particules d'ambiance");
            go.transform.SetParent(owner.transform, false);
            particles = go.AddComponent<AmbienceParticles>();
            particles.area = LevelArea();
            return particles;
        }

        // Rebuilt only when the particle settings changed: re-applying the
        // ambience for a fog or light tweak keeps the air as it is.
        public void Show(AmbiencePreset newPreset)
        {
            int signature = Signature(newPreset);
            if (newPreset == preset && signature == shownSignature) return;
            preset = newPreset;
            shownSignature = signature;
            ConfigureDust();
            ConfigureHaze();
        }

        private void ConfigureDust()
        {
            float volume = area.size.x * area.size.y * area.size.z;
            int count = Mathf.Min(MaxDust, Mathf.RoundToInt(volume * DustPerCubicMetre * preset.dustIntensity));
            if (!Prepare(ref dust, "Poussière", count, preset.dustMaterial)) return;

            ParticleSystem.MainModule main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(preset.dustSize * 0.5f, preset.dustSize * 1.5f);
            // More dust also means each grain shows a little more.
            Color color = preset.dustColor;
            color.a = Mathf.Clamp01(color.a * Mathf.Sqrt(preset.dustIntensity));
            main.startColor = color;

            dust.transform.position = area.center;
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.scale = area.size;

            ParticleSystem.NoiseModule noise = dust.noise;
            noise.enabled = preset.dustDrift > 0f;
            noise.strength = preset.dustDrift;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;

            Finish(dust, count, 14f);
        }

        private void ConfigureHaze()
        {
            float floor = area.size.x * area.size.z;
            int count = Mathf.Min(MaxHaze, Mathf.RoundToInt(floor * HazePerSquareMetre * preset.hazeIntensity));
            if (!Prepare(ref haze, "Brume", count, preset.hazeMaterial)) return;

            ParticleSystem.MainModule main = haze.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Color color = preset.hazeColor;
            color.a = Mathf.Clamp01(color.a * preset.hazeIntensity);
            main.startColor = color;

            // A sheet from the floor up to Haze Height.
            float height = preset.hazeHeight;
            haze.transform.position = new Vector3(area.center.x, area.min.y + height * 0.5f, area.center.z);
            ParticleSystem.ShapeModule shape = haze.shape;
            shape.scale = new Vector3(area.size.x, height, area.size.z);

            ParticleSystem.RotationOverLifetimeModule spin = haze.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            ParticleSystem.NoiseModule noise = haze.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.08f;
            noise.scrollSpeed = 0.05f;

            Finish(haze, count, 21f);
        }

        // Creates the system on first use (stopped: some settings refuse a
        // playing system), or hides it when there is nothing to show.
        private bool Prepare(ref ParticleSystem system, string name, int count, Material material)
        {
            if (count <= 0 || material == null)
            {
                if (system != null) system.gameObject.SetActive(false);
                if (count > 0) Debug.LogWarning($"[Ambiance] « {preset.name} » : pas de matériau pour {name} (lancer Tools > Pool > Ensure Config Assets Exist).", preset);
                return false;
            }
            if (system == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                system = go.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.prewarm = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;

                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;

                // Fade in and out, so nothing pops.
                ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;

                var renderer = system.GetComponent<ParticleSystemRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            system.gameObject.SetActive(true);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return true;
        }

        // Steady count: as many born per second as die.
        private static void Finish(ParticleSystem system, int count, float meanLifetime)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = count;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = count / meanLifetime;
            system.Play(true);
        }

        private static int Signature(AmbiencePreset p) => System.HashCode.Combine(
            System.HashCode.Combine(p.dustIntensity, p.dustColor, p.dustSize, p.dustDrift, p.dustMaterial),
            System.HashCode.Combine(p.hazeIntensity, p.hazeColor, p.hazeHeight, p.hazeMaterial));

        // The room: every mesh of the scene except the players' (spawned
        // later anyway) and giant ones (sky domes), capped around its middle.
        private static Bounds LevelArea()
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
            bool any = false;
            foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>())
            {
                Bounds b = renderer.bounds;
                if (b.size.x > 200f || b.size.z > 200f) continue;
                if (renderer.GetComponentInParent<PlayerInput>() != null) continue;
                if (any) bounds.Encapsulate(b);
                else { bounds = b; any = true; }
            }
            Vector3 size = bounds.size;
            size.x = Mathf.Min(size.x, MaxAreaSide);
            size.y = Mathf.Clamp(size.y, 2f, 20f);
            size.z = Mathf.Min(size.z, MaxAreaSide);
            return new Bounds(bounds.center, size);
        }
    }
}
