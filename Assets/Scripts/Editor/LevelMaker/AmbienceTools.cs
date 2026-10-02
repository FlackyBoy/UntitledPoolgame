#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Level Maker, ambiences (lot 5): Adaptive Probe Volumes with one baked
    // lighting scenario per ambience, so a room's mood can change while
    // playing. Lights stay real-time for their direct light; only indirect
    // light is baked, into the probes.
    public static class AmbienceTools
    {
        public const string AmbienceFolder = KitGenerator.KitsFolder + "/Ambiances";

        // ---------- Project ----------

        // URP assets used by the quality levels with APV and lighting
        // scenarios (+ blending) on.
        public static List<UniversalRenderPipelineAsset> RenderAssets()
        {
            var assets = new List<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main) assets.Add(main);
            for (int i = 0; i < QualitySettings.count; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q && !assets.Contains(q)) assets.Add(q);
            return assets;
        }

        public static bool ApvEnabled(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            return so.FindProperty("m_LightProbeSystem")?.intValue == 1
                   && so.FindProperty("m_SupportProbeVolumeScenarios")?.boolValue == true
                   && so.FindProperty("m_SupportProbeVolumeScenarioBlending")?.boolValue == true;
        }

        public static void EnableApv()
        {
            foreach (UniversalRenderPipelineAsset asset in RenderAssets())
            {
                var so = new SerializedObject(asset);
                SerializedProperty system = so.FindProperty("m_LightProbeSystem");
                if (system == null)
                {
                    Debug.LogWarning($"[Level Maker] {asset.name} : réglage APV introuvable (version d'URP ?), à activer à la main dans l'asset.", asset);
                    continue;
                }
                system.intValue = 1;   // Adaptive Probe Volumes
                so.FindProperty("m_SupportProbeVolumeScenarios").boolValue = true;
                so.FindProperty("m_SupportProbeVolumeScenarioBlending").boolValue = true;
                so.ApplyModifiedProperties();
                Debug.Log($"[Level Maker] APV et scénarios d'éclairage activés dans {asset.name}.", asset);
            }
            AssetDatabase.SaveAssets();
        }

        // Back to light probe groups, scenarios off (the state before lot 5).
        public static void DisableApv()
        {
            foreach (UniversalRenderPipelineAsset asset in RenderAssets())
            {
                var so = new SerializedObject(asset);
                SerializedProperty system = so.FindProperty("m_LightProbeSystem");
                if (system == null) continue;
                system.intValue = 0;   // Light Probe Groups (legacy)
                so.FindProperty("m_SupportProbeVolumeScenarios").boolValue = false;
                so.FindProperty("m_SupportProbeVolumeScenarioBlending").boolValue = false;
                so.ApplyModifiedProperties();
                Debug.Log($"[Level Maker] APV désactivé dans {asset.name}.", asset);
            }
            AssetDatabase.SaveAssets();
        }

        // Removes an ambience from the level and its lighting scenario (with
        // the scenario's baked data) from the scene's baking set.
        public static void RemoveAmbience(AmbienceController controller, AmbiencePreset preset)
        {
            Undo.RecordObject(controller, "Retirer l'ambiance");
            controller.presets.Remove(preset);
            if (controller.active == preset) controller.active = controller.presets.FirstOrDefault(p => p != null);
            EditorUtility.SetDirty(controller);

            ProbeVolumeBakingSet set = BakingSet();
            if (preset != null && set != null && set.lightingScenarios.Contains(preset.Scenario))
            {
                // A baking set keeps at least one scenario.
                if (set.lightingScenarios.Count > 1)
                {
                    set.RemoveScenario(preset.Scenario);
                    EditorUtility.SetDirty(set);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[Level Maker] Scénario « {preset.Scenario} » retiré du baking set {set.name}.", set);
                }
                else
                    Debug.Log($"[Level Maker] « {preset.Scenario} » est le dernier scénario du baking set : il reste (un baking set en garde toujours un).", set);
            }
            if (controller.active != null) ApplyInEditor(controller, controller.active);
        }

        // ---------- Scene ----------

        public static AmbienceController Controller => LevelBuilder.FindAll<AmbienceController>().FirstOrDefault();
        public static ProbeVolume GlobalProbeVolume => LevelBuilder.FindAll<ProbeVolume>().FirstOrDefault(v => v.mode == ProbeVolume.Mode.Global);

        public static ProbeVolumeBakingSet BakingSet()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) return null;
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            foreach (string setGuid in AssetDatabase.FindAssets("t:ProbeVolumeBakingSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(AssetDatabase.GUIDToAssetPath(setGuid));
                if (set != null && set.sceneGUIDs.Contains(guid)) return set;
            }
            return null;
        }

        // Everything the scene needs for ambiences: the controller (with the
        // main light and global volume), a global probe volume, lighting
        // settings that bake indirect light only, a baking set holding this
        // scene and one scenario per ambience, and the static renderers
        // taking their indirect light from the probes. Returns false (with a
        // message) when something must be done by hand.
        public static bool SetUpScene(LevelMakerSettings settings)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Level Maker", "Enregistre d'abord la scène : les données d'éclairage sont rangées à côté.", "OK");
                return false;
            }

            AmbienceController controller = Controller;
            if (controller == null)
            {
                var go = new GameObject("Ambiance");
                Undo.RegisterCreatedObjectUndo(go, "Ambiance");
                controller = go.AddComponent<AmbienceController>();
            }
            Undo.RecordObject(controller, "Ambiance");
            if (controller.mainLight == null)
                controller.mainLight = LevelBuilder.FindAll<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (controller.mainLight == null)
            {
                var sun = new GameObject("Directional Light");
                Undo.RegisterCreatedObjectUndo(sun, "Ambiance");
                controller.mainLight = sun.AddComponent<Light>();
                controller.mainLight.type = LightType.Directional;
            }
            if (controller.volume == null)
                controller.volume = LevelBuilder.FindAll<Volume>().FirstOrDefault(v => v.isGlobal);
            if (controller.volume == null)
            {
                var vgo = new GameObject("Global Volume");
                Undo.RegisterCreatedObjectUndo(vgo, "Ambiance");
                controller.volume = vgo.AddComponent<Volume>();
                controller.volume.isGlobal = true;
            }

            if (GlobalProbeVolume == null)
            {
                var pv = new GameObject("Adaptive Probe Volume");
                Undo.RegisterCreatedObjectUndo(pv, "Ambiance");
                pv.AddComponent<ProbeVolume>().mode = ProbeVolume.Mode.Global;
            }

            EnsureLightingSettings(scene);
            UseProbesForStaticRenderers();

            ProbeVolumeBakingSet set = BakingSet() ?? CreateBakingSet(scene);
            if (set == null) return false;
            foreach (AmbiencePreset preset in controller.presets.Where(p => p != null))
                set.TryAddScenario(preset.Scenario);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            ProbeReferenceVolume.instance.SetActiveBakingSet(set);

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static void EnsureLightingSettings(Scene scene)
        {
            if (Lightmapping.TryGetLightingSettings(out LightingSettings current) && current != null)
            {
                current.bakedGI = true;
                EditorUtility.SetDirty(current);
                return;
            }
            var lighting = new LightingSettings
            {
                name = scene.name + " Lighting",
                bakedGI = true,
                realtimeGI = false,
                mixedBakeMode = MixedLightingMode.IndirectOnly,
            };
            string path = Path.Combine(Path.GetDirectoryName(scene.path), scene.name + " Lighting.lighting").Replace('\\', '/');
            AssetDatabase.CreateAsset(lighting, AssetDatabase.GenerateUniqueAssetPath(path));
            Lightmapping.lightingSettings = lighting;
        }

        // Pieces placed by the Level Maker are static and contribute to GI;
        // with nothing baked into lightmaps they must take their indirect
        // light from the probes, or they get none.
        public static void UseProbesForStaticRenderers()
        {
            foreach (MeshRenderer renderer in LevelBuilder.FindAll<MeshRenderer>())
            {
                if (!GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI)) continue;
                if (renderer.receiveGI == ReceiveGI.LightProbes) continue;
                Undo.RecordObject(renderer, "Ambiance");
                renderer.receiveGI = ReceiveGI.LightProbes;
            }
        }

        // Same initialization as the Lighting window's (internal SetDefaults,
        // called by reflection), saved next to the scene.
        private static ProbeVolumeBakingSet CreateBakingSet(Scene scene)
        {
            var set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
            MethodInfo setDefaults = typeof(ProbeVolumeBakingSet).GetMethod("SetDefaults", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setDefaults == null)
            {
                Object.DestroyImmediate(set);
                EditorUtility.DisplayDialog("Level Maker",
                    "Impossible de créer le baking set automatiquement (version d'URP ?). Crée-le dans Window > Rendering > Lighting > " +
                    "Adaptive Probe Volumes (Baking Set), avec cette scène, puis relance « Préparer la scène ».", "OK");
                return null;
            }
            setDefaults.Invoke(set, null);
            set.name = scene.name + " Baking Set";
            set.TryAddScene(AssetDatabase.AssetPathToGUID(scene.path));

            string folder = Path.Combine(Path.GetDirectoryName(scene.path), scene.name).Replace('\\', '/');
            LevelBuilder.EnsureFolder(folder);
            AssetDatabase.CreateAsset(set, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + set.name + ".asset"));
            AssetDatabase.SaveAssets();
            typeof(ProbeVolumeBakingSet).GetMethod("SyncBakingSets", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
            return set;
        }

        // ---------- Applying ----------

        // The ambience in the editor, undoable for the scene objects it
        // touches; lights that feed the bake switched to Mixed (real-time
        // direct light, baked indirect light).
        public static void ApplyInEditor(AmbienceController controller, AmbiencePreset preset)
        {
            if (controller == null || preset == null) return;
            Undo.RecordObject(controller, "Ambiance");
            if (controller.mainLight != null)
            {
                Undo.RecordObject(controller.mainLight, "Ambiance");
                Undo.RecordObject(controller.mainLight.transform, "Ambiance");
            }
            if (controller.volume != null) Undo.RecordObject(controller.volume, "Ambiance");
            foreach (Light light in LevelBuilder.FindAll<Light>())
            {
                Undo.RecordObject(light, "Ambiance");
                if (light.lightmapBakeType == LightmapBakeType.Realtime) light.lightmapBakeType = LightmapBakeType.Mixed;
            }
            controller.Apply(preset);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ---------- Baking ----------

        private static readonly Queue<AmbiencePreset> bakeQueue = new Queue<AmbiencePreset>();
        private static AmbiencePreset baking, restoreAfter;
        private static AmbienceController bakeController;

        public static bool IsBaking => baking != null || bakeQueue.Count > 0;

        // Each ambience in turn: applied, made the active scenario, its probes
        // baked; then the ambience that was on is put back.
        public static void Bake(AmbienceController controller, IEnumerable<AmbiencePreset> presets, LevelMakerSettings settings)
        {
            if (IsBaking || !SetUpScene(settings)) return;
            bakeQueue.Clear();
            foreach (AmbiencePreset preset in presets.Where(p => p != null)) bakeQueue.Enqueue(preset);
            if (bakeQueue.Count == 0) return;
            bakeController = controller;
            restoreAfter = controller.active;
            EditorApplication.update -= BakeStep;
            EditorApplication.update += BakeStep;
        }

        private static void BakeStep()
        {
            if (AdaptiveProbeVolumes.isRunning || Lightmapping.isRunning) return;
            if (baking != null)
            {
                Debug.Log($"[Level Maker] Ambiance « {baking.name} » précalculée (scénario {baking.Scenario}).", baking);
                baking = null;
            }
            if (bakeQueue.Count == 0)
            {
                EditorApplication.update -= BakeStep;
                if (bakeController != null && restoreAfter != null) ApplyInEditor(bakeController, restoreAfter);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Debug.Log("[Level Maker] Précalcul des ambiances terminé.");
                return;
            }

            baking = bakeQueue.Dequeue();
            ApplyInEditor(bakeController, baking);
            ProbeVolumeBakingSet set = BakingSet();
            if (set == null)
            {
                Debug.LogError("[Level Maker] Pas de baking set pour cette scène : relance « Préparer la scène ».");
                CancelBake();
                return;
            }
            set.TryAddScenario(baking.Scenario);
            ProbeReferenceVolume.instance.SetActiveBakingSet(set);
            ProbeReferenceVolume.instance.lightingScenario = baking.Scenario;
            if (!AdaptiveProbeVolumes.BakeAsync())
            {
                Debug.LogError($"[Level Maker] Le précalcul de « {baking.name} » n'a pas pu démarrer (APV activé ? volume de sondes ? scène enregistrée ?).", baking);
                bakeQueue.Clear();
                baking = null;
                EditorApplication.update -= BakeStep;
            }
        }

        public static void CancelBake()
        {
            bakeQueue.Clear();
            if (AdaptiveProbeVolumes.isRunning) AdaptiveProbeVolumes.Cancel();
            baking = null;
            EditorApplication.update -= BakeStep;
        }

        public static bool IsBaked(AmbiencePreset preset)
        {
            ProbeVolumeBakingSet set = BakingSet();
            return set != null && preset != null && set.lightingScenarios.Contains(preset.Scenario) && set.HasBakedData(preset.Scenario);
        }

        // ---------- Starting presets ----------

        // Bar néon, Prison sombre, Jour neutre, each with its post-process
        // profile, in Assets/LevelKits/Ambiances (existing ones are kept).
        public static List<AmbiencePreset> CreateDefaultPresets()
        {
            LevelBuilder.EnsureFolder(AmbienceFolder);
            var list = new List<AmbiencePreset>
            {
                Preset("Bar néon", p =>
                {
                    p.ambientMode = AmbientMode.Trilight;
                    p.ambientSky = new Color(0.16f, 0.08f, 0.24f);
                    p.ambientEquator = new Color(0.10f, 0.05f, 0.16f);
                    p.ambientGround = new Color(0.04f, 0.02f, 0.06f);
                    p.reflectionIntensity = 0.6f;
                    p.mainLightColor = new Color(0.55f, 0.6f, 1f);
                    p.mainLightIntensity = 0.25f;
                    p.mainLightRotation = new Vector3(40f, -30f, 0f);
                    p.fog = true;
                    p.fogColor = new Color(0.12f, 0.05f, 0.18f);
                    p.fogMode = FogMode.ExponentialSquared;
                    p.fogDensity = 0.03f;
                    p.decorTint = new Color(1f, 0.3f, 0.85f);
                    p.decorTintStrength = 0.6f;
                    p.decorIntensity = 1.5f;
                }, profile =>
                {
                    AddBloom(profile, 1.6f, 0.85f);
                    AddColorAdjust(profile, 0.3f, 15f, 30f);
                    AddVignette(profile, 0.35f);
                    AddTonemap(profile, TonemappingMode.ACES);
                }),
                Preset("Prison sombre", p =>
                {
                    p.ambientMode = AmbientMode.Trilight;
                    p.ambientSky = new Color(0.14f, 0.16f, 0.19f);
                    p.ambientEquator = new Color(0.10f, 0.11f, 0.13f);
                    p.ambientGround = new Color(0.05f, 0.05f, 0.06f);
                    p.reflectionIntensity = 0.5f;
                    p.mainLightColor = new Color(0.85f, 0.92f, 1f);
                    p.mainLightIntensity = 0.8f;
                    p.mainLightRotation = new Vector3(60f, 20f, 0f);
                    p.mainLightShadows = LightShadows.Hard;
                    p.fog = true;
                    p.fogColor = new Color(0.2f, 0.22f, 0.25f);
                    p.fogMode = FogMode.Exponential;
                    p.fogDensity = 0.035f;
                    p.decorTint = new Color(0.8f, 0.9f, 1f);
                    p.decorTintStrength = 0.4f;
                    p.decorIntensity = 0.8f;
                }, profile =>
                {
                    AddBloom(profile, 0.3f, 1f);
                    AddColorAdjust(profile, -0.2f, 25f, -50f);
                    AddVignette(profile, 0.45f);
                    AddTonemap(profile, TonemappingMode.ACES);
                }),
                Preset("Jour neutre", p =>
                {
                    p.skybox = RenderSettings.skybox;
                    p.ambientMode = p.skybox != null ? AmbientMode.Skybox : AmbientMode.Trilight;
                    p.ambientSky = new Color(0.55f, 0.6f, 0.7f);
                    p.ambientEquator = new Color(0.45f, 0.45f, 0.45f);
                    p.ambientGround = new Color(0.25f, 0.23f, 0.2f);
                    p.mainLightColor = new Color(1f, 0.96f, 0.88f);
                    p.mainLightIntensity = 1.3f;
                    p.mainLightRotation = new Vector3(50f, -30f, 0f);
                    p.fog = false;
                    p.decorTintStrength = 0f;
                    p.decorIntensity = 1f;
                }, profile =>
                {
                    AddBloom(profile, 0.2f, 1f);
                    AddTonemap(profile, TonemappingMode.Neutral);
                }),
            };
            AssetDatabase.SaveAssets();
            return list;
        }

        private static AmbiencePreset Preset(string name, System.Action<AmbiencePreset> setup, System.Action<VolumeProfile> post)
        {
            string path = $"{AmbienceFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AmbiencePreset>(path);
            if (existing != null) return existing;

            var preset = ScriptableObject.CreateInstance<AmbiencePreset>();
            setup(preset);
            preset.scenarioName = name;
            AssetDatabase.CreateAsset(preset, path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, $"{AmbienceFolder}/{name} Volume.asset");
            post(profile);
            foreach (VolumeComponent component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            preset.volumeProfile = profile;
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static void AddBloom(VolumeProfile profile, float intensity, float threshold)
        {
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(intensity);
            bloom.threshold.Override(threshold);
        }

        private static void AddColorAdjust(VolumeProfile profile, float exposure, float contrast, float saturation)
        {
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
        }

        private static void AddVignette(VolumeProfile profile, float intensity)
        {
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(intensity);
        }

        private static void AddTonemap(VolumeProfile profile, TonemappingMode mode)
        {
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(mode);
        }
    }
}
#endif
