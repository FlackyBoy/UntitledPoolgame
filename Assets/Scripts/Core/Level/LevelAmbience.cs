using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UntitledPoolGame.Core
{
    // Gives every level the ambience of its type without touching its scene:
    // the level's entry in GameFlowSettings names an AmbiencePreset (Bar
    // néon, Labo R&D…), shared by all the levels of that type, and this
    // applies it when the scene loads, from the menu or straight from the
    // editor. A scene with its own AmbienceController (baked lighting) gets
    // the ambience as its active one; leave Ambience empty for a Level Maker
    // level that picks its moods itself.
    public static class LevelAmbience
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // The scene already open when Play starts has loaded before this ran.
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The first scene may come both from Init and from the event.
            if (mode != LoadSceneMode.Single || scene == lastScene) return;
            lastScene = scene;

            if (!TryFindLevel(scene.name, out LevelEntry level))
            {
                Debug.Log($"[Ambiance] Scène « {scene.name} » absente des niveaux de GameFlowSettings : pas d'ambiance de niveau.");
                return;
            }
            AmbiencePreset preset = level.ambience;
            if (preset == null)
            {
                Debug.Log($"[Ambiance] Niveau « {level.displayName} » sans ambiance (GameFlowSettings > Levels > Ambience) : scène telle quelle.");
                return;
            }
            // A scene set up for baking (Level Maker, Précalculer l'éclairage)
            // has its controller: the level's ambience becomes its active one,
            // applied in its Start, with the lighting scenario baked for it.
            AmbienceController existing = Object.FindAnyObjectByType<AmbienceController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!existing.presets.Contains(preset)) existing.presets.Add(preset);
                existing.active = preset;
                Debug.Log($"[Ambiance] « {preset.name} » appliquée à « {scene.name} » par son AmbienceController ({existing.name}).", existing);
                return;
            }

            var root = new GameObject("Ambiance du niveau");
            SceneManager.MoveGameObjectToScene(root, scene);

            // Under the AmbienceController's Start, so it is filled first.
            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            AmbienceController controller = root.AddComponent<AmbienceController>();
            controller.presets.Add(preset);
            controller.active = preset;
            controller.volume = volume;
            controller.mainLight = MainLight();
            Debug.Log($"[Ambiance] « {preset.name} » appliquée à « {scene.name} » (objet « Ambiance du niveau », lumière principale : {(controller.mainLight != null ? controller.mainLight.name : "aucune")}).", root);
        }

        private static Scene lastScene;

        private static bool TryFindLevel(string sceneName, out LevelEntry found)
        {
            foreach (LevelEntry level in GameFlowSettings.Instance.levels)
                if (level != null && level.sceneName == sceneName) { found = level; return true; }
            found = null;
            return false;
        }

        // The sun set in the Lighting window, or else the brightest
        // directional light of the scene.
        private static Light MainLight()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            Light best = null;
            foreach (Light light in Object.FindObjectsByType<Light>())
                if (light.type == LightType.Directional && (best == null || light.intensity > best.intensity)) best = light;
            return best;
        }
    }
}
