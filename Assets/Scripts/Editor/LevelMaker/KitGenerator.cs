#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // The palettes the Level Maker can create on its own: a generated
    // greybox kit (grey blocks at real-kit dimensions, to block out a room
    // before the art kit is chosen) and a palette pointing at the Forest Bar
    // asset already in the project. Existing assets are never overwritten.
    public static class KitGenerator
    {
        public const string KitsFolder = "Assets/LevelKits";
        private const string GreyboxFolder = KitsFolder + "/Greybox";
        private const string ForestBarPrefabs = "Assets/Plugins/RetopoStudios/ForestBar/HDRP/Art/Prefabs";

        // Greybox dimensions (meters): 2 m modules, 3 m high walls.
        private const float W = 2f, H = 3f, T = 0.2f, OpeningW = 1f, DoorH = 2.2f, SillH = 1f;

        public static PrefabPalette CreateGreybox()
        {
            string palettePath = GreyboxFolder + "/Palette_Greybox.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PrefabPalette>(palettePath);
            if (existing != null)
            {
                // Palettes made before stairs existed get theirs.
                if (existing.stairs.Count == 0 || existing.railings.Count == 0)
                {
                    AddGreyboxLevelPieces(existing);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                return existing;
            }

            LevelBuilder.EnsureFolder(GreyboxFolder);
            Material wallMat = Mat("GB_Wall", new Color(0.78f, 0.78f, 0.76f));
            Material trimMat = Mat("GB_Trim", new Color(0.45f, 0.45f, 0.48f));
            Material floorMat = Mat("GB_Floor", new Color(0.55f, 0.52f, 0.48f));
            Material ceilingMat = Mat("GB_Ceiling", new Color(0.62f, 0.62f, 0.66f));
            Material propMat = Mat("GB_Prop", new Color(0.95f, 0.6f, 0.25f));

            float jambW = (W - OpeningW) / 2f;
            float jambX = OpeningW / 2f + jambW / 2f;

            var palette = ScriptableObject.CreateInstance<PrefabPalette>();
            // Two variants of the walls and floors, to try the variant lists.
            palette.walls = new List<GameObject>
            {
                Save("GB_Mur", go => Box(go, new Vector3(0f, H / 2f, 0f), new Vector3(W, H, T), wallMat)),
                Save("GB_Mur_Sombre", go => Box(go, new Vector3(0f, H / 2f, 0f), new Vector3(W, H, T), trimMat)),
            };
            palette.doors = new List<GameObject>
            {
                Save("GB_Porte", go =>
                {
                    Box(go, new Vector3(-jambX, H / 2f, 0f), new Vector3(jambW, H, T), wallMat);
                    Box(go, new Vector3(jambX, H / 2f, 0f), new Vector3(jambW, H, T), wallMat);
                    Box(go, new Vector3(0f, (DoorH + H) / 2f, 0f), new Vector3(OpeningW, H - DoorH, T), wallMat);
                }),
            };
            palette.windows = new List<GameObject>
            {
                Save("GB_Fenetre", go =>
                {
                    Box(go, new Vector3(-jambX, H / 2f, 0f), new Vector3(jambW, H, T), wallMat);
                    Box(go, new Vector3(jambX, H / 2f, 0f), new Vector3(jambW, H, T), wallMat);
                    Box(go, new Vector3(0f, SillH / 2f, 0f), new Vector3(OpeningW, SillH, T), wallMat);
                    Box(go, new Vector3(0f, (DoorH + H) / 2f, 0f), new Vector3(OpeningW, H - DoorH, T), wallMat);
                }),
            };
            palette.pillars = new List<GameObject>
            {
                Save("GB_Poteau", go => Box(go, new Vector3(0f, H / 2f, 0f), new Vector3(0.3f, H, 0.3f), trimMat)),
            };
            palette.floorTiles = new List<GameObject>
            {
                Save("GB_Dalle_Sol", go => Box(go, new Vector3(0f, -0.05f, 0f), new Vector3(W, 0.1f, W), floorMat)),
                Save("GB_Dalle_Sol_Sombre", go => Box(go, new Vector3(0f, -0.05f, 0f), new Vector3(W, 0.1f, W), trimMat)),
            };
            palette.ceilingTiles = new List<GameObject>
            {
                Save("GB_Dalle_Plafond", go => Box(go, new Vector3(0f, 0.05f, 0f), new Vector3(W, 0.1f, W), ceilingMat)),
            };

            palette.props = new List<GameObject>
            {
                Save("GB_Caisse", go => Box(go, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), propMat)),
                Save("GB_Tonneau", go => Cylinder(go, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), propMat)),
                Save("GB_Table", go =>
                {
                    Box(go, new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.05f, 0.8f), propMat);
                    foreach (float x in new[] { -0.55f, 0.55f })
                        foreach (float z in new[] { -0.35f, 0.35f })
                            Box(go, new Vector3(x, 0.36f, z), new Vector3(0.06f, 0.72f, 0.06f), trimMat);
                }),
                Save("GB_Tabouret", go =>
                {
                    Cylinder(go, new Vector3(0f, 0.72f, 0f), new Vector3(0.38f, 0.03f, 0.38f), propMat);
                    Box(go, new Vector3(0f, 0.35f, 0f), new Vector3(0.06f, 0.7f, 0.06f), trimMat);
                }),
                Save("GB_Comptoir", go => Box(go, new Vector3(0f, 0.55f, 0f), new Vector3(2f, 1.1f, 0.6f), trimMat)),
                Save("GB_Etagere", go => Box(go, new Vector3(0f, 1f, 0f), new Vector3(1.2f, 2f, 0.4f), trimMat)),
            };

            AddGreyboxLevelPieces(palette);
            AssetDatabase.CreateAsset(palette, palettePath);
            AssetDatabase.SaveAssets();
            return palette;
        }

        // Stairs one level high (15 steps of 0.2 m, 0.3 m deep: about 34°,
        // under the character controller's 45°) and a 1 m railing.
        private static void AddGreyboxLevelPieces(PrefabPalette palette)
        {
            Material trimMat = Mat("GB_Trim", new Color(0.45f, 0.45f, 0.48f));
            Material propMat = Mat("GB_Prop", new Color(0.95f, 0.6f, 0.25f));
            if (palette.stairs.Count == 0)
                palette.stairs.Add(Save("GB_Escalier", go =>
                {
                    const int steps = 15;
                    float rise = H / steps, tread = 0.3f;
                    for (int i = 0; i < steps; i++)
                        Box(go, new Vector3(0f, rise * (i + 1) / 2f, tread * (i + 0.5f)), new Vector3(1.2f, rise * (i + 1), tread), trimMat);
                }));
            if (palette.railings.Count == 0)
                palette.railings.Add(Save("GB_GardeCorps", go =>
                {
                    Box(go, new Vector3(0f, 0.975f, 0f), new Vector3(W, 0.05f, 0.08f), propMat);
                    foreach (float x in new[] { -W / 2f + 0.03f, 0f, W / 2f - 0.03f })
                        Box(go, new Vector3(x, 0.5f, 0f), new Vector3(0.05f, 1f, 0.05f), propMat);
                }));
        }

        // Points at the Forest Bar prefabs in place (the plugin folder is
        // left untouched). Despite the folder name, its "HDRP" materials were
        // converted to URP Lit (166 of 181), so they render as is. Its wall
        // pieces run along their local Z (found by the
        // palette's Auto wall axis); Flip Walls if they face the wrong way.
        public static PrefabPalette CreateForestBar()
        {
            string folder = KitsFolder + "/ForestBar";
            string palettePath = folder + "/Palette_ForestBar.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PrefabPalette>(palettePath);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(ForestBarPrefabs))
            {
                Debug.LogWarning($"[Level Maker] Forest Bar introuvable ({ForestBarPrefabs}).");
                return null;
            }

            LevelBuilder.EnsureFolder(folder);
            var palette = ScriptableObject.CreateInstance<PrefabPalette>();
            // Chosen by name, unseen: Diagnostiquer la palette tells which
            // ones don't share the wall's size.
            palette.walls = ForestBarList("Kit_BricksPlaster", "Kit_BricksEmpty", "Kit_BricksOldWood", "Kit_WallBlockConcrete");
            palette.doors = ForestBarList("Kit_BricksDoor", "Kit_BricksDoorOldWood", "Kit_BricksDoubleDoor");
            palette.windows = ForestBarList("Kit_BricksWindow", "Kit_BricksWindowTwo", "Kit_BricksOldWoodWindowOpen");
            palette.pillars = ForestBarList("Kit_BricksThinColon", "Kit_BricksThickColon", "Kit_WoodSmallColon");
            palette.floorTiles = ForestBarList("Kit_RusticFloor", "Kit_StoneFloor", "Kit_WoodGround", "Kit_Concrete");
            palette.ceilingTiles = ForestBarList("Kit_CeilingWood", "Kit_CeilingWoodPlanks", "Kit_CeilingMetalRusted");
            palette.props = new List<GameObject>();
            foreach (string name in new[]
                     {
                         "Prop_BarCounter", "Prop_BarStool", "Prop_RoundTable", "Prop_Chair", "Prop_Wood_Table",
                         "Prop_HighTableStool_Table", "Prop_HighTableStool_Chair", "Prop_Sofa_1", "Prop_LeatherSeat",
                         "Prop_BeerKeg", "Prop_WhiskeyBarrel", "Prop_Shelf", "Prop_OverheadLamp", "Prop_Speaker_Large",
                         "Prop_GameMachine", "Prop_Television", "Prop_DrumKit", "Prop_Flowerpot", "Plant_Monstera_Deliciosa",
                         "Prop_CardboardBox_1", "Prop_WoodPallet", "Prop_Rug",
                     })
            {
                GameObject prefab = ForestBar(name);
                if (prefab != null) palette.props.Add(prefab);
            }

            AssetDatabase.CreateAsset(palette, palettePath);
            AssetDatabase.SaveAssets();
            return palette;
        }

        // ---------- Filling a palette from a folder ----------

        private static readonly string[] SkipWords = { "decal", "cutout", "fx", "particle", "smoke", "fog", "flame", "spline", "lightning", "sea" };
        private static readonly string[] DoorWords = { "door", "porte" };
        private static readonly string[] WindowWords = { "window", "fenetre", "fenêtre" };
        private static readonly string[] WallWords = { "wall", "mur" };
        private static readonly string[] FloorWords = { "floor", "ground", "tile", "sol", "dalle" };
        private static readonly string[] CeilingWords = { "ceiling", "roof", "plafond", "toit" };
        private static readonly string[] PillarWords = { "pillar", "column", "colon", "poteau", "colonne", "post" };
        private static readonly string[] StairsWords = { "stair", "escalier" };
        private static readonly string[] RailingWords = { "railing", "rail", "fence", "barrier", "balustrade", "garde" };

        // Sorts every prefab of a folder (subfolders included) into the
        // palette's lists, by name first (English or French), by measured
        // shape otherwise. Wall pieces only count as walls, doors or windows
        // when they have the module width (the most common width among
        // them): a corner or a double-width piece can't be laid on the
        // trace, it goes to the props. Adds to what's there, never removes.
        // Returns a report; prefabs whose materials can't render in this
        // pipeline (pink) are counted.
        public static string FillFromFolder(PrefabPalette palette, string folder)
        {
            RoomBuilder.ClearMeasures();
            var prefabs = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null) prefabs.Add(prefab);
            }

            var walls = new List<GameObject>();
            var doors = new List<GameObject>();
            var windows = new List<GameObject>();
            var floors = new List<GameObject>();
            var ceilings = new List<GameObject>();
            var pillars = new List<GameObject>();
            var stairs = new List<GameObject>();
            var railings = new List<GameObject>();
            var props = new List<GameObject>();
            int skipped = 0, pink = 0;

            foreach (GameObject prefab in prefabs)
            {
                string n = prefab.name.ToLowerInvariant();
                if (Has(n, SkipWords) || !RoomBuilder.TryMeasure(prefab, out Bounds b)) { skipped++; continue; }
                if (!RendersInThisPipeline(prefab)) pink++;

                Vector3 s = b.size;
                float length = Mathf.Max(s.x, s.z), depth = Mathf.Min(s.x, s.z);
                bool flat = s.y < 0.25f * length && length > 0.5f;
                bool wallShape = s.y > 1.5f && length > 1f && depth < 0.35f * length;
                bool pillarShape = s.y > 1.5f && length < 0.8f;

                // Stairs: named so and climbing at least a meter (not a single step).
                if (Has(n, StairsWords) && s.y > 1f && length > 1f) stairs.Add(prefab);
                else if (Has(n, RailingWords) && !Has(n, PillarWords) && s.y < 1.6f && length > 0.5f) railings.Add(prefab);
                else if (Has(n, DoorWords) && (Has(n, WallWords) || wallShape)) doors.Add(prefab);
                else if (Has(n, WindowWords) && (Has(n, WallWords) || wallShape)) windows.Add(prefab);
                else if (Has(n, CeilingWords) && flat) ceilings.Add(prefab);
                else if (Has(n, FloorWords) && flat) floors.Add(prefab);
                else if (Has(n, PillarWords)) pillars.Add(prefab);
                else if (Has(n, WallWords) && wallShape) walls.Add(prefab);
                else if (Has(n, DoorWords) || Has(n, WindowWords) || Has(n, WallWords) || Has(n, FloorWords) || Has(n, CeilingWords)) props.Add(prefab);
                else if (wallShape) walls.Add(prefab);
                else if (pillarShape) pillars.Add(prefab);
                else props.Add(prefab);
            }

            // Module width: the most common wall length (to 5 cm).
            float module = palette.moduleWidthOverride > 0f ? palette.moduleWidthOverride
                : walls.Count == 0 ? 0f
                : walls.GroupBy(w => Mathf.Round(Length(w) * 20f) / 20f).OrderByDescending(g => g.Count()).First().Key;
            int offModule = 0;
            if (module > 0f)
                foreach (List<GameObject> list in new[] { walls, doors, windows, railings })
                    for (int i = list.Count - 1; i >= 0; i--)
                        if (Mathf.Abs(Length(list[i]) - module) > module * 0.05f)
                        {
                            props.Add(list[i]);
                            list.RemoveAt(i);
                            offModule++;
                        }

            Undo.RecordObject(palette, "Remplir la palette");
            int added = AddAll(palette.walls, walls) + AddAll(palette.doors, doors) + AddAll(palette.windows, windows)
                        + AddAll(palette.floorTiles, floors) + AddAll(palette.ceilingTiles, ceilings)
                        + AddAll(palette.pillars, pillars) + AddAll(palette.stairs, stairs) + AddAll(palette.railings, railings)
                        + AddAll(palette.props, props);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();

            string report = $"{prefabs.Count} prefabs dans {folder} : {added} ajoutés — murs {walls.Count}, portes {doors.Count}, " +
                            $"fenêtres {windows.Count}, sols {floors.Count}, plafonds {ceilings.Count}, poteaux {pillars.Count}, " +
                            $"escaliers {stairs.Count}, garde-corps {railings.Count}, props {props.Count}" +
                            $"{(module > 0f ? $" (module {module:0.##} m ; {offModule} pièce(s) de mur d'une autre largeur mises en props)" : "")}" +
                            $" ; {skipped} ignorés (effets, decals, sans mesh).";
            if (pink > 0)
                report += $"\n⚠ {pink} prefab(s) ont des matériaux que ce projet (URP) ne sait pas afficher : ils seront roses. " +
                          "Importe la version URP du pack et remplis la palette depuis celle-ci.";
            Debug.Log($"[Level Maker] Palette {palette.name} : {report}", palette);
            return report;
        }

        private static bool Has(string name, string[] words) => words.Any(w => name.Contains(w));

        private static float Length(GameObject prefab) =>
            RoomBuilder.TryMeasure(prefab, out Bounds b) ? Mathf.Max(b.size.x, b.size.z) : 0f;

        private static int AddAll(List<GameObject> target, List<GameObject> items)
        {
            int added = 0;
            foreach (GameObject item in items)
                if (!target.Contains(item)) { target.Add(item); added++; }
            return added;
        }

        // A material whose shader isn't supported here (HDRP Shader Graph in
        // URP…) renders with Unity's pink error shader.
        private static bool RendersInThisPipeline(GameObject prefab)
        {
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material mat in renderer.sharedMaterials)
                    if (mat != null && (mat.shader == null || !mat.shader.isSupported || mat.shader.name == "Hidden/InternalErrorShader"))
                        return false;
            return true;
        }

        private static List<GameObject> ForestBarList(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (string name in names)
            {
                GameObject prefab = ForestBar(name);
                if (prefab != null) list.Add(prefab);
            }
            return list;
        }

        private static GameObject ForestBar(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{ForestBarPrefabs}/{name}.prefab");

        // ---------- Greybox helpers ----------

        private static Material Mat(string name, Color color)
        {
            string path = $"{GreyboxFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject Save(string name, System.Action<GameObject> build)
        {
            string path = $"{GreyboxFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = new GameObject(name);
            build(go);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void Box(GameObject parent, Vector3 center, Vector3 size, Material mat) =>
            Primitive(PrimitiveType.Cube, parent, center, size, mat);

        // A Unity cylinder is 2 units high: half-height in size.y.
        private static void Cylinder(GameObject parent, Vector3 center, Vector3 size, Material mat) =>
            Primitive(PrimitiveType.Cylinder, parent, center, size, mat);

        private static void Primitive(PrimitiveType type, GameObject parent, Vector3 center, Vector3 size, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = center;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
#endif
