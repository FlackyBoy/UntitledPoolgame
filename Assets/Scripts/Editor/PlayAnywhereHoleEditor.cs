#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // Play Anywhere hole selected: the course is drawn and edited in the
    // Scene view.
    // - Spheres: the tees (blue, red), the path points (green) and the cup
    //   (yellow). Dragged, they land on whatever collider is under the mouse
    //   (floor, counter, shelf), so the course takes its height from the
    //   décor. The flag follows the cup.
    // - "+" between two points inserts one there; the red cube over a point
    //   removes it.
    // - Dots on the corridor's side set its width there (at the tees and
    //   the cup: Path Width).
    // Everything is undoable. The direction signs that follow from the course
    // are previewed in orange (PlayAnywhereCourseSigns); a button makes them
    // real objects of the scene, to move and size by hand (PlayAnywhereSignEditor).
    [CustomEditor(typeof(PlayAnywhereHole))]
    public class PlayAnywhereHoleEditor : Editor
    {
        private static readonly Color PathColour = new Color(0.3f, 1f, 0.4f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var hole = (PlayAnywhereHole)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Parcours : le couloir vert dans la Scene. Une bille qui en sort, ou qui tombe plus bas que lui, est hors limites.\n" +
                "• Tire les sphères (départs, points, trou) : elles se posent sur la surface sous la souris (sol, comptoir…).\n" +
                "• « + » entre deux points en ajoute un ; le cube rouge au-dessus d'un point le retire.\n" +
                "• Les points sur le bord du couloir règlent sa largeur.\n" +
                "• Les panneaux suivent le parcours : réglages Signs de PlayAnywhereSettings.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ajouter un point avant le trou"))
                {
                    hole.GetNode(hole.NodeCount - 2, out Vector3 a, out float widthA);
                    hole.GetNode(hole.NodeCount - 1, out Vector3 b, out _);
                    Undo.RecordObject(hole, "Ajouter un point au parcours");
                    hole.InsertPoint(hole.PointCount, (a + b) * 0.5f, widthA);
                    SceneView.RepaintAll();
                }
                using (new EditorGUI.DisabledScope(hole.PointCount == 0))
                    if (GUILayout.Button("Retirer le dernier point"))
                    {
                        Undo.RecordObject(hole, "Retirer un point du parcours");
                        hole.RemovePoint(hole.PointCount - 1);
                        SceneView.RepaintAll();
                    }
            }
            if (hole.CourseSigns) SignsGUI(hole);
        }

        private void SignsGUI(PlayAnywhereHole hole)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Panneaux", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                Transform placed = hole.PlacedSigns;
                if (placed == null)
                {
                    EditorGUILayout.HelpBox("Automatiques : l'aperçu orange montre où ils seront en jeu, et ils suivent le parcours. Pour les déplacer ou changer leur taille, place-les dans la scène.", MessageType.None);
                    if (GUILayout.Button("Placer les panneaux dans la scène")) PlaceSigns(hole);
                    return;
                }

                PlayAnywhereSign[] signs = placed.GetComponentsInChildren<PlayAnywhereSign>(true);
                int edited = System.Array.FindAll(signs, s => s.IsHandEdited).Length;
                EditorGUILayout.HelpBox($"{signs.Length} panneau(x) dans la scène, dont {edited} modifié(s) à la main. Clique sur l'un d'eux et utilise les outils Déplacer, Tourner et Échelle ; ils ne suivent plus le parcours tout seuls.", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Recalculer (garde les modifiés)")) PlaceSigns(hole);
                    if (GUILayout.Button("Revenir à l'automatique"))
                    {
                        Undo.DestroyObjectImmediate(placed.gameObject);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        // Makes real objects of the signs: those edited by hand stay and keep
        // their spot, the others are replaced by what the course asks for now.
        private static void PlaceSigns(PlayAnywhereHole hole)
        {
            var settings = PoolSettingsLoader.LoadOrDefault<PlayAnywhereSettings>("PlayAnywhereSettings");
            if (settings.signPrefab == null)
            {
                Debug.LogWarning("[PlayAnywhere] Pas de Sign Prefab dans PlayAnywhereSettings : lance Tools > Pool > Ensure Config Assets Exist.", hole);
                return;
            }
            Undo.SetCurrentGroupName("Placer les panneaux");
            int group = Undo.GetCurrentGroup();

            Transform root = hole.PlacedSigns;
            if (root == null)
            {
                root = new GameObject(PlayAnywhereCourseSigns.RootName).transform;
                root.SetParent(hole.transform, true);
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Placer les panneaux");
            }

            var kept = new List<Vector3>();
            foreach (PlayAnywhereSign sign in root.GetComponentsInChildren<PlayAnywhereSign>(true))
            {
                if (sign.IsHandEdited) kept.Add(sign.transform.position);
                else Undo.DestroyObjectImmediate(sign.gameObject);
            }

            float keepOff = settings.signSpacing * 0.4f;
            PlayAnywhereCourseSigns.Compute(hole, settings, signPlacements);
            foreach (PlayAnywhereCourseSigns.Placement p in signPlacements)
            {
                if (kept.Exists(k => Vector3.Distance(k, p.position) < keepOff)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(settings.signPrefab.gameObject, root);
                Undo.RegisterCreatedObjectUndo(go, "Placer les panneaux");
                go.transform.SetPositionAndRotation(p.position, p.rotation);
                go.transform.localScale *= settings.signSize;
                var sign = go.GetComponent<PlayAnywhereSign>();
                sign.Configure(PlayAnywhereCourseSigns.Flipped(p), p.drop);
                sign.MarkGenerated();
            }
            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            var hole = (PlayAnywhereHole)target;

            if (hole.CourseSigns && !Application.isPlaying && hole.PlacedSigns == null && Event.current.type == EventType.Repaint) DrawSignPreview(hole);

            MoveTransform(hole.Player1Tee, new Color(0.3f, 0.6f, 1f), "Départ J1", null);
            MoveTransform(hole.Player2Tee, new Color(1f, 0.35f, 0.3f), "Départ J2", null);
            MoveTransform(hole.Cup, Color.yellow, "Trou", hole.Flag);

            for (int i = 0; i < hole.PointCount; i++)
            {
                hole.GetNode(i + 1, out Vector3 point, out float width);
                Handles.Label(point + Vector3.up * HandleUtility.GetHandleSize(point) * 0.3f, $"{i + 1}");
                if (MoveOnSurface(point, PathColour, out Vector3 moved))
                {
                    Undo.RecordObject(hole, "Déplacer un point du parcours");
                    hole.SetPoint(i, moved, width);
                }
                // Remove: a small red cube above the point.
                float size = HandleUtility.GetHandleSize(point) * 0.08f;
                Handles.color = new Color(1f, 0.3f, 0.3f);
                if (Handles.Button(point + Vector3.up * size * 4f, Quaternion.identity, size, size * 1.5f, Handles.CubeHandleCap))
                {
                    Undo.RecordObject(hole, "Retirer un point du parcours");
                    hole.RemovePoint(i);
                    return;
                }
            }

            if (hole.PathWidth <= 0f) return;
            for (int i = 0; i < hole.NodeCount; i++)
            {
                hole.GetNode(i, out Vector3 node, out float width);
                Vector3 side = NodeSide(hole, i);

                // Width: a dot on the corridor's edge, slid sideways.
                Handles.color = PathColour;
                Vector3 edge = node + side * (width * 0.5f);
                EditorGUI.BeginChangeCheck();
                Vector3 newEdge = Handles.Slider(edge, side, HandleUtility.GetHandleSize(edge) * 0.06f, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    float newWidth = Mathf.Max(0.1f, Vector3.Dot(newEdge - node, side) * 2f);
                    Undo.RecordObject(hole, "Largeur du parcours");
                    if (i == 0 || i == hole.NodeCount - 1)
                    {
                        serializedObject.Update();
                        serializedObject.FindProperty("pathWidth").floatValue = newWidth;
                        serializedObject.ApplyModifiedProperties();
                    }
                    else hole.SetPoint(i - 1, node, newWidth);
                }

                // Insert: a "+" halfway to the next node.
                if (i == hole.NodeCount - 1) continue;
                hole.GetNode(i + 1, out Vector3 next, out float nextWidth);
                Vector3 middle = (node + next) * 0.5f;
                float size = HandleUtility.GetHandleSize(middle) * 0.06f;
                Handles.Label(middle + Vector3.up * size * 2f, "+");
                if (Handles.Button(middle, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                {
                    Undo.RecordObject(hole, "Ajouter un point au parcours");
                    hole.InsertPoint(i, middle, (width + nextWidth) * 0.5f);
                    return;
                }
            }
        }

        private static readonly List<PlayAnywhereCourseSigns.Placement> signPlacements = new List<PlayAnywhereCourseSigns.Placement>();

        // Where the signs will stand, as outlines: post, board, an arrow the
        // way the chevrons point. Sizes are those of the generated sign.
        private static void DrawSignPreview(PlayAnywhereHole hole)
        {
            var settings = PoolSettingsLoader.LoadOrDefault<PlayAnywhereSettings>("PlayAnywhereSettings");
            PlayAnywhereCourseSigns.Compute(hole, settings, signPlacements);
            Handles.color = new Color(1f, 0.6f, 0.1f);
            float size = settings.signSize;
            foreach (PlayAnywhereCourseSigns.Placement p in signPlacements)
            {
                Vector3 board = p.position + Vector3.up * (1.1f * size);
                Vector3 across = p.rotation * Vector3.right * (0.45f * size);
                Vector3 up = Vector3.up * (0.2f * size);
                Handles.DrawLine(p.position + Vector3.down * p.drop, p.position + Vector3.up * (1.4f * size));
                Handles.DrawPolyLine(board - across - up, board + across - up, board + across + up, board - across + up, board - across - up);
                Vector3 tip = board + (PlayAnywhereCourseSigns.Flipped(p) ? -across : across) * 0.8f;
                Handles.DrawLine(board - (tip - board), tip);
                Handles.DrawLine(tip, board + up * 0.7f);
                Handles.DrawLine(tip, board - up * 0.7f);
            }
        }

        // Across the course at this node: square to the stretch through it.
        private static Vector3 NodeSide(PlayAnywhereHole hole, int index)
        {
            hole.GetNode(Mathf.Max(0, index - 1), out Vector3 before, out _);
            hole.GetNode(Mathf.Min(hole.NodeCount - 1, index + 1), out Vector3 after, out _);
            Vector3 along = after - before;
            along.y = 0f;
            return along.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, along.normalized) : Vector3.right;
        }

        // A tee or the cup, moved onto the surface under the mouse. What
        // follows (the flag) moves by as much.
        private static void MoveTransform(Transform piece, Color colour, string label, Transform follower)
        {
            if (piece == null) return;
            Handles.Label(piece.position + Vector3.up * HandleUtility.GetHandleSize(piece.position) * 0.3f, label);
            if (!MoveOnSurface(piece.position, colour, out Vector3 moved)) return;
            Vector3 delta = moved - piece.position;
            Undo.RecordObject(piece, $"Déplacer {label}");
            piece.position = moved;
            if (follower == null) return;
            Undo.RecordObject(follower, $"Déplacer {label}");
            follower.position += delta;
        }

        // A sphere dragged in the view; it lands where the mouse ray meets a
        // collider (the tees and the cup have none), else stays in the
        // view's plane.
        private static bool MoveOnSurface(Vector3 position, Color colour, out Vector3 moved)
        {
            Handles.color = colour;
            EditorGUI.BeginChangeCheck();
            moved = Handles.FreeMoveHandle(position, HandleUtility.GetHandleSize(position) * 0.12f, Vector3.zero, Handles.SphereHandleCap);
            if (!EditorGUI.EndChangeCheck()) return false;
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                moved = hit.point;
            return true;
        }
    }
}
#endif
