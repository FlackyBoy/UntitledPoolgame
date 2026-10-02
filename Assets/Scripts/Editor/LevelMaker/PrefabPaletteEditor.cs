#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Palette inspector (also shown inside the Level Maker): one drop zone
    // per kind of piece, taking several prefabs at once — or whole folders,
    // every prefab inside — instead of filling the lists one by one.
    [CustomEditor(typeof(PrefabPalette))]
    public class PrefabPaletteEditor : Editor
    {
        private static readonly (string property, string label)[] Zones =
        {
            ("walls", "Murs"), ("doors", "Portes"), ("windows", "Fenêtres"),
            ("pillars", "Poteaux"), ("floorTiles", "Sols"), ("ceilingTiles", "Plafonds"),
            ("stairs", "Escaliers"), ("railings", "Garde-corps"), ("props", "Props"),
        };

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Glisser des prefabs ou des dossiers (plusieurs à la fois) sur une case :", EditorStyles.miniBoldLabel);
            const int columns = 3;
            for (int i = 0; i < Zones.Length; i += columns)
                using (new EditorGUILayout.HorizontalScope())
                    for (int j = i; j < i + columns && j < Zones.Length; j++)
                        DropZone(Zones[j].property, Zones[j].label);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void DropZone(string propertyName, string label)
        {
            SerializedProperty list = serializedObject.FindProperty(propertyName);
            Rect rect = GUILayoutUtility.GetRect(60f, 34f, GUILayout.ExpandWidth(true));
            Event e = Event.current;
            bool hovering = rect.Contains(e.mousePosition) && DragAndDrop.objectReferences.Length > 0;
            Color old = GUI.backgroundColor;
            if (hovering) GUI.backgroundColor = new Color(0.4f, 1f, 0.6f);
            GUI.Box(rect, $"{label}\n{list.arraySize}", EditorStyles.helpBox);
            GUI.backgroundColor = old;

            if (!rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DraggedPrefabs().Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var palette = (PrefabPalette)target;
                List<GameObject> prefabs = DraggedPrefabs();
                Undo.RecordObject(palette, "Ajouter à la palette");
                serializedObject.Update();
                var existing = new HashSet<Object>();
                for (int k = 0; k < list.arraySize; k++) existing.Add(list.GetArrayElementAtIndex(k).objectReferenceValue);
                int added = 0;
                foreach (GameObject prefab in prefabs)
                {
                    if (existing.Contains(prefab)) continue;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = prefab;
                    existing.Add(prefab);
                    added++;
                }
                serializedObject.ApplyModifiedProperties();
                Debug.Log($"[Level Maker] Dépôt sur « {label} » : reçu {DragAndDrop.objectReferences.Length} objet(s) et " +
                          $"{DragAndDrop.paths.Length} chemin(s), {prefabs.Count} prefab(s) reconnu(s), {added} ajouté(s) " +
                          $"(déjà présents : {prefabs.Count - added}).", palette);
                e.Use();
                GUIUtility.ExitGUI();
            }
        }

        // The prefabs being dragged, read both from the dragged objects and
        // from their paths (depending on where a multi-selection is dragged
        // from, one of the two can hold only the first item): prefab assets,
        // and every prefab in a dragged folder (subfolders included).
        private static List<GameObject> DraggedPrefabs()
        {
            var paths = new List<string>(DragAndDrop.paths);
            foreach (Object o in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path);
            }

            var prefabs = new List<GameObject>();
            void Take(GameObject prefab)
            {
                if (prefab != null && !prefabs.Contains(prefab)) prefabs.Add(prefab);
            }
            foreach (string path in paths)
            {
                if (AssetDatabase.IsValidFolder(path))
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                        Take(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
                else if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    Take(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            return prefabs;
        }
    }
}
#endif
