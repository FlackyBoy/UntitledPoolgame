#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Palettes the Level Maker can create itself: a greybox kit (grey blocks at
    // real kit sizes) and one pointing at the Forest Bar. Never overwrites.
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
                // Palettes made before stairs or the furniture existed get theirs.
                bool missingFurniture = FurnitureNames.Any(n => !existing.props.Exists(p => p != null && p.name == n));
                if (existing.stairs.Count == 0 || existing.railings.Count == 0 || existing.lights.Count == 0 || missingFurniture)
                {
                    AddGreyboxLevelPieces(existing);
                    AddGreyboxFurniture(existing, overwrite: false);
                    AddGreyboxLights(existing, overwrite: false);
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
            };

            AddGreyboxLevelPieces(palette);
            AddGreyboxFurniture(palette, overwrite: false);
            AddGreyboxLights(palette, overwrite: false);
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

        private static readonly string[] FurnitureNames =
            { "GB_Table", "GB_TableCarree", "GB_TableHaute", "GB_TableBasse", "GB_TableBasse_Ronde", "GB_Chaise", "GB_Tabouret", "GB_Comptoir", "GB_ComptoirMural", "GB_Etagere",
              "GB_Estrade", "GB_Estrade_Demi", "GB_Estrade_Marches", "GB_Estrade_Marches_1m",
              "GB_FauteuilClub", "GB_Canape_2Places", "GB_Canape_3Places",
              "GB_BabyFoot", "GB_Flechettes", "GB_Flechettes_Ligne", "GB_BorneArcade" };

        // Stage 60 cm high, reached by two 20 cm steps (under the character
        // controller's 30 cm step offset); its edge can't be stepped onto.
        private const float StageH = 0.6f, StepRise = 0.2f, StepTread = 0.3f;

        // Overwrites the furniture prefabs with the current shapes. Overwriting
        // keeps their GUIDs: instances already placed in scenes update.
        [MenuItem("Tools/Pool/Régénérer le mobilier greybox", priority = 3)]
        private static void RegenerateFurniture()
        {
            PrefabPalette palette = CreateGreybox();
            AddGreyboxFurniture(palette, overwrite: true);
            AddGreyboxLights(palette, overwrite: true);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(palette);
            Debug.Log($"[Level Maker] Mobilier greybox régénéré dans {GreyboxFolder} ({FurnitureNames.Length} prefabs).", palette);
        }

        // Bar furniture placeholders at real sizes (meters), base at y = 0.
        // Seats and shelves face +Z; the counter's customer side is +Z.
        private static void AddGreyboxFurniture(PrefabPalette palette, bool overwrite)
        {
            Material trimMat = Mat("GB_Trim", new Color(0.45f, 0.45f, 0.48f));
            Material propMat = Mat("GB_Prop", new Color(0.95f, 0.6f, 0.25f));
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);

            var furniture = new[]
            {
                Save("GB_Table", go =>
                {
                    Box(go, new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.05f, 0.8f), propMat);
                    foreach (float x in new[] { -0.55f, 0.55f })
                        foreach (float z in new[] { -0.35f, 0.35f })
                            Box(go, new Vector3(x, 0.36f, z), new Vector3(0.06f, 0.72f, 0.06f), trimMat);
                }, overwrite),
                // 80 x 80 cm, 76 cm high, with an apron under the top.
                Save("GB_TableCarree", go =>
                {
                    Box(go, new Vector3(0f, 0.74f, 0f), new Vector3(0.8f, 0.04f, 0.8f), propMat);
                    foreach (float x in new[] { -0.35f, 0.35f })
                        foreach (float z in new[] { -0.35f, 0.35f })
                            Box(go, new Vector3(x, 0.36f, z), new Vector3(0.05f, 0.72f, 0.05f), trimMat);
                    foreach (float side in new[] { -0.35f, 0.35f })
                    {
                        Box(go, new Vector3(0f, 0.67f, side), new Vector3(0.7f, 0.08f, 0.02f), trimMat);
                        Box(go, new Vector3(side, 0.67f, 0f), new Vector3(0.02f, 0.08f, 0.7f), trimMat);
                    }
                }, overwrite),
                // Round standing table, 70 cm across, 1.06 m high.
                Save("GB_TableHaute", go =>
                {
                    Cylinder(go, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.02f, 0.5f), trimMat);
                    Cylinder(go, new Vector3(0f, 0.52f, 0f), new Vector3(0.07f, 0.5f, 0.07f), trimMat);
                    Cylinder(go, new Vector3(0f, 1.04f, 0f), new Vector3(0.7f, 0.02f, 0.7f), propMat);
                }, overwrite),
                // Coffee table for the club seats: 1.1 × 0.6 m, 42 cm high,
                // with a shelf underneath.
                Save("GB_TableBasse", go =>
                {
                    Box(go, new Vector3(0f, 0.405f, 0f), new Vector3(1.1f, 0.03f, 0.6f), propMat);
                    Box(go, new Vector3(0f, 0.12f, 0f), new Vector3(1f, 0.02f, 0.5f), propMat);
                    foreach (float x in new[] { -0.51f, 0.51f })
                        foreach (float z in new[] { -0.26f, 0.26f })
                            Box(go, new Vector3(x, 0.195f, z), new Vector3(0.05f, 0.39f, 0.05f), trimMat);
                }, overwrite),
                // Round coffee table, 80 cm across, 42 cm high, on a drum base.
                Save("GB_TableBasse_Ronde", go =>
                {
                    Cylinder(go, new Vector3(0f, 0.015f, 0f), new Vector3(0.5f, 0.015f, 0.5f), trimMat);
                    Cylinder(go, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.17f, 0.3f), trimMat);
                    Cylinder(go, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.02f, 0.8f), propMat);
                }, overwrite),
                // Seat at 48 cm, backrest on the -Z side up to 88 cm.
                Save("GB_Chaise", go =>
                {
                    foreach (float x in new[] { -0.19f, 0.19f })
                    {
                        foreach (float z in new[] { -0.19f, 0.19f })
                            Box(go, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.44f, 0.04f), trimMat);
                        Box(go, new Vector3(x, 0.68f, -0.19f), new Vector3(0.04f, 0.4f, 0.04f), trimMat);
                    }
                    Box(go, new Vector3(0f, 0.46f, 0f), new Vector3(0.44f, 0.04f, 0.44f), propMat);
                    Box(go, new Vector3(0f, 0.78f, -0.19f), new Vector3(0.42f, 0.18f, 0.03f), propMat);
                }, overwrite),
                // Bar stool: seat at 77 cm, footrest ring at 30 cm.
                Save("GB_Tabouret", go =>
                {
                    Cylinder(go, new Vector3(0f, 0.015f, 0f), new Vector3(0.4f, 0.015f, 0.4f), trimMat);
                    Cylinder(go, new Vector3(0f, 0.37f, 0f), new Vector3(0.05f, 0.35f, 0.05f), trimMat);
                    foreach (float side in new[] { -0.15f, 0.15f })
                    {
                        Box(go, new Vector3(side, 0.3f, 0f), new Vector3(0.025f, 0.025f, 0.325f), trimMat);
                        Box(go, new Vector3(0f, 0.3f, side), new Vector3(0.325f, 0.025f, 0.025f), trimMat);
                    }
                    Cylinder(go, new Vector3(0f, 0.74f, 0f), new Vector3(0.38f, 0.03f, 0.38f), propMat);
                }, overwrite),
                // 2 m long, 1.1 m high: recessed plinth, body, overhanging top,
                // foot rail on the customer side.
                Save("GB_Comptoir", go =>
                {
                    Box(go, new Vector3(0f, 0.05f, 0f), new Vector3(1.88f, 0.1f, 0.48f), trimMat);
                    Box(go, new Vector3(0f, 0.575f, 0f), new Vector3(1.94f, 0.95f, 0.54f), propMat);
                    Box(go, new Vector3(0f, 1.075f, 0f), new Vector3(2f, 0.05f, 0.6f), trimMat);
                    Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 0.2f, 0.36f), new Vector3(0.05f, 0.9f, 0.05f), trimMat, alongX);
                    foreach (float x in new[] { -0.8f, 0.8f })
                        Box(go, new Vector3(x, 0.2f, 0.315f), new Vector3(0.03f, 0.03f, 0.09f), trimMat);
                }, overwrite),
                // Standing ledge fixed to a wall (wall side -Z): 1.6 m plank
                // at 1.05 m on brackets, foot rail. The panel behind it goes
                // down to the floor so the Props tool, which sets the lowest
                // renderer on the clicked surface, keeps the ledge's height.
                Save("GB_ComptoirMural", go =>
                {
                    Box(go, new Vector3(0f, 0.515f, -0.16f), new Vector3(1.6f, 1.03f, 0.02f), trimMat);
                    Box(go, new Vector3(0f, 1.05f, 0f), new Vector3(1.6f, 0.04f, 0.3f), propMat);
                    foreach (float x in new[] { -0.65f, 0.65f })
                    {
                        Box(go, new Vector3(x, 0.89f, -0.135f), new Vector3(0.03f, 0.28f, 0.03f), trimMat);
                        Primitive(PrimitiveType.Cube, go, new Vector3(x, 0.89f, -0.0175f), new Vector3(0.03f, 0.366f, 0.03f), trimMat,
                            Quaternion.Euler(40f, 0f, 0f));
                    }
                    Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 0.2f, 0.2f), new Vector3(0.05f, 0.75f, 0.05f), trimMat, alongX);
                    foreach (float x in new[] { -0.6f, 0.6f })
                        Box(go, new Vector3(x, 0.2f, 0.02f), new Vector3(0.03f, 0.03f, 0.36f), trimMat);
                }, overwrite),
                // 1.2 m wide, 2 m high, 40 cm deep, open on +Z: sides, back,
                // four shelves and a top.
                Save("GB_Etagere", go =>
                {
                    foreach (float x in new[] { -0.585f, 0.585f })
                        Box(go, new Vector3(x, 1f, 0f), new Vector3(0.03f, 2f, 0.4f), trimMat);
                    Box(go, new Vector3(0f, 1f, -0.19f), new Vector3(1.14f, 2f, 0.02f), trimMat);
                    foreach (float y in new[] { 0.05f, 0.53f, 1.01f, 1.49f, 1.985f })
                        Box(go, new Vector3(0f, y, 0.01f), new Vector3(1.14f, 0.03f, 0.38f), propMat);
                }, overwrite),
            };
            foreach (GameObject prefab in furniture.Concat(StagePieces(trimMat, propMat, overwrite)).Concat(ClubSeats(trimMat, overwrite))
                         .Concat(BarGames(trimMat, propMat, overwrite)))
                if (!palette.props.Contains(prefab)) palette.props.Add(prefab);
        }

        // For a palette with no light (any kit): the Lights tool's button.
        public static void AddNeonTo(PrefabPalette palette)
        {
            LevelBuilder.EnsureFolder(GreyboxFolder);
            AddGreyboxLights(palette, overwrite: false);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
        }

        // 1.2 m tube along X, +Y out of the surface it's mounted on (see
        // RoomBuilder.MountedTurn). Three lamps along it (see NeonLight), set
        // away from the surface by its Lamp Distance: their spots on the wall
        // merge into one long glow, closer they'd show as three.
        private static void AddGreyboxLights(PrefabPalette palette, bool overwrite)
        {
            Material trimMat = Mat("GB_Trim", new Color(0.45f, 0.45f, 0.48f));
            Material black = Mat("GB_Noir", new Color(0.08f, 0.08f, 0.09f));
            Material glow = Mat("GB_Neon", Color.white, "Universal Render Pipeline/Unlit");
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);

            GameObject neon = Save("GB_Neon", go =>
            {
                Box(go, new Vector3(0f, 0.01f, 0f), new Vector3(1.24f, 0.02f, 0.05f), trimMat);
                foreach (float x in new[] { -0.45f, 0.45f })
                    Box(go, new Vector3(x, 0.035f, 0f), new Vector3(0.03f, 0.03f, 0.05f), black);
                foreach (float x in new[] { -0.61f, 0.61f })
                    Primitive(PrimitiveType.Cylinder, go, new Vector3(x, 0.05f, 0f), new Vector3(0.05f, 0.015f, 0.05f), black, alongX);
                Renderer tube = Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 0.05f, 0f), new Vector3(0.04f, 0.6f, 0.04f), glow, alongX)
                    .GetComponent<Renderer>();
                tube.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Light Lamp(string lampName, float x)
                {
                    var lampObject = new GameObject(lampName);
                    lampObject.transform.SetParent(go.transform, false);
                    lampObject.transform.localPosition = new Vector3(x, 0.4f, 0f);
                    Light lamp = lampObject.AddComponent<Light>();
                    lamp.type = LightType.Point;
                    return lamp;
                }

                NeonLight light = go.AddComponent<NeonLight>();
                light.tubes = new[] { tube };
                light.lamp = Lamp("Lumiere", 0f);
                light.sideLamps = new[] { Lamp("Lumiere_G", -0.4f), Lamp("Lumiere_D", 0.4f) };
                light.Apply();
            }, overwrite);
            if (!palette.lights.Contains(neon)) palette.lights.Add(neon);
        }

        // Modular stage: 2 × 2 m and 2 × 1 m blocks (the greybox module) set
        // side by side, and steps set against an edge. The deck is inset 1 cm
        // per side so the joints between blocks show.
        private static GameObject[] StagePieces(Material trimMat, Material propMat, bool overwrite) => new[]
        {
            Save("GB_Estrade", go => StageBlock(go, W, W, trimMat, propMat), overwrite),
            Save("GB_Estrade_Demi", go => StageBlock(go, W, W / 2f, trimMat, propMat), overwrite),
            Save("GB_Estrade_Marches", go => StageSteps(go, W, trimMat, propMat), overwrite),
            Save("GB_Estrade_Marches_1m", go => StageSteps(go, 1f, trimMat, propMat), overwrite),
        };

        // Bar games. Baby-foot: played from the long sides (±Z); darts: on a
        // wall (wall side -Z), with the throw line as its own prop; arcade
        // cabinet: player on +Z.
        private static GameObject[] BarGames(Material trimMat, Material propMat, bool overwrite)
        {
            Material black = Mat("GB_Noir", new Color(0.08f, 0.08f, 0.09f));
            Material red = Mat("GB_Rouge", new Color(0.8f, 0.15f, 0.12f));
            Material blue = Mat("GB_Bleu", new Color(0.15f, 0.3f, 0.8f));
            Material green = Mat("GB_Vert", new Color(0.15f, 0.55f, 0.25f));
            Material screen = Mat("GB_Ecran", new Color(0.3f, 0.95f, 1f));
            Material cabinet = Mat("GB_Arcade", new Color(0.4f, 0.15f, 0.65f));
            Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);

            return new[]
            {
                // 1.2 × 0.7 m box at 94 cm, eight rods across, handles out to
                // ±65 cm. Rods 1, 2, 4, 6 (from -X) are red's, handled from -Z.
                Save("GB_BabyFoot", go =>
                {
                    foreach (float x in new[] { -0.5f, 0.5f })
                        foreach (float z in new[] { -0.25f, 0.25f })
                            Box(go, new Vector3(x, 0.35f, z), new Vector3(0.08f, 0.7f, 0.08f), trimMat);
                    Box(go, new Vector3(0f, 0.72f, 0f), new Vector3(1.2f, 0.04f, 0.7f), green);
                    foreach (float z in new[] { -0.335f, 0.335f })
                        Box(go, new Vector3(0f, 0.83f, z), new Vector3(1.2f, 0.22f, 0.03f), propMat);
                    foreach (float x in new[] { -0.585f, 0.585f })
                    {
                        Box(go, new Vector3(x, 0.83f, 0f), new Vector3(0.03f, 0.22f, 0.7f), propMat);
                        Box(go, new Vector3(x * 1.03f, 0.79f, 0f), new Vector3(0.02f, 0.1f, 0.22f), black);
                    }
                    int[] players = { 1, 2, 3, 5, 5, 3, 2, 1 };
                    bool[] redSide = { true, true, false, true, false, true, false, false };
                    for (int i = 0; i < players.Length; i++)
                    {
                        float x = -0.525f + 0.15f * i;
                        float side = redSide[i] ? -1f : 1f;
                        Primitive(PrimitiveType.Cylinder, go, new Vector3(x, 0.88f, 0f), new Vector3(0.016f, 0.55f, 0.016f), trimMat, alongZ);
                        Primitive(PrimitiveType.Cylinder, go, new Vector3(x, 0.88f, side * 0.6f), new Vector3(0.035f, 0.05f, 0.035f), black, alongZ);
                        for (int j = 0; j < players[i]; j++)
                        {
                            float z = players[i] == 1 ? 0f : -0.24f + 0.48f * j / (players[i] - 1);
                            Box(go, new Vector3(x, 0.84f, z), new Vector3(0.025f, 0.1f, 0.035f), redSide[i] ? red : blue);
                        }
                    }
                }, overwrite),
                // Board at the regulation 1.73 m, 45 cm across, in a frame on
                // a floor-to-2.2 m backboard (also what puts the prop on the
                // floor: the Props tool sets its lowest point on the surface).
                Save("GB_Flechettes", go =>
                {
                    Box(go, new Vector3(0f, 1.1f, -0.02f), new Vector3(0.9f, 2.2f, 0.04f), trimMat);
                    Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 1.73f, 0.015f), new Vector3(0.45f, 0.015f, 0.45f), black, alongZ);
                    Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 1.73f, 0.032f), new Vector3(0.34f, 0.003f, 0.34f), propMat, alongZ);
                    Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 1.73f, 0.034f), new Vector3(0.16f, 0.003f, 0.16f), green, alongZ);
                    Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 1.73f, 0.036f), new Vector3(0.04f, 0.003f, 0.04f), red, alongZ);
                    foreach (float y in new[] { 1.41f, 2.05f })
                        Box(go, new Vector3(0f, y, 0.05f), new Vector3(0.64f, 0.04f, 0.1f), propMat);
                    foreach (float x in new[] { -0.3f, 0.3f })
                        Box(go, new Vector3(x, 1.73f, 0.05f), new Vector3(0.04f, 0.6f, 0.1f), propMat);
                }, overwrite),
                // Throw line, to set 2.37 m from the board's face.
                Save("GB_Flechettes_Ligne", go => Box(go, new Vector3(0f, 0.005f, 0f), new Vector3(0.6f, 0.01f, 0.05f), red), overwrite),
                // 70 cm wide, 1.8 m high: cabinet, sloped control panel with a
                // stick and three buttons, tilted screen, lit marquee on top.
                Save("GB_BorneArcade", go =>
                {
                    foreach (float x in new[] { -0.33f, 0.33f })
                        Box(go, new Vector3(x, 0.9f, 0f), new Vector3(0.04f, 1.8f, 0.8f), cabinet);
                    Box(go, new Vector3(0f, 0.9f, -0.38f), new Vector3(0.62f, 1.8f, 0.04f), cabinet);
                    Box(go, new Vector3(0f, 0.42f, 0.1f), new Vector3(0.62f, 0.84f, 0.6f), cabinet);
                    Box(go, new Vector3(0f, 0.55f, 0.405f), new Vector3(0.2f, 0.15f, 0.01f), black);
                    Primitive(PrimitiveType.Cube, go, new Vector3(0f, 0.9f, 0.28f), new Vector3(0.62f, 0.04f, 0.3f), black, Quaternion.Euler(15f, 0f, 0f));
                    Cylinder(go, new Vector3(-0.12f, 0.95f, 0.27f), new Vector3(0.015f, 0.04f, 0.015f), trimMat);
                    Primitive(PrimitiveType.Sphere, go, new Vector3(-0.12f, 0.99f, 0.27f), new Vector3(0.045f, 0.045f, 0.045f), red);
                    for (int i = 0; i < 3; i++)
                        Cylinder(go, new Vector3(0.04f + 0.08f * i, 0.93f, 0.28f), new Vector3(0.04f, 0.01f, 0.04f), i == 1 ? blue : red);
                    Box(go, new Vector3(0f, 1.3f, -0.18f), new Vector3(0.62f, 0.6f, 0.36f), black);
                    Primitive(PrimitiveType.Cube, go, new Vector3(0f, 1.3f, 0.01f), new Vector3(0.54f, 0.44f, 0.02f), screen, Quaternion.Euler(-15f, 0f, 0f));
                    Box(go, new Vector3(0f, 1.69f, -0.1f), new Vector3(0.62f, 0.2f, 0.52f), propMat);
                }, overwrite),
            };
        }

        // Club armchair and sofas: low and deep, thick arms and back with
        // rolled tops, one seat cushion per place. Leather brown so they
        // read apart from the orange furniture. Back on -Z, facing +Z.
        private static GameObject[] ClubSeats(Material trimMat, bool overwrite)
        {
            Material leather = Mat("GB_Cuir", new Color(0.42f, 0.22f, 0.12f));
            return new[]
            {
                Save("GB_FauteuilClub", go => ClubSeat(go, 1, leather, trimMat), overwrite),
                Save("GB_Canape_2Places", go => ClubSeat(go, 2, leather, trimMat), overwrite),
                Save("GB_Canape_3Places", go => ClubSeat(go, 3, leather, trimMat), overwrite),
            };
        }

        // 60 cm per place between 16 cm arms, 85 cm deep, seat at 47 cm,
        // back up to 84 cm.
        private static void ClubSeat(GameObject go, int places, Material leather, Material trimMat)
        {
            const float place = 0.6f, arm = 0.16f, depth = 0.85f, back = 0.18f;
            float inner = places * place, total = inner + 2f * arm;
            float seatDepth = depth - back, seatZ = back / 2f, backZ = -depth / 2f + back / 2f;
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f), alongZ = Quaternion.Euler(90f, 0f, 0f);

            foreach (float x in new[] { -total / 2f + 0.06f, total / 2f - 0.06f })
                foreach (float z in new[] { -depth / 2f + 0.06f, depth / 2f - 0.06f })
                    Box(go, new Vector3(x, 0.025f, z), new Vector3(0.06f, 0.05f, 0.06f), trimMat);
            Box(go, new Vector3(0f, 0.2f, seatZ), new Vector3(inner, 0.3f, seatDepth), leather);
            for (int i = 0; i < places; i++)
                Box(go, new Vector3(-inner / 2f + place * (i + 0.5f), 0.41f, seatZ + 0.02f),
                    new Vector3(place - 0.02f, 0.12f, seatDepth - 0.06f), leather);
            Box(go, new Vector3(0f, 0.4f, backZ), new Vector3(total, 0.7f, back), leather);
            Primitive(PrimitiveType.Cylinder, go, new Vector3(0f, 0.75f, backZ), new Vector3(back, total / 2f, back), leather, alongX);
            foreach (float x in new[] { -(inner + arm) / 2f, (inner + arm) / 2f })
            {
                Box(go, new Vector3(x, 0.3f, seatZ), new Vector3(arm, 0.5f, seatDepth), leather);
                Primitive(PrimitiveType.Cylinder, go, new Vector3(x, 0.55f, seatZ), new Vector3(arm + 0.04f, seatDepth / 2f, arm + 0.04f), leather, alongZ);
            }
        }

        private static void StageBlock(GameObject go, float width, float depth, Material trimMat, Material propMat)
        {
            Box(go, new Vector3(0f, (StageH - 0.02f) / 2f, 0f), new Vector3(width, StageH - 0.02f, depth), trimMat);
            Box(go, new Vector3(0f, StageH - 0.01f, 0f), new Vector3(width - 0.02f, 0.02f, depth - 0.02f), propMat);
        }

        // Climbs toward -Z: the stage goes on the -Z side, the way up faces +Z.
        private static void StageSteps(GameObject go, float width, Material trimMat, Material propMat)
        {
            int steps = Mathf.RoundToInt(StageH / StepRise) - 1;
            float depth = steps * StepTread;
            for (int i = 0; i < steps; i++)
            {
                float top = StepRise * (i + 1);
                float z = depth / 2f - StepTread * (i + 0.5f);
                Box(go, new Vector3(0f, (top - 0.02f) / 2f, z), new Vector3(width, top - 0.02f, StepTread), trimMat);
                Box(go, new Vector3(0f, top - 0.01f, z), new Vector3(width - 0.02f, 0.02f, StepTread - 0.02f), propMat);
            }
        }

        // Points at the Forest Bar prefabs in place. Its "HDRP" materials were
        // converted to URP. Walls run along local Z; Flip Walls if they face
        // the wrong way.
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

        // Sorts a folder's prefabs into the palette, by name (English or French)
        // then by shape. Wall pieces only count as walls, doors or windows at
        // the module width (the most common one); others go to props. Adds,
        // never removes. Returns a report, pink (unrenderable) prefabs counted.
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

        private static Material Mat(string name, Color color, string shaderName = "Universal Render Pipeline/Lit")
        {
            string path = $"{GreyboxFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject Save(string name, System.Action<GameObject> build, bool overwrite = false)
        {
            string path = $"{GreyboxFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite) return existing;
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

        private static GameObject Primitive(PrimitiveType type, GameObject parent, Vector3 center, Vector3 size, Material mat,
                                      Quaternion rotation = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = center;
            if (rotation != default) part.transform.localRotation = rotation;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = mat;
            return part;
        }
    }
}
#endif
