#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UntitledPoolGame.PoolEditor
{
    // Draws a prefab as a translucent ghost in the Scene view, where a tool
    // is about to place it (the Level Maker's Props tool). Nothing is
    // instantiated: the prefab's meshes are drawn directly during the Scene
    // view's repaint (Graphics.DrawMeshNow), so no hidden object can be left
    // behind in the scene or saved with it. Parts behind other geometry are
    // drawn fainter, so a prop placed against a wall still reads.
    public static class PrefabGhost
    {
        private struct Part
        {
            public Mesh mesh;
            public Matrix4x4 local;   // from the prefab root's own frame
        }

        private static readonly Dictionary<GameObject, Part[]> cache = new Dictionary<GameObject, Part[]>();
        private static Material material;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        // position / rotation / scale: the root's, as placed (see
        // RoomBuilder.PropPose). Call during EventType.Repaint only.
        public static void Draw(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            if (prefab == null || Event.current == null || Event.current.type != EventType.Repaint) return;
            Material mat = Material;
            if (mat == null) return;
            Part[] parts = PartsOf(prefab);
            Matrix4x4 root = Matrix4x4.TRS(position, rotation, scale);

            // Hidden parts first, faint; then the visible ones.
            DrawParts(mat, parts, root, new Color(color.r, color.g, color.b, color.a * 0.3f), 5f);
            DrawParts(mat, parts, root, color, 4f);
        }

        private static void DrawParts(Material mat, Part[] parts, Matrix4x4 root, Color color, float zTest)
        {
            mat.SetColor(ColorId, color);
            mat.SetFloat(ZTestId, zTest);
            if (!mat.SetPass(0)) return;
            foreach (Part part in parts)
            {
                Matrix4x4 m = root * part.local;
                for (int sub = 0; sub < part.mesh.subMeshCount; sub++)
                    Graphics.DrawMeshNow(part.mesh, m, sub);
            }
        }

        // The prefab's visible meshes (first LOD only), relative to its root.
        // Cached: palettes reuse the same prefabs all the time.
        private static Part[] PartsOf(GameObject prefab)
        {
            if (cache.TryGetValue(prefab, out Part[] parts)) return parts;

            var skip = new HashSet<Renderer>();
            foreach (LODGroup group in prefab.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    foreach (Renderer r in lods[i].renderers)
                        if (r != null) skip.Add(r);
            }

            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            var list = new List<Part>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || skip.Contains(renderer)) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                list.Add(new Part { mesh = mesh, local = toRoot * renderer.transform.localToWorldMatrix });
            }
            parts = list.ToArray();
            cache[prefab] = parts;
            return parts;
        }

        private static Material Material
        {
            get
            {
                if (material != null) return material;
                Shader shader = Shader.Find("Hidden/UntitledPool/PrefabGhost");
                if (shader == null)
                {
                    Debug.LogWarning("[Level Maker] Shader du fantôme introuvable (Hidden/UntitledPool/PrefabGhost) : pas d'aperçu des props.");
                    return null;
                }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return material;
            }
        }

        // A prefab edited since it was cached would show its old shape.
        [InitializeOnLoadMethod]
        private static void ClearOnAssetChange() => EditorApplication.projectChanged += cache.Clear;
    }
}
#endif
