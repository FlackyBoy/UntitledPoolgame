#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Menu → level flow, editor side: the settings assets (levels, loading
    // screen texts, menu texts and look) and the main menu scene, first in the build list with
    // the level scenes after it (LoadSceneAsync only finds scenes that are
    // in the build).
    public static class GameFlowTools
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string PlayerActionsPath = "Assets/InputManagers/InputSystem_Actions_Local.inputactions";

        // Called by Tools > Pool > Ensure Config Assets Exist.
        public static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            const string flowPath = "Assets/Resources/GameFlowSettings.asset";
            GameFlowSettings flow = AssetDatabase.LoadAssetAtPath<GameFlowSettings>(flowPath);
            if (flow == null)
            {
                flow = ScriptableObject.CreateInstance<GameFlowSettings>();
                flow.playerActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerActionsPath);
                AssetDatabase.CreateAsset(flow, flowPath);
                Debug.Log($"[GameFlow] Created {flowPath} (levels, menu scene, player actions).");
            }
            else if (flow.playerActions == null)
            {
                flow.playerActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerActionsPath);
                EditorUtility.SetDirty(flow);
            }

            const string loadingPath = "Assets/Resources/LoadingScreenSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<LoadingScreenSettings>(loadingPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LoadingScreenSettings>(), loadingPath);
                Debug.Log($"[GameFlow] Created {loadingPath} (loading screen texts, tips, messages).");
            }

            const string menuPath = "Assets/Resources/MenuSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<MenuSettings>(menuPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MenuSettings>(), menuPath);
                Debug.Log($"[GameFlow] Created {menuPath} (main menu, pause and key bindings texts, palette, fonts, background).");
            }

            const string textFxPath = "Assets/Resources/TextFxSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<TextFxSettings>(textFxPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TextFxSettings>(), textFxPath);
                Debug.Log($"[GameFlow] Created {textFxPath} (animated text: effects, entrances, typewriter).");
            }

            // One file per menu screen, with the built-in layout (Menu Studio).
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Menus")) AssetDatabase.CreateFolder("Assets/Resources", "Menus");
            foreach (string id in MenuLayouts.All)
            {
                string path = $"Assets/Resources/Menus/{id}.asset";
                if (AssetDatabase.LoadAssetAtPath<MenuScreen>(path) != null) continue;
                AssetDatabase.CreateAsset(MenuDefaults.Create(id), path);
                Debug.Log($"[GameFlow] Created {path} (menu screen, edit it in Tools > Pool > Menu Studio).");
            }

            const string feelPath = "Assets/Resources/GameFeelSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<GameFeelSettings>(feelPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameFeelSettings>(), feelPath);
                Debug.Log($"[GameFlow] Created {feelPath} (Feel sequences per match moment).");
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Pool/Create Main Menu Scene")]
        public static void CreateMainMenuScene()
        {
            EnsureAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null)
            {
                // Created next to the open scene and closed again: the scene
                // being worked on stays as it is.
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var camera = new GameObject("Camera");
                Camera cam = camera.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                SceneManager.MoveGameObjectToScene(camera, scene);
                var menu = new GameObject("Main Menu");
                menu.AddComponent<MainMenuScene>();
                SceneManager.MoveGameObjectToScene(menu, scene);
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                EditorSceneManager.SaveScene(scene, MenuScenePath);
                EditorSceneManager.CloseScene(scene, true);
                Debug.Log($"[GameFlow] Created {MenuScenePath}.");
            }
            SyncBuildScenes();
        }

        [MenuItem("Tools/Pool/Sync Build Scenes (menu + levels)")]
        public static void SyncBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            // The menu first: it's what the built game opens on.
            scenes.RemoveAll(s => s.path == MenuScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null)
                scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));

            GameFlowSettings flow = AssetDatabase.LoadAssetAtPath<GameFlowSettings>("Assets/Resources/GameFlowSettings.asset");
            var missing = new List<string>();
            if (flow != null)
                foreach (LevelEntry level in flow.levels)
                {
                    if (level.comingSoon || string.IsNullOrEmpty(level.sceneName)) continue;
                    string path = FindScene(level.sceneName);
                    if (path == null) { missing.Add(level.sceneName); continue; }
                    // LoadSceneAsync goes by name: another scene of the same
                    // name in the list (a _Recovery copy…) could be loaded instead.
                    scenes.RemoveAll(s => s.path != path && System.IO.Path.GetFileNameWithoutExtension(s.path) == level.sceneName);
                    int index = scenes.FindIndex(s => s.path == path);
                    if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                    else if (!scenes[index].enabled) scenes[index] = new EditorBuildSettingsScene(path, true);
                }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[GameFlow] Build scenes: " + string.Join(", ", scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))) +
                      (missing.Count > 0 ? $" — introuvables : {string.Join(", ", missing)} (vérifie le nom de scène dans GameFlowSettings)" : ""));
        }

        // The project's scene with that name: Unity's _Recovery copies are
        // ignored, and Assets/Scenes wins over any other folder.
        public static string FindScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            string found = null;
            foreach (string guid in AssetDatabase.FindAssets(sceneName + " t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != sceneName || path.Contains("/_Recovery/")) continue;
                if (path.StartsWith("Assets/Scenes/")) return path;
                found ??= path;
            }
            return found;
        }
    }
}
#endif
