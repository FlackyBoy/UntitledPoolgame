#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // The scorch decal around a table hole (TableHolePower > Scorch Decal):
    // bakes a texture (burnt felt under the hole's torn shape, an uneven
    // charred patch with blast streaks fading out — no black disc nor ember
    // ring: the hole's own random shape draws both), wraps it in a URP decal
    // material, and hands it to the Trou power asset if it has none. Called
    // by Tools > Pool > Ensure Config Assets Exist. Also warns when no URP
    // renderer has the Decal feature, without which decals don't show.
    internal static class TableHoleScorchGenerator
    {
        private const string TexturePath = "Assets/Textures/Decals/TableHoleScorch.png";
        private const string MaterialPath = "Assets/Materials/Decals/TableHoleScorch.mat";
        private const int Size = 256;
        // Where the hole's edge falls in the texture (radius 1 = its border):
        // 1 / Scorch Scale with the default 2.2.
        private const float HoleEdge = 0.45f;

        // Written in the texture's import settings: an older generated
        // texture (round black centre) is baked again; one of the user's own,
        // put in place of it, never is.
        private const string Version = "TableHoleScorch v3";

        public static void Ensure(string powerAssetPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) material = CreateMaterial();
            if (material == null) return;
            if (AssetImporter.GetAtPath(TexturePath) is TextureImporter existing
                && existing.userData != Version
                // The first version (15/10) wrote no marker at all.
                && (existing.userData.Length == 0 || existing.userData.StartsWith("TableHoleScorch")))
            {
                BakeTexture();
                Debug.Log($"[TableHoleScorch] {TexturePath} regenerated ({Version}).");
            }

            TableHolePower power = AssetDatabase.LoadAssetAtPath<TableHolePower>(powerAssetPath);
            if (power != null)
            {
                var serialized = new SerializedObject(power);
                SerializedProperty scorch = serialized.FindProperty("scorchDecal");
                if (scorch.objectReferenceValue == null)
                {
                    scorch.objectReferenceValue = material;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[TableHoleScorch] {MaterialPath} assigned to {powerAssetPath} (Scorch Decal).");
                }
            }

            WarnIfNoDecalFeature();
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Shader Graphs/Decal");
            if (shader == null)
            {
                Debug.LogWarning("[TableHoleScorch] Shader « Shader Graphs/Decal » introuvable (URP) : pas de decal pour les trous.");
                return null;
            }

            Texture2D texture = BakeTexture();
            var material = new Material(shader) { name = "TableHoleScorch" };
            if (material.HasProperty("Base_Map")) material.SetTexture("Base_Map", texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            EnsureFolder("Assets/Materials/Decals");
            AssetDatabase.CreateAsset(material, MaterialPath);
            Debug.Log($"[TableHoleScorch] Created {MaterialPath}.");
            return material;
        }

        private static Texture2D BakeTexture()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            var burnt = new Color(0.05f, 0.03f, 0.02f);
            var charred = new Color(0.12f, 0.07f, 0.04f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (Size - 1f) * 2f - 1f, v = y / (Size - 1f) * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float angle = Mathf.Atan2(v, u);
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    // Uneven outline (broad noise round the circle), blast
                    // streaks shooting out (sharp angular noise), and grain.
                    float around = Mathf.PerlinNoise(cos * 2.5f + 7f, sin * 2.5f + 3f);
                    float rays = Mathf.Pow(Mathf.PerlinNoise(cos * 9f + 41f, sin * 9f + 17f), 3f);
                    float grain = Mathf.PerlinNoise(u * 9f + 20f, v * 9f + 20f);

                    // No black disc in the middle: the hole's own torn shape
                    // (TableHole) draws the black; under it and around its
                    // lobes, burnt felt.
                    // (No ember ring here: a round one gave the torn hole a
                    // circle around it; the hole's own edge glows instead.)
                    float t = Mathf.InverseLerp(HoleEdge * 0.6f, 1f, r);
                    Color c = Color.Lerp(burnt, charred, Mathf.Clamp01(t * 1.3f));
                    float reach = t - rays * 0.55f + (around - 0.5f) * 0.6f + (grain - 0.5f) * 0.2f;
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reach));
                    c.a = r > 1f ? 0f : Mathf.Clamp01(alpha);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            EnsureFolder("Assets/Textures/Decals");
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath);
            if (AssetImporter.GetAtPath(TexturePath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.userData = Version;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        // Decals only show with the Decal feature on the URP renderer in use.
        private static void WarnIfNoDecalFeature()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                    if (feature is DecalRendererFeature) return;
            }
            Debug.LogWarning("[TableHoleScorch] Aucun renderer URP n'a la fonction Decal : le contour brûlé des trous ne s'affichera pas. " +
                             "Sélectionner Assets/Settings/PC_Renderer (et Mobile_Renderer) > Add Renderer Feature > Decal.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
