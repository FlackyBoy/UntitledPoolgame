#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // A course's sign placed in the scene: moved, turned and sized with Unity's
    // usual tools. The foot of its post stays on the ground: raised, the post
    // grows; lowered, it shortens, until the board reaches the ground. The ground
    // is looked up when the sign moves sideways, and remembered while it goes up
    // and down.
    [CustomEditor(typeof(PlayAnywhereSign))]
    [CanEditMultipleObjects]
    public class PlayAnywhereSignEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!((PlayAnywhereSign)target).gameObject.scene.IsValid()) return;

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            float size = EditorGUILayout.Slider("Taille", ((PlayAnywhereSign)target).transform.localScale.y, 0.1f, 5f);
            if (EditorGUI.EndChangeCheck())
                foreach (Object t in targets)
                {
                    Transform sign = ((PlayAnywhereSign)t).transform;
                    Undo.RecordObject(sign, "Taille du panneau");
                    sign.localScale = Vector3.one * size;
                }
            HeightGUI();
            if (GUILayout.Button("Remettre à sa place automatique"))
                foreach (Object t in targets)
                {
                    var sign = (PlayAnywhereSign)t;
                    RecordAll(sign, "Remettre le panneau à sa place");
                    Undo.RecordObject(sign.transform, "Remettre le panneau à sa place");
                    sign.RestoreGenerated();
                }
        }

        // Height against the post's normal length: 0 is that length, below 0 the
        // post is shorter and the board lower, above 0 the post is longer.
        private void HeightGUI()
        {
            var first = (PlayAnywhereSign)target;
            UpdateGround(first);
            EditorGUI.BeginChangeCheck();
            float height = EditorGUILayout.Slider("Hauteur", first.transform.position.y - first.GroundHeight, -first.MaxSink, 3f);
            if (!EditorGUI.EndChangeCheck()) return;
            foreach (Object t in targets)
            {
                var sign = (PlayAnywhereSign)t;
                UpdateGround(sign);
                RecordAll(sign, "Hauteur du panneau");
                Undo.RecordObject(sign.transform, "Hauteur du panneau");
                Vector3 position = sign.transform.position;
                position.y = sign.GroundHeight + Mathf.Max(height, -sign.MaxSink);
                sign.transform.position = position;
                sign.SetPostDrop(position.y - sign.GroundHeight);
            }
        }

        private void OnSceneGUI()
        {
            var sign = (PlayAnywhereSign)target;
            if (!sign.transform.hasChanged || Application.isPlaying) return;
            sign.transform.hasChanged = false;

            UpdateGround(sign);
            // Applied even at the same drop: scaling the sign changes the post.
            // Recorded so that it's saved on this prefab instance.
            RecordAll(sign, "Poteau du panneau");
            Vector3 position = sign.transform.position;
            float lowest = sign.GroundHeight - sign.MaxSink;
            if (position.y < lowest)
            {
                position.y = lowest;
                sign.transform.position = position;
                sign.transform.hasChanged = false;
            }
            sign.SetPostDrop(position.y - sign.GroundHeight);
        }

        // Looks up the ground if the sign moved sideways, from its foot, or
        // from the ground it knew if it's sunk. A sign placed before the ground
        // was remembered starts from the height it was generated at.
        private static void UpdateGround(PlayAnywhereSign sign)
        {
            if (sign.IsOverGroundSpot) return;
            Vector3 foot = sign.transform.position;
            float start = sign.GroundKnown ? Mathf.Max(foot.y, sign.GroundHeight) : sign.GeneratedHeight;
            bool found = PlayAnywhereCourseSigns.GroundBelow(new Vector3(foot.x, start + GroundProbe, foot.z), out RaycastHit ground);
            float height = found ? ground.point.y : foot.y;
            if (!sign.GroundKnown || Mathf.Abs(height - sign.GroundHeight) > 0.01f)
                Debug.Log($"[PlayAnywhere] Panneau '{sign.name}' : sol {(found ? $"sur '{ground.collider.name}'" : "introuvable")}, à y = {height:0.00} (pied à {foot.y:0.00}).", sign);
            Undo.RecordObject(sign, "Sol du panneau");
            sign.SetGround(height);
        }

        private const float GroundProbe = 0.05f;

        private static void RecordAll(PlayAnywhereSign sign, string name)
        {
            Undo.RecordObject(sign, name);
            foreach (Transform part in sign.GetComponentsInChildren<Transform>()) Undo.RecordObject(part, name);
        }
    }
}
#endif
