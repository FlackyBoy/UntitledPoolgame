#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Bakes the open level's lighting for the ambience of its type
    // (GameFlowSettings > Levels > Ambience): bounce light into Adaptive
    // Probe Volumes, through the Level Maker's ambience bake, then the
    // reflection probes, so shiny balls and varnish reflect the room instead
    // of the default sky. Adds a room-sized reflection probe if there is none.
    public static class LevelLightingTools
    {
        [MenuItem("Tools/Pool/Précalculer l'éclairage du niveau ouvert", priority = 23)]
        public static void BakeOpenLevel()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Éclairage", "Enregistre d'abord la scène : les données d'éclairage sont rangées à côté.", "OK");
                return;
            }
            if (AmbienceTools.IsBaking || Lightmapping.isRunning)
            {
                EditorUtility.DisplayDialog("Éclairage", "Un précalcul est déjà en cours.", "OK");
                return;
            }

            LevelEntry level = GameFlowSettings.Instance.levels.FirstOrDefault(l => l != null && l.sceneName == scene.name);
            if (level == null || level.ambience == null)
            {
                EditorUtility.DisplayDialog("Éclairage",
                    $"La scène « {scene.name} » n'a pas d'ambiance de niveau : choisis-en une dans GameFlowSettings > Levels > Ambience (et ajoute le niveau à la liste s'il n'y est pas).", "OK");
                return;
            }
            AmbiencePreset preset = level.ambience;

            if (!AmbienceTools.ApvEnabled(GraphicsSettings.defaultRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))
                AmbienceTools.EnableApv();

            // The level's ambience becomes the scene's: what is baked is what
            // LevelAmbience puts on in play.
            AmbienceController controller = AmbienceTools.Controller;
            if (controller == null)
            {
                var go = new GameObject("Ambiance");
                Undo.RegisterCreatedObjectUndo(go, "Ambiance");
                controller = go.AddComponent<AmbienceController>();
            }
            Undo.RecordObject(controller, "Ambiance");
            if (!controller.presets.Contains(preset)) controller.presets.Add(preset);
            controller.active = preset;
            EditorUtility.SetDirty(controller);

            EnsureReflectionProbe();

            AmbienceTools.BakeFinished -= BakeReflections;
            AmbienceTools.BakeFinished += BakeReflections;
            AmbienceTools.Bake(controller, new[] { preset }, null);
            if (!AmbienceTools.IsBaking)
            {
                AmbienceTools.BakeFinished -= BakeReflections;
                Debug.LogError("[Éclairage] Le précalcul n'a pas démarré : voir les messages au-dessus.");
                return;
            }
            Debug.Log($"[Éclairage] Précalcul de « {scene.name} » avec l'ambiance « {preset.name} » : sondes de lumière, puis reflets. Laisser Unity travailler (barre en bas à droite).");
        }

        // Each probe baked to a cubemap next to the scene, then used as its
        // custom texture: kept as is by later bakes, and no lighting data
        // asset to lose.
        private static void BakeReflections()
        {
            AmbienceTools.BakeFinished -= BakeReflections;
            Scene scene = SceneManager.GetActiveScene();
            string folder = System.IO.Path.GetDirectoryName(scene.path).Replace("\\", "/") + "/" + scene.name;
            LevelBuilder.EnsureFolder(folder);

            int baked = 0, failed = 0;
            foreach (ReflectionProbe probe in LevelBuilder.FindAll<ReflectionProbe>())
            {
                if (probe.mode == ReflectionProbeMode.Realtime) continue;
                string path = $"{folder}/Reflet {probe.name}.exr";
                if (!Lightmapping.BakeReflectionProbe(probe, path)) { failed++; continue; }
                AssetDatabase.ImportAsset(path);
                Undo.RecordObject(probe, "Reflets");
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                EditorUtility.SetDirty(probe);
                baked++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (failed == 0) Debug.Log($"[Éclairage] {baked} sonde(s) de reflet précalculée(s) dans {folder} : éclairage du niveau terminé.");
            else Debug.LogError($"[Éclairage] {failed} sonde(s) de reflet n'ont pas pu être précalculées ({baked} réussie(s)).");
        }

        // One baked, box-projected probe around everything static, when the
        // scene has none: enough for a single room; rooms of their own can
        // get theirs by hand (it blends between probes).
        private static void EnsureReflectionProbe()
        {
            if (LevelBuilder.FindAll<ReflectionProbe>().Count > 0) return;

            Bounds bounds = default;
            bool any = false;
            foreach (MeshRenderer renderer in LevelBuilder.FindAll<MeshRenderer>())
            {
                if (!GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ReflectionProbeStatic)) continue;
                if (any) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; any = true; }
            }
            if (!any)
            {
                Debug.LogWarning("[Éclairage] Aucun objet « Reflection Probe Static » : pas de sonde de reflet ajoutée (cocher Static sur le décor).");
                return;
            }

            var go = new GameObject("Reflection Probe (niveau)");
            Undo.RegisterCreatedObjectUndo(go, "Sonde de reflet");
            // Eye height near the middle of the room: the reflection looks right
            // from where players stand.
            go.transform.position = new Vector3(bounds.center.x, bounds.min.y + 1.5f, bounds.center.z);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.size = bounds.size + Vector3.one * 0.5f;
            probe.center = bounds.center - go.transform.position;
            Debug.Log($"[Éclairage] Sonde de reflet ajoutée ({bounds.size.x:0.#} × {bounds.size.y:0.#} × {bounds.size.z:0.#} m).", go);
        }
    }
}
#endif
