#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Scene picker for [SceneName] string fields (GameFlowSettings levels):
    // an object field that only takes scenes, storing the scene's file name.
    // Under it, a warning with a fix button when the scene is not in the
    // build list (LoadSceneAsync would not find it) or no longer exists.
    [CustomPropertyDrawer(typeof(SceneNameAttribute))]
    public class SceneNameDrawer : PropertyDrawer
    {
        private const float WarningHeight = 38f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (property.propertyType == SerializedPropertyType.String && Problem(property.stringValue, out _) != null)
                h += WarningHeight + 2f;
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string path = FindScenePath(property.stringValue);
            SceneAsset current = path != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(path) : null;

            EditorGUI.BeginProperty(line, label, property);
            EditorGUI.BeginChangeCheck();
            var picked = (SceneAsset)EditorGUI.ObjectField(line, label, current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = picked != null ? picked.name : "";
            EditorGUI.EndProperty();

            string problem = Problem(property.stringValue, out bool canFix);
            if (problem == null) return;
            Rect box = new Rect(position.x, line.yMax + 2f, position.width - (canFix ? 110f : 0f), WarningHeight);
            EditorGUI.HelpBox(box, problem, MessageType.Warning);
            if (canFix && GUI.Button(new Rect(box.xMax + 4f, box.y + 8f, 106f, 22f), "Ajouter au build"))
                AddToBuild(FindScenePath(property.stringValue));
        }

        // Null when fine. canFix: the scene exists but isn't in the build.
        private static string Problem(string sceneName, out bool canFix)
        {
            canFix = false;
            if (string.IsNullOrEmpty(sceneName)) return null;
            string path = FindScenePath(sceneName);
            if (path == null) return $"Scène « {sceneName} » introuvable dans le projet.";
            if (EditorBuildSettings.scenes.Any(s => s.path == path && s.enabled)) return null;
            canFix = true;
            return "Pas dans la liste des scènes du build : le chargement échouera.";
        }

        private static string FindScenePath(string sceneName) => GameFlowTools.FindScene(sceneName);

        private static void AddToBuild(string path)
        {
            if (path == null) return;
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
