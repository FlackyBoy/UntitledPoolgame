#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // The power tokens on the table: a small 3D object showing what the power
    // does (a rocket for Tir boosté, a bomb for Bille destructrice...) over a
    // see-through ring in the category's colour, spinning and bobbing
    // (PowerTokenFloat). Built from simple shapes, nothing modelled: a new
    // power gets its shape with a case in BuildShape, otherwise a "?". A "?"
    // token per category fills the empty category slots too.
    //
    // Tools > Pool > Générer les jetons de pouvoir only creates missing ones
    // (they're placeholders to retouch). "Régénérer tous les jetons"
    // rebuilds them in place after a confirmation. A token of the user's own
    // is never touched.
    internal static class PowerTokenGenerator
    {
        private const string PrefabFolder = "Assets/Prefabs/Powers";
        private const string MeshFolder = "Assets/Prefabs/Powers/Meshes";
        private const string MaterialFolder = "Assets/Materials/PowerTokens";
        // The HUD icons (PoolPower > Hud Icon), same shapes as the tokens.
        private const string HudIconFolder = "Assets/Textures/PowerHudIcons";
        private const int HudIconSize = 256;

        // Origin on the felt, ring lying there, object floating above. Every
        // object is scaled to ShapeSize at its largest.
        private const float ShapeSize = 0.075f;
        private const float Hover = 0.025f;          // felt -> bottom of the object, bob included
        private const float RingOpacity = 0.6f;      // lets the felt show through
        private const float RingRadius = 0.05f;
        private const float RingTube = 0.005f;

        // Only the missing ones: the generated prefabs are placeholders the
        // user retouches by hand (17/10), rebuilding them would undo that.
        [MenuItem("Tools/Pool/Générer les jetons de pouvoir", priority = 25)]
        private static void GenerateMissing() => Generate(onlyMissing: true);

        [MenuItem("Tools/Pool/Régénérer tous les jetons de pouvoir (écrase les retouches)", priority = 26)]
        private static void RegenerateAll()
        {
            if (EditorUtility.DisplayDialog("Régénérer les jetons de pouvoir",
                    $"Tous les jetons générés ({PrefabFolder}/Token_*) vont être reconstruits : les modifications faites à la main sur ces prefabs seront perdues.",
                    "Tout régénérer", "Annuler"))
                Generate(onlyMissing: false);
        }

        // onlyMissing: only powers (and category slots) with no token yet.
        public static void Generate(bool onlyMissing)
        {
            RemoveFirstVersion();
            var settings = Resources.Load<PoolPowerSpawnSettings>("PoolPowerSpawnSettings");
            int count = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:PoolPower"))
            {
                var power = AssetDatabase.LoadAssetAtPath<PoolPower>(AssetDatabase.GUIDToAssetPath(guid));
                if (power == null) continue;
                var serialized = new SerializedObject(power);
                SerializedProperty slot = serialized.FindProperty("tokenPrefab");
                if (slot == null) continue;

                string key = power.GetType().Name.Replace("Power", "");

                // Its HUD icon, on the same terms as the token: only when
                // missing, or all generated ones on a full regeneration.
                SerializedProperty icon = serialized.FindProperty("hudIcon");
                if (icon != null)
                {
                    bool ourIcon = icon.objectReferenceValue == null
                                   || AssetDatabase.GetAssetPath(icon.objectReferenceValue).StartsWith(HudIconFolder + "/");
                    if (ourIcon && (!onlyMissing || icon.objectReferenceValue == null))
                    {
                        icon.objectReferenceValue = BakeHudIcon(key);
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        count++;
                    }
                }

                bool ours = slot.objectReferenceValue == null || IsGenerated(slot.objectReferenceValue);
                if (!ours || (onlyMissing && slot.objectReferenceValue != null)) continue;
                Color color = settings != null ? settings.GetColor(power.Type) : Color.gray;
                slot.objectReferenceValue = BuildToken("Token_" + key, key, power.Type, color);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            // The category slots: a "?" token, when nothing is there yet.
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                foreach (PowerType type in new[] { PowerType.Attack, PowerType.Defense, PowerType.Effect })
                {
                    SerializedProperty slot = serialized.FindProperty(type.ToString().ToLowerInvariant() + "CratePrefab");
                    if (slot == null) continue;
                    if (slot.objectReferenceValue != null && (onlyMissing || !IsGenerated(slot.objectReferenceValue))) continue;
                    slot.objectReferenceValue = BuildToken("Token_" + type, "Unknown", type, settings.GetColor(type));
                    count++;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            if (count > 0 || !onlyMissing) Debug.Log($"[PowerTokens] {count} jeton(s) générés dans {PrefabFolder}.");
        }

        private static bool IsGenerated(Object asset) =>
            AssetDatabase.GetAssetPath(asset).StartsWith(PrefabFolder + "/Token_");

        // Removes what the first, box-shaped version of the tokens generated.
        private static void RemoveFirstVersion()
        {
            bool removed = AssetDatabase.DeleteAsset("Assets/Textures/PowerIcons");
            removed |= AssetDatabase.DeleteAsset($"{PrefabFolder}/PowerTokenBody.asset");
            if (AssetDatabase.IsValidFolder(MaterialFolder))
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string file = Path.GetFileNameWithoutExtension(path);
                    if (file.StartsWith("Icon_") || (file.StartsWith("Token_") && !file.StartsWith("Token_Ring")))
                        removed |= AssetDatabase.DeleteAsset(path);
                }
            if (removed) Debug.Log("[PowerTokens] Anciens jetons texturés retirés (icônes, matériaux, cube arrondi).");
        }

        // ---------- HUD icons ----------

        // White with a dark outline on transparent, like the HUD's own
        // drawings. Shapes checked in a rough render (17/10).
        private static Texture2D BakeHudIcon(string key)
        {
            var g = new Glyph();
            DrawHudIcon(g, key);
            if (g.Empty) DrawHudIcon(g, "Unknown");

            var pixels = new Color32[HudIconSize * HudIconSize];
            const float outline = 0.09f;   // glyph units: the icon spans -1..1
            float pixel = 2f / HudIconSize;
            for (int y = 0; y < HudIconSize; y++)
                for (int x = 0; x < HudIconSize; x++)
                {
                    float d = g.Distance((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
                    float fill = Mathf.Clamp01(0.5f - d / pixel);
                    float ink = Mathf.Clamp01(0.5f - (d - outline) / pixel);
                    pixels[y * HudIconSize + x] = Color.Lerp(new Color(0.08f, 0.06f, 0.1f, ink), Color.white, fill);
                }
            var texture = new Texture2D(HudIconSize, HudIconSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            EnsureFolder(HudIconFolder);
            string path = $"{HudIconFolder}/{key}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // One case per power (class name without "Power"), same objects as the
        // tokens. Coordinates -1..1, y up, within about 0.8. Cut = a hole.
        private static void DrawHudIcon(Glyph g, string key)
        {
            switch (key)
            {
                case "BoostedShot":   // a rocket
                    g.Add(Glyph.Segment(0f, -0.25f, 0f, 0.3f, 0.42f));
                    g.Add(Glyph.Polygon((-0.21f, 0.3f), (0.21f, 0.3f), (0f, 0.72f)));
                    g.Add(Glyph.Polygon((-0.21f, -0.1f), (-0.48f, -0.42f), (-0.21f, -0.38f)));
                    g.Add(Glyph.Polygon((0.21f, -0.1f), (0.48f, -0.42f), (0.21f, -0.38f)));
                    g.Add(Glyph.Polygon((-0.13f, -0.5f), (0.13f, -0.5f), (0f, -0.8f)));
                    g.Cut(Glyph.Circle(0f, 0.12f, 0.09f));
                    break;
                case "VisionImpair":  // an eye above a bank of fog
                    g.Arc(0f, -0.45f, 0.85f, 37f, 143f, 0.1f);
                    g.Arc(0f, 0.55f, 0.85f, 217f, 323f, 0.1f);
                    g.Add(Glyph.Circle(0f, 0.07f, 0.2f));
                    g.Add(Glyph.Circle(-0.42f, -0.55f, 0.17f));
                    g.Add(Glyph.Circle(-0.15f, -0.5f, 0.21f));
                    g.Add(Glyph.Circle(0.15f, -0.52f, 0.19f));
                    g.Add(Glyph.Circle(0.42f, -0.56f, 0.16f));
                    g.Add(Glyph.Box(0f, -0.66f, 0.5f, 0.08f, 0.06f));
                    g.Cut(Glyph.Circle(0f, 0.07f, 0.08f));
                    break;
                case "InvertedControls":   // two arrows chasing each other
                    g.Arc(0f, 0f, 0.55f, 20f, 160f, 0.13f);
                    g.Add(Glyph.Polygon((-0.78f, 0.12f), (-0.32f, 0.12f), (-0.55f, -0.18f)));
                    g.Arc(0f, 0f, 0.55f, 200f, 340f, 0.13f);
                    g.Add(Glyph.Polygon((0.78f, -0.12f), (0.32f, -0.12f), (0.55f, 0.18f)));
                    break;
                case "ClosePocket":   // a padlock
                    g.Add(Glyph.Box(0f, -0.25f, 0.48f, 0.38f, 0.1f));
                    g.Arc(0f, 0.13f, 0.3f, 0f, 180f, 0.13f);
                    g.Add(Glyph.Segment(-0.3f, 0.13f, -0.3f, 0f, 0.13f));
                    g.Add(Glyph.Segment(0.3f, 0.13f, 0.3f, 0f, 0.13f));
                    g.Cut(Glyph.Circle(0f, -0.2f, 0.09f));
                    g.Cut(Glyph.Segment(0f, -0.2f, 0f, -0.42f, 0.08f));
                    break;
                case "BallBlast":     // a bomb, fuse lit
                    g.Add(Glyph.Circle(-0.1f, -0.2f, 0.5f));
                    g.Add(Glyph.Box(-0.1f, 0.33f, 0.13f, 0.07f, 0f));
                    g.Arc(0.22f, 0.4f, 0.3f, 180f, 105f, 0.09f);
                    g.Add(Glyph.Star(0.13f, 0.72f, 0.22f, 0.09f, 6, 0f));
                    g.Cut(Glyph.Circle(-0.3f, -0.02f, 0.09f));
                    break;
                case "Magnet":        // a horseshoe magnet
                    g.Arc(0f, -0.05f, 0.45f, 180f, 360f, 0.2f);
                    g.Add(Glyph.Segment(-0.45f, -0.05f, -0.45f, 0.45f, 0.2f));
                    g.Add(Glyph.Segment(0.45f, -0.05f, 0.45f, 0.45f, 0.2f));
                    g.Cut(Glyph.Box(-0.45f, 0.4f, 0.06f, 0.12f, 0f));
                    g.Cut(Glyph.Box(0.45f, 0.4f, 0.06f, 0.12f, 0f));
                    break;
                case "IdenticalGroups":   // three identical balls
                    foreach (var (x, y) in new[] { (-0.38f, -0.25f), (0.38f, -0.25f), (0f, 0.4f) })
                    {
                        g.Add(Glyph.Circle(x, y, 0.26f));
                        g.Cut(Glyph.Circle(x, y, 0.1f));
                    }
                    break;
                case "TableHole":     // a meteor and its trail of fire
                    g.Add(Glyph.Circle(-0.3f, -0.3f, 0.36f));
                    g.Add(Glyph.Polygon((-0.57f, -0.03f), (0.72f, 0.72f), (-0.03f, -0.57f)));
                    g.Cut(Glyph.Segment(-0.05f, 0.05f, 0.42f, 0.42f, 0.07f));
                    g.Cut(Glyph.Segment(0.05f, -0.12f, 0.38f, 0.2f, 0.06f));
                    g.Cut(Glyph.Circle(-0.42f, -0.2f, 0.08f));
                    g.Cut(Glyph.Circle(-0.18f, -0.45f, 0.06f));
                    break;
                case "Unknown":
                    g.Arc(0f, 0.3f, 0.32f, -60f, 200f, 0.15f);
                    g.Add(Glyph.Segment(0.16f, 0.02f, 0f, -0.18f, 0.15f));
                    g.Add(Glyph.Segment(0f, -0.18f, 0f, -0.3f, 0.15f));
                    g.Add(Glyph.Circle(0f, -0.6f, 0.11f));
                    break;
            }
        }

        // Signed distance (< 0 inside) of a union of shapes, minus the cut ones.
        private class Glyph
        {
            private readonly List<System.Func<float, float, float>> shapes = new List<System.Func<float, float, float>>();
            private readonly List<System.Func<float, float, float>> cuts = new List<System.Func<float, float, float>>();
            public bool Empty => shapes.Count == 0;

            public void Add(System.Func<float, float, float> shape) => shapes.Add(shape);
            public void Cut(System.Func<float, float, float> shape) => cuts.Add(shape);

            public float Distance(float x, float y)
            {
                float d = float.MaxValue;
                foreach (var s in shapes) d = Mathf.Min(d, s(x, y));
                float hole = float.MaxValue;
                foreach (var c in cuts) hole = Mathf.Min(hole, c(x, y));
                return cuts.Count > 0 ? Mathf.Max(d, -hole) : d;
            }

            // Degrees, counter-clockwise from +x; from may be past to.
            public void Arc(float cx, float cy, float r, float from, float to, float width)
            {
                const int steps = 24;
                for (int i = 0; i < steps; i++)
                {
                    float a0 = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                    float a1 = Mathf.Lerp(from, to, (i + 1) / (float)steps) * Mathf.Deg2Rad;
                    Add(Segment(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, width));
                }
            }

            public static System.Func<float, float, float> Circle(float cx, float cy, float r) =>
                (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

            // Half sizes, corner radius.
            public static System.Func<float, float, float> Box(float cx, float cy, float hw, float hh, float r) => (x, y) =>
            {
                float dx = Mathf.Abs(x - cx) - hw + r, dy = Mathf.Abs(y - cy) - hh + r;
                return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
            };

            public static System.Func<float, float, float> Segment(float ax, float ay, float bx, float by, float width) => (x, y) =>
            {
                var a = new Vector2(ax, ay);
                Vector2 ab = new Vector2(bx, by) - a, ap = new Vector2(x, y) - a;
                float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                return (ap - ab * t).magnitude - width / 2f;
            };

            public static System.Func<float, float, float> Star(float cx, float cy, float outer, float inner, int points, float startDeg)
            {
                var poly = new (float, float)[points * 2];
                for (int i = 0; i < poly.Length; i++)
                {
                    float a = (startDeg + i * 180f / points) * Mathf.Deg2Rad;
                    float r = i % 2 == 0 ? outer : inner;
                    poly[i] = (cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                }
                return Polygon(poly);
            }

            // Any simple polygon: distance to its edges, negative inside.
            public static System.Func<float, float, float> Polygon(params (float x, float y)[] points) => (x, y) =>
            {
                var p = new Vector2(x, y);
                float d = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    var a = new Vector2(points[j].x, points[j].y);
                    var b = new Vector2(points[i].x, points[i].y);
                    Vector2 ab = b - a, ap = p - a;
                    float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    d = Mathf.Min(d, (ap - ab * t).magnitude);
                    if ((b.y > y) != (a.y > y) && x < (a.x - b.x) * (y - b.y) / (a.y - b.y) + b.x) inside = !inside;
                }
                return inside ? -d : d;
            };
        }

        // ---------- Token ----------

        private static GameObject BuildToken(string name, string key, PowerType type, Color categoryColor)
        {
            var root = new GameObject(name);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            var shape = new GameObject("Shape").transform;
            shape.SetParent(pivot, false);
            var tokenFloat = root.AddComponent<PowerTokenFloat>();

            // The ring stays flat on the felt (child of the root, not of the
            // pivot that spins and tilts).
            Part(root.transform, "Category ring", Torus(RingRadius, RingTube, 360f), RingMaterial(type, categoryColor),
                new Vector3(0f, RingTube + 0.001f, 0f), Vector3.zero, Vector3.one, shadows: false);

            if (!BuildShape(shape, key)) BuildQuestionMark(shape);
            float halfHeight = FitToSize(shape);
            pivot.localPosition = new Vector3(0f, Hover + halfHeight, 0f);

            // Shapes read better nearly upright than on a corner.
            var serialized = new SerializedObject(tokenFloat);
            serialized.FindProperty("pivot").objectReferenceValue = pivot;
            serialized.FindProperty("tilt").floatValue = 8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(PrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // One case per power. Objects fit in about 15 cm around the pivot.
        // Upright arcs and arrows are in the XY plane: Vertical turns a torus
        // arc (laid in XZ) up into it.
        private static readonly Vector3 Vertical = new Vector3(-90f, 0f, 0f);

        private static bool BuildShape(Transform p, string key)
        {
            switch (key)
            {
                case "BoostedShot":   // a rocket
                {
                    Material red = Red;
                    Part(p, "Body", Builtin("Capsule"), White, Vector3.zero, Vector3.zero, new Vector3(0.055f, 0.045f, 0.055f));
                    Part(p, "Nose", Cone(), red, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.055f, 0.04f, 0.055f));
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 120f;
                        Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                        Part(p, "Fin", Builtin("Cube"), red, dir * 0.032f + Vector3.down * 0.03f, new Vector3(0f, a, 0f), new Vector3(0.006f, 0.035f, 0.025f));
                    }
                    Part(p, "Flame", Cone(), Flame, new Vector3(0f, -0.048f, 0f), new Vector3(180f, 0f, 0f), Vector3.one * 0.035f, shadows: false);
                    return true;
                }
                case "VisionImpair":  // a cartoon eye, fog drifting over it
                {
                    Part(p, "Eyeball", Builtin("Sphere"), White, Vector3.zero, Vector3.zero, Vector3.one * 0.08f);
                    Part(p, "Iris", Builtin("Sphere"), Lit("Iris", new Color(0.2f, 0.55f, 0.95f)), new Vector3(0f, 0.004f, 0.034f), Vector3.zero, new Vector3(0.038f, 0.038f, 0.014f));
                    Part(p, "Pupil", Builtin("Sphere"), Black, new Vector3(0f, 0.004f, 0.041f), Vector3.zero, new Vector3(0.019f, 0.019f, 0.006f));
                    Part(p, "Shine", Builtin("Sphere"), Glow("Shine", Color.white), new Vector3(0.008f, 0.013f, 0.043f), Vector3.zero, Vector3.one * 0.007f, shadows: false);
                    Material cloud = Lit("Cloud", new Color(0.86f, 0.88f, 0.93f), smoothness: 0.15f);
                    Part(p, "Fog", Builtin("Sphere"), cloud, new Vector3(-0.024f, -0.024f, 0.03f), Vector3.zero, new Vector3(0.04f, 0.032f, 0.032f));
                    Part(p, "Fog", Builtin("Sphere"), cloud, new Vector3(0.002f, -0.03f, 0.034f), Vector3.zero, new Vector3(0.046f, 0.036f, 0.034f));
                    Part(p, "Fog", Builtin("Sphere"), cloud, new Vector3(0.027f, -0.021f, 0.029f), Vector3.zero, new Vector3(0.036f, 0.03f, 0.03f));
                    return true;
                }
                case "InvertedControls":   // two arrows chasing each other
                {
                    Material yellow = Lit("Yellow", new Color(1f, 0.82f, 0.15f));
                    const float r = 0.045f, sweep = 140f;
                    foreach (float start in new[] { 20f, 200f })
                    {
                        Part(p, "Arc", Torus(r, 0.009f, sweep), yellow, Vector3.zero, Vertical, Vector3.one, rotateInPlane: start);
                        float end = (start + sweep) * Mathf.Deg2Rad;
                        Vector3 tip = new Vector3(Mathf.Cos(end), Mathf.Sin(end), 0f) * r;
                        Vector3 tangent = new Vector3(-Mathf.Sin(end), Mathf.Cos(end), 0f);
                        Part(p, "Head", Cone(), yellow, tip, Quaternion.FromToRotation(Vector3.up, tangent).eulerAngles, new Vector3(0.035f, 0.03f, 0.035f));
                    }
                    return true;
                }
                case "ClosePocket":   // a padlock
                {
                    Material gold = Lit("Gold", new Color(1f, 0.75f, 0.2f), smoothness: 0.8f, metallic: 0.8f);
                    Part(p, "Body", Builtin("Cube"), gold, new Vector3(0f, -0.02f, 0f), Vector3.zero, new Vector3(0.08f, 0.065f, 0.035f));
                    Part(p, "Shackle", Torus(0.025f, 0.007f, 180f), Steel, new Vector3(0f, 0.03f, 0f), Vertical, Vector3.one);
                    Part(p, "Leg", Builtin("Cylinder"), Steel, new Vector3(-0.025f, 0.021f, 0f), Vector3.zero, new Vector3(0.014f, 0.009f, 0.014f));
                    Part(p, "Leg", Builtin("Cylinder"), Steel, new Vector3(0.025f, 0.021f, 0f), Vector3.zero, new Vector3(0.014f, 0.009f, 0.014f));
                    Part(p, "Keyhole", Builtin("Cylinder"), Black, new Vector3(0f, -0.02f, 0.0175f), new Vector3(90f, 0f, 0f), new Vector3(0.014f, 0.002f, 0.014f));
                    return true;
                }
                case "BallBlast":     // a cartoon bomb, fuse lit
                {
                    // A thick fuse, clear of the spark at its tip.
                    Vector3 fuseBase = new Vector3(-0.035f, 0.035f, 0f);
                    Part(p, "Bomb", Builtin("Sphere"), Lit("Bomb", new Color(0.14f, 0.15f, 0.19f), smoothness: 0.65f), new Vector3(0f, -0.01f, 0f), Vector3.zero, Vector3.one * 0.07f);
                    Part(p, "Shine", Builtin("Sphere"), Glow("Shine", Color.white), new Vector3(-0.014f, 0.01f, 0.026f), Vector3.zero, new Vector3(0.012f, 0.008f, 0.006f), shadows: false);
                    Part(p, "Cap", Builtin("Cylinder"), Steel, new Vector3(0f, 0.027f, 0f), Vector3.zero, new Vector3(0.028f, 0.008f, 0.028f));
                    // From the top of the cap, curving up and to the side.
                    Part(p, "Fuse", Torus(0.035f, 0.007f, 100f), Lit("Fuse", new Color(0.85f, 0.69f, 0.43f)), fuseBase, Vertical, Vector3.one);
                    Vector3 sparkAt = fuseBase + new Vector3(Mathf.Cos(100f * Mathf.Deg2Rad), Mathf.Sin(100f * Mathf.Deg2Rad), 0f) * 0.035f;
                    Part(p, "Spark", Builtin("Sphere"), Spark, sparkAt, Vector3.zero, Vector3.one * 0.018f, shadows: false);
                    for (int i = 0; i < 4; i++)
                        Part(p, "Ray", Builtin("Cube"), Spark, sparkAt, new Vector3(0f, 0f, i * 45f), new Vector3(0.046f, 0.007f, 0.007f), shadows: false);
                    return true;
                }
                case "Magnet":        // a horseshoe magnet
                {
                    Material red = Red;
                    Part(p, "Bend", Torus(0.035f, 0.013f, 180f), red, new Vector3(0f, -0.01f, 0f), Vertical, Vector3.one, rotateInPlane: 180f);
                    Part(p, "Leg", Builtin("Cylinder"), red, new Vector3(-0.035f, 0.01f, 0f), Vector3.zero, new Vector3(0.026f, 0.02f, 0.026f));
                    Part(p, "Leg", Builtin("Cylinder"), red, new Vector3(0.035f, 0.01f, 0f), Vector3.zero, new Vector3(0.026f, 0.02f, 0.026f));
                    Part(p, "Tip", Builtin("Cylinder"), Steel, new Vector3(-0.035f, 0.038f, 0f), Vector3.zero, new Vector3(0.0262f, 0.008f, 0.0262f));
                    Part(p, "Tip", Builtin("Cylinder"), Steel, new Vector3(0.035f, 0.038f, 0f), Vector3.zero, new Vector3(0.0262f, 0.008f, 0.0262f));
                    return true;
                }
                case "IdenticalGroups":   // three identical billiard balls, racked
                {
                    // Laid flat, 7.5 cm apart: upright and close they overlapped
                    // as the token turned. White spot facing up.
                    Material ball = Lit("BallYellow", new Color(1f, 0.8f, 0.15f), smoothness: 0.9f, emission: 0.1f);
                    Material spot = Lit("BallSpot", new Color(0.97f, 0.97f, 0.95f), smoothness: 0.9f);
                    foreach (Vector3 at in new[] { new Vector3(-0.0375f, 0f, -0.0217f), new Vector3(0.0375f, 0f, -0.0217f), new Vector3(0f, 0f, 0.0433f) })
                    {
                        Part(p, "Ball", Builtin("Sphere"), ball, at, Vector3.zero, Vector3.one * 0.045f);
                        Part(p, "Spot", Builtin("Sphere"), spot, at + new Vector3(0f, 0.0205f, 0f), Vector3.zero, new Vector3(0.02f, 0.006f, 0.02f));
                    }
                    return true;
                }
                case "TableHole":     // a meteor: a rock trailing balls of fire
                {
                    // A trail of shrinking fireballs behind the rock (one long
                    // flame swallowed it).
                    Part(p, "Rock", BumpyRock(), Lit("Rock", new Color(0.47f, 0.38f, 0.33f), smoothness: 0.25f), Vector3.zero, Vector3.zero, Vector3.one);
                    Vector3 back = new Vector3(0.7f, 1f, 0f).normalized;   // up and back, as if falling
                    var trail = new (float distance, float radius, bool yellow, float z)[]
                    {
                        (0.032f, 0.026f, false, -0.008f), (0.056f, 0.021f, false, -0.006f), (0.076f, 0.016f, true, -0.004f),
                        (0.091f, 0.011f, false, -0.002f), (0.102f, 0.007f, true, 0f),
                    };
                    foreach (var (distance, radius, yellow, z) in trail)
                        Part(p, "Fire", Builtin("Sphere"), yellow ? Spark : Flame, back * distance + new Vector3(0f, 0f, z), Vector3.zero, Vector3.one * (radius * 2f), shadows: false);
                    Part(p, "Core", Builtin("Sphere"), Spark, back * 0.03f + new Vector3(0f, 0f, 0.006f), Vector3.zero, Vector3.one * 0.026f, shadows: false);
                    return true;
                }
            }
            return false;
        }

        // "?": an arc, a short stem and a dot.
        private static void BuildQuestionMark(Transform p)
        {
            Material white = Lit("QuestionMark", new Color(0.97f, 0.97f, 0.97f), emission: 0.25f);
            Part(p, "Hook", Torus(0.024f, 0.0075f, 260f), white, new Vector3(0f, 0.022f, 0f), Vertical, Vector3.one, rotateInPlane: -60f);
            Part(p, "Stem", Builtin("Cylinder"), white, new Vector3(0.004f, -0.008f, 0f), new Vector3(0f, 0f, 20f), new Vector3(0.015f, 0.011f, 0.015f));
            Part(p, "Dot", Builtin("Sphere"), white, new Vector3(0f, -0.045f, 0f), Vector3.zero, Vector3.one * 0.018f);
        }

        // Scales the shape to ShapeSize at its largest and centres it on the
        // pivot; returns its half height once scaled.
        private static float FitToSize(Transform shape)
        {
            Matrix4x4 toShape = shape.worldToLocalMatrix;
            bool found = false;
            var bounds = new Bounds();
            foreach (MeshFilter filter in shape.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 m = toShape * filter.transform.localToWorldMatrix;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (!found) return 0f;
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = largest > 1e-5f ? ShapeSize / largest : 1f;
            shape.localScale = Vector3.one * scale;
            shape.localPosition = -bounds.center * scale;
            return bounds.extents.y * scale;
        }

        // rotateInPlane: turns a torus arc around its axis first, so once
        // stood up it starts at that angle.
        private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 euler, Vector3 scale,
            bool shadows = true, float rotateInPlane = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler) * Quaternion.Euler(0f, -rotateInPlane, 0f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // ---------- Materials ----------

        private static Material White => Lit("White", new Color(0.95f, 0.95f, 0.95f));
        private static Material Black => Lit("Black", new Color(0.04f, 0.04f, 0.05f), smoothness: 0.95f);
        private static Material Red => Lit("Red", new Color(0.9f, 0.15f, 0.12f));
        private static Material Steel => Lit("Silver", new Color(0.8f, 0.82f, 0.86f), smoothness: 0.85f, metallic: 0.9f);
        private static Material Flame => Glow("Flame", new Color(1f, 0.55f, 0.1f));
        private static Material Spark => Glow("Spark", new Color(1f, 0.9f, 0.3f));

        private static Material Lit(string name, Color color, float smoothness = 0.6f, float metallic = 0f, float emission = 0f)
        {
            Material m = LoadOrCreateMaterial($"{MaterialFolder}/Shape_{name}.mat", "Universal Render Pipeline/Lit");
            if (m == null) return null;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // Bright, self-lit (flames, sparks).
        private static Material Glow(string name, Color color)
        {
            Material m = LoadOrCreateMaterial($"{MaterialFolder}/Shape_{name}.mat", "Universal Render Pipeline/Unlit");
            if (m == null) return null;
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        // The category ring, see-through: transparent, alpha blended, set up
        // by URP's own code as its Inspector does when Surface Type changes.
        private static Material RingMaterial(PowerType type, Color color)
        {
            Material m = LoadOrCreateMaterial($"{MaterialFolder}/Token_Ring_{type}.mat", "Universal Render Pipeline/Lit");
            if (m == null) return null;
            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, RingOpacity));
            m.SetFloat("_Smoothness", 0.8f);
            m.SetColor("_EmissionColor", color * 0.8f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_EMISSION");
            BaseShaderGUI.SetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[PowerTokens] Shader « {shaderName} » introuvable : pièce sans matériau.");
                return null;
            }
            EnsureFolder(MaterialFolder);
            m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------- Meshes ----------

        private static Mesh Builtin(string name) => Resources.GetBuiltinResource<Mesh>(name + ".fbx");

        // Saved as assets (a prefab can't keep a mesh made in memory),
        // rebuilt in place on every run.
        private static Mesh SaveMesh(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            string path = $"{MeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = mesh == null;
            if (create) mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (create)
            {
                EnsureFolder(MeshFolder);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Base circle of radius 0.5 on y = 0, tip at y = 1 (scaled per part).
        private static Mesh Cone()
        {
            const int n = 20;
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < n; i++)
            {
                float a0 = i * 2f * Mathf.PI / n, a1 = (i + 1) * 2f * Mathf.PI / n;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f, p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
                Vector3 n0 = new Vector3(p0.x, 0.25f, p0.z).normalized, n1 = new Vector3(p1.x, 0.25f, p1.z).normalized;
                int b = v.Count;
                v.Add(p0); v.Add(Vector3.up); v.Add(p1);
                nr.Add(n0); nr.Add((n0 + n1).normalized); nr.Add(n1);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                b = v.Count;   // base cap
                v.Add(p0); v.Add(p1); v.Add(Vector3.zero);
                nr.Add(Vector3.down); nr.Add(Vector3.down); nr.Add(Vector3.down);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
            return SaveMesh("Cone", v, nr, t);
        }

        // A tube bent along an arc of radius major, in the XZ plane, from +X
        // toward +Z over sweep degrees (360 = a full ring), ends rounded.
        private static Mesh Torus(float major, float minor, float sweep)
        {
            string name = $"Torus_{major * 1000f:0}_{minor * 10000f:0}_{sweep:0}";
            int segments = Mathf.Max(6, Mathf.CeilToInt(48 * sweep / 360f));
            const int sides = 12;
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = sweep * Mathf.Deg2Rad * i / segments;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = 2f * Mathf.PI * j / sides;
                    Vector3 normal = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v.Add(radial * major + normal * minor);
                    nr.Add(normal);
                }
            }
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            if (sweep < 359f)
            {
                AddBall(v, nr, t, new Vector3(major, 0f, 0f), minor);
                float e = sweep * Mathf.Deg2Rad;
                AddBall(v, nr, t, new Vector3(Mathf.Cos(e), 0f, Mathf.Sin(e)) * major, minor);
            }
            return SaveMesh(name, v, nr, t);
        }

        // A small sphere closing the end of a tube.
        private static void AddBall(List<Vector3> v, List<Vector3> nr, List<int> t, Vector3 center, float radius)
        {
            const int rings = 6, sides = 10;
            int start = v.Count;
            for (int i = 0; i <= rings; i++)
            {
                float lat = Mathf.PI * i / rings - Mathf.PI / 2f;
                for (int j = 0; j <= sides; j++)
                {
                    float lon = 2f * Mathf.PI * j / sides;
                    var n = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    v.Add(center + n * radius);
                    nr.Add(n);
                }
            }
            AddSphereTriangles(t, start, rings, sides);
        }

        // Rows from the south pole up; outward-facing.
        private static void AddSphereTriangles(List<int> t, int start, int rings, int sides)
        {
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * (sides + 1) + j, b = a + sides + 1;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
        }

        // A lumpy rock about 6 cm across: a sphere whose radius varies with
        // a few fixed waves (always the same shape).
        private static Mesh BumpyRock()
        {
            const int rings = 14, sides = 20;
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float lat = Mathf.PI * i / rings - Mathf.PI / 2f;
                for (int j = 0; j <= sides; j++)
                {
                    float lon = 2f * Mathf.PI * j / sides;
                    var n = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    float bump = 1f + 0.12f * Mathf.Sin(3f * lon + 1.3f) * Mathf.Cos(2f * lat) + 0.08f * Mathf.Sin(5f * lat + 2f * lon);
                    v.Add(n * (0.032f * bump));
                    nr.Add(n);
                }
            }
            AddSphereTriangles(t, 0, rings, sides);
            Mesh mesh = SaveMesh("Rock", v, nr, t);
            mesh.RecalculateNormals();   // the bumps catch the light
            return mesh;
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
