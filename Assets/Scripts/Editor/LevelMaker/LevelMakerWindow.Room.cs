#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // ② Salle: trace walls on a grid, turn wall modules into doors and
    // windows, fill floors and ceilings, paint props. Each tool shows the
    // palette's variants for what it places, as thumbnails: the selected one
    // is the one used. Rooms are rebuilt from their RoomOutline (which keeps
    // the chosen variants), so changing kit keeps the layout.
    public partial class LevelMakerWindow
    {
        private enum RoomTool { None, Walls, Openings, Floors, Stairs, Props }

        private static readonly string[] RoomToolNames = { "Aucun", "Murs", "Ouvertures", "Sol / plafond", "Escaliers", "Props" };
        private static readonly float[] GridSteps = { 0.25f, 0.5f, 1f, 2f };
        private static readonly string[] GridStepNames = { "0,25 m", "0,5 m", "1 m", "2 m" };
        private static readonly RoomModuleKind[] OpeningKinds =
            { RoomModuleKind.Door, RoomModuleKind.Window, RoomModuleKind.Wall, RoomModuleKind.Railing, RoomModuleKind.None };
        private static readonly string[] OpeningNames = { "Porte", "Fenêtre", "Mur plein", "Garde-corps", "Vide" };

        private RoomTool roomTool;
        private readonly List<Vector3> trace = new List<Vector3>();
        private int openingBrush;
        // Selected variant of each kind (index in the palette's list; -1 pillar = none).
        private int wallBrush, pillarBrush, doorBrush, windowBrush, floorBrush, ceilingBrush, stairsBrush, railingBrush;
        private bool brushCeiling;
        private Vector2 wallScroll, pillarScroll, openingScroll, floorScroll, ceilingScroll, stairsScroll;
        // Level being traced on (0 = ground floor) and the stairs' climbing direction.
        private int activeLevel;
        private float stairsYaw;
        private bool insertMode;   // Openings: nest the piece in the clicked module instead of replacing it
        private bool floorOnlyTrace;   // Walls tool: trace a floor zone with no walls

        // Length the trace snaps to: the wall's width, or the floor tile's
        // for a floor-only zone (whole tiles, no stretching).
        private float TraceStep => floorOnlyTrace
            ? Mathf.Max(0.05f, RoomBuilder.TileSize(Palette.FloorTile(floorBrush)).x)
            : RoomBuilder.ModuleWidth(Palette, wallBrush);

        private float TraceY => settings.floorHeight + activeLevel * RoomBuilder.StoreyHeight(Palette, wallBrush);
        private RoomModule hoveredModule;
        private int propIndex;
        private bool propRandomYaw = true;
        private float propYaw;
        private float propMinScale = 1f, propMaxScale = 1f;
        private bool propAlignToSurface;
        private bool showGrid = true;
        private bool paletteFoldout;
        private Editor paletteEditor;
        private Vector2 propScroll;

        private PrefabPalette Palette => settings.palette;

        private void StopRoomTool()
        {
            roomTool = RoomTool.None;
            trace.Clear();
            hoveredModule = null;
        }

        // ---------- Window ----------

        private void DrawRoomStep()
        {
            EditorGUILayout.LabelField("Kit", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(settingsSo.FindProperty("palette"), new GUIContent("Palette"));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Palette greybox (générée)")) UsePalette(KitGenerator.CreateGreybox());
                if (GUILayout.Button("Palette Forest Bar")) UsePalette(KitGenerator.CreateForestBar());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Nouvelle palette…")) CreateEmptyPalette();
                using (new EditorGUI.DisabledScope(Palette == null))
                    if (GUILayout.Button("Remplir depuis un dossier…")) FillPaletteFromFolder();
            }
            if (Palette == null)
            {
                EditorGUILayout.HelpBox("Choisis ou crée une palette pour construire.", MessageType.Info);
                return;
            }
            paletteFoldout = EditorGUILayout.Foldout(paletteFoldout, "Modifier la palette", true);
            if (paletteFoldout)
            {
                Editor.CreateCachedEditor(Palette, null, ref paletteEditor);
                using (new EditorGUI.IndentLevelScope()) paletteEditor.OnInspectorGUI();
            }
            EditorGUILayout.LabelField($"Module : {RoomBuilder.ModuleWidth(Palette, wallBrush):0.##} m   Hauteur des murs : {RoomBuilder.WallHeight(Palette, wallBrush):0.##} m",
                EditorStyles.miniLabel);
            if (GUILayout.Button("Diagnostiquer la palette (Console)")) RoomBuilder.LogPalette(Palette);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grille", EditorStyles.boldLabel);
            int gridIndex = Mathf.Max(0, System.Array.IndexOf(GridSteps, settings.gridStep));
            int newGrid = EditorGUILayout.Popup("Pas", gridIndex, GridStepNames);
            if (newGrid != gridIndex) settingsSo.FindProperty("gridStep").floatValue = GridSteps[newGrid];
            EditorGUILayout.PropertyField(settingsSo.FindProperty("floorHeight"), new GUIContent("Hauteur du sol"));
            int level = EditorGUILayout.IntSlider(new GUIContent("Étage actif", "Étage sur lequel on trace (0 = rez-de-chaussée). " +
                "Un étage = la hauteur des murs du kit."), activeLevel, 0, 5);
            if (level != activeLevel) { activeLevel = level; trace.Clear(); SceneView.RepaintAll(); }
            EditorGUILayout.LabelField(" ", $"Tracé à {TraceY:0.##} m (étage de {RoomBuilder.StoreyHeight(Palette, wallBrush):0.##} m)", EditorStyles.miniLabel);
            showGrid = EditorGUILayout.Toggle("Afficher la grille", showGrid);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Outil (vue Scène)", EditorStyles.boldLabel);
            var tool = (RoomTool)GUILayout.Toolbar((int)roomTool, RoomToolNames, GUILayout.Height(24));
            if (tool != roomTool)
            {
                trace.Clear();
                hoveredModule = null;
                roomTool = tool;
                SceneView.RepaintAll();
            }
            switch (roomTool)
            {
                case RoomTool.Walls: DrawWallToolOptions(); break;
                case RoomTool.Openings: DrawOpeningToolOptions(); break;
                case RoomTool.Floors: DrawFloorToolOptions(); break;
                case RoomTool.Stairs: DrawStairsToolOptions(); break;
                case RoomTool.Props: DrawPropToolOptions(); break;
            }

            EditorGUILayout.Space();
            DrawRoomList();
        }

        private void DrawStairsToolOptions()
        {
            stairsBrush = VariantGrid("Escalier", Palette.stairs, stairsBrush, ref stairsScroll);
            stairsYaw = EditorGUILayout.Slider("Sens de montée (R : +90°)", stairsYaw, 0f, 360f);
            EditorGUILayout.HelpBox("Clic sur le sol d'une salle fermée : pose un escalier qui monte vers l'étage au-dessus " +
                                    "(la flèche montre le sens de montée ; il perce le sol de l'étage et reçoit une rampe invisible). " +
                                    "Maj + clic : retire l'escalier le plus proche. Si la flèche pointe vers le bas des marches, " +
                                    "coche « Stairs Reversed » dans la palette.", MessageType.None);
        }

        // Thumbnails of a palette list; the selected index is the variant
        // used. withNone adds a first "Aucun" entry (index -1).
        private int VariantGrid(string label, List<GameObject> list, int selected, ref Vector2 scroll, bool withNone = false)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            var entries = new List<GUIContent>();
            if (withNone) entries.Add(new GUIContent("Aucun"));
            foreach (GameObject prefab in list)
                entries.Add(prefab == null ? new GUIContent("(vide)")
                    : new GUIContent(AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab), prefab.name));
            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("  (aucune variante dans la palette)", EditorStyles.miniLabel);
                return selected;
            }

            const int size = 64;
            int columns = Mathf.Max(1, (int)((position.width - 30) / (size + 6)));
            int rows = Mathf.CeilToInt(entries.Count / (float)columns);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(2, rows) * (size + 4) + 8));
            int offset = withNone ? 1 : 0;
            int index = Mathf.Clamp(selected + offset, 0, entries.Count - 1);
            index = GUILayout.SelectionGrid(index, entries.ToArray(), columns,
                GUILayout.Width(columns * (size + 6)), GUILayout.Height(rows * (size + 4)));
            EditorGUILayout.EndScrollView();
            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();

            int variant = index - offset;
            string name = variant < 0 ? "Aucun" : list[variant] != null ? list[variant].name : "(vide)";
            EditorGUILayout.LabelField($"  {name}", EditorStyles.miniLabel);
            return variant;
        }

        private RoomOutline SelectedRoom =>
            Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<RoomOutline>() : null;

        // Applies the selected variants to the selected room, then rebuilds it.
        private void ApplyToSelectedRoom(string what, System.Action<RoomOutline> apply)
        {
            RoomOutline room = SelectedRoom;
            using (new EditorGUI.DisabledScope(room == null))
                if (GUILayout.Button(room != null ? $"Appliquer {what} à « {room.name} »" : $"Sélectionne une salle pour lui appliquer {what}"))
                {
                    Undo.RecordObject(room, "Variantes de la salle");
                    apply(room);
                    RoomBuilder.Build(room);
                    MarkDirty();
                }
        }

        private void CreateEmptyPalette()
        {
            LevelBuilder.EnsureFolder(KitGenerator.KitsFolder);
            string path = EditorUtility.SaveFilePanelInProject("Nouvelle palette", "Palette_", "asset",
                "Où enregistrer la palette ?", KitGenerator.KitsFolder);
            if (string.IsNullOrEmpty(path)) return;
            var palette = CreateInstance<PrefabPalette>();
            AssetDatabase.CreateAsset(palette, path);
            AssetDatabase.SaveAssets();
            UsePalette(palette);
        }

        // Fills the current palette from a folder of prefabs (sorted by
        // KitGenerator.FillFromFolder), then shows the result to correct.
        private void FillPaletteFromFolder()
        {
            string absolute = EditorUtility.OpenFolderPanel("Dossier de prefabs du kit", Application.dataPath, "");
            if (string.IsNullOrEmpty(absolute)) return;
            string dataPath = Application.dataPath.Replace('\\', '/');
            absolute = absolute.Replace('\\', '/');
            if (!absolute.StartsWith(dataPath))
            {
                EditorUtility.DisplayDialog("Level Maker", "Le dossier doit être dans Assets.", "OK");
                return;
            }
            string folder = "Assets" + absolute.Substring(dataPath.Length);
            string report = KitGenerator.FillFromFolder(Palette, folder);
            paletteFoldout = true;
            EditorUtility.DisplayDialog("Palette remplie", report + "\n\nVérifie le classement dans « Modifier la palette ».", "OK");
        }

        private void UsePalette(PrefabPalette palette)
        {
            if (palette == null) return;
            settingsSo.FindProperty("palette").objectReferenceValue = palette;
            settingsSo.ApplyModifiedProperties();
            EditorGUIUtility.PingObject(palette);
        }

        private void DrawWallToolOptions()
        {
            int previousWall = wallBrush;
            wallBrush = VariantGrid("Mur", Palette.walls, wallBrush, ref wallScroll);
            float width = RoomBuilder.ModuleWidth(Palette, wallBrush);
            // The floor and ceiling follow the wall: the tiles closest to its width.
            if (wallBrush != previousWall) MatchTilesToWall();
            EditorGUILayout.LabelField($"  Largeur {width:0.##} m — sol : {NameOf(Palette.floorTiles, floorBrush)}, " +
                                       $"plafond : {NameOf(Palette.ceilingTiles, ceilingBrush)} (choisis selon la largeur)", EditorStyles.miniLabel);
            pillarBrush = VariantGrid("Poteau aux angles", Palette.pillars, pillarBrush, ref pillarScroll, withNone: true);
            ApplyToSelectedRoom("ce mur, ce poteau et le sol / plafond assortis", room =>
            {
                // Cells set to the old room wall would otherwise become overrides.
                room.openings.RemoveAll(o => o.kind == RoomModuleKind.Wall && o.variant == wallBrush);
                room.wallVariant = wallBrush;
                room.pillarVariant = pillarBrush;
                room.floorVariant = floorBrush;
                room.ceilingVariant = ceilingBrush;
            });
            bool floorOnly = EditorGUILayout.ToggleLeft(new GUIContent("Sol seul (sans murs)",
                "Trace une zone de sol libre : remplie de la dalle de sol choisie (à sa taille), sans murs ni poteaux. " +
                "Le tracé s'aimante alors sur la taille de la dalle."), floorOnlyTrace);
            if (floorOnly != floorOnlyTrace) { floorOnlyTrace = floorOnly; trace.Clear(); SceneView.RepaintAll(); }
            if (floorOnlyTrace)
            {
                floorBrush = VariantGrid("Dalle de sol", Palette.floorTiles, floorBrush, ref floorScroll);
                EditorGUILayout.LabelField($"  Pas du tracé : {TraceStep:0.##} m (taille de la dalle)", EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox("Clic : poser un point (aimanté à la grille, angles par 45°, longueur en modules entiers ; Maj = angle libre). " +
                                    "Cliquer sur le premier point ferme la salle, en ajoutant un coin si besoin pour que chaque côté " +
                                    "reste un nombre entier de murs (en vert ; en rouge si c'est impossible). Entrée : terminer un tracé ouvert. " +
                                    "Retour arrière : retirer le dernier point. Échap : annuler.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(trace.Count < 2 || ClosingCorners() == null))
                    if (GUILayout.Button(floorOnlyTrace ? "Fermer la zone de sol" : "Fermer la salle")) FinishTrace(true);
                using (new EditorGUI.DisabledScope(trace.Count < 2 || floorOnlyTrace))
                    if (GUILayout.Button("Terminer (ouvert)")) FinishTrace(false);
                using (new EditorGUI.DisabledScope(trace.Count == 0))
                    if (GUILayout.Button("Annuler")) { trace.Clear(); SceneView.RepaintAll(); }
            }
            EditorGUILayout.LabelField($"{trace.Count} point(s)", EditorStyles.miniLabel);
        }

        private void DrawOpeningToolOptions()
        {
            openingBrush = GUILayout.Toolbar(openingBrush, OpeningNames);
            switch (OpeningKinds[openingBrush])
            {
                case RoomModuleKind.Door: doorBrush = VariantGrid("Porte", Palette.doors, doorBrush, ref openingScroll); break;
                case RoomModuleKind.Window: windowBrush = VariantGrid("Fenêtre", Palette.windows, windowBrush, ref openingScroll); break;
                case RoomModuleKind.Railing: railingBrush = VariantGrid("Garde-corps", Palette.railings, railingBrush, ref openingScroll); break;
                case RoomModuleKind.None: EditorGUILayout.LabelField("Retire le module : côté ouvert (passage, bord de mezzanine).", EditorStyles.miniLabel); break;
                default: wallBrush = VariantGrid("Mur", Palette.walls, wallBrush, ref openingScroll); break;
            }
            insertMode = EditorGUILayout.ToggleLeft(new GUIContent("Imbriquer dans le module cliqué",
                "Pose la pièce choisie à l'intérieur du module existant au lieu de le remplacer (ex. porte de cage dans son cadre). " +
                "« Vide » retire la pièce imbriquée."), insertMode);
            EditorGUILayout.HelpBox("Clic sur un module de mur : le remplace par la variante choisie ci-dessus " +
                                    "(une pièce plus large qu'un mur prend la place des murs voisins nécessaires, au lieu de les chevaucher). " +
                                    "Les côtés vides sont marqués d'une sphère blanche : clic dessus pour y poser le choix.",
                MessageType.None);
        }

        private void MatchTilesToWall()
        {
            float width = RoomBuilder.ModuleWidth(Palette, wallBrush);
            if (Palette.floorTiles.Count > 0) floorBrush = RoomBuilder.BestTileFor(Palette.floorTiles, width);
            if (Palette.ceilingTiles.Count > 0) ceilingBrush = RoomBuilder.BestTileFor(Palette.ceilingTiles, width);
        }

        private static string NameOf(List<GameObject> list, int index) =>
            index >= 0 && index < list.Count && list[index] != null ? list[index].name : "—";

        private int OpeningVariant => OpeningKinds[openingBrush] switch
        {
            RoomModuleKind.Door => doorBrush,
            RoomModuleKind.Window => windowBrush,
            RoomModuleKind.Railing => railingBrush,
            _ => wallBrush,
        };

        private void DrawFloorToolOptions()
        {
            floorBrush = VariantGrid("Sol", Palette.floorTiles, floorBrush, ref floorScroll);
            ceilingBrush = VariantGrid("Plafond", Palette.ceilingTiles, ceilingBrush, ref ceilingScroll);
            brushCeiling = EditorGUILayout.Toggle("Avec plafond", brushCeiling);
            ApplyToSelectedRoom("ce sol et ce plafond", ApplyFloorBrush);
            EditorGUILayout.HelpBox("Clic sur une salle fermée (mur, sol…) dans la vue Scène : lui applique ce sol" +
                                    " (et ce plafond si « Avec plafond » est coché, sinon le plafond est retiré).", MessageType.None);
        }

        private void ApplyFloorBrush(RoomOutline room)
        {
            room.floorVariant = floorBrush;
            room.ceilingVariant = ceilingBrush;
            room.hasFloor = true;
            room.hasCeiling = brushCeiling;
        }

        private void DrawPropToolOptions()
        {
            List<GameObject> props = Palette.props.Where(p => p != null).ToList();
            if (props.Count == 0)
            {
                EditorGUILayout.HelpBox("La palette n'a aucun prop.", MessageType.Info);
                return;
            }
            propIndex = Mathf.Clamp(propIndex, 0, props.Count - 1);
            const int size = 64;
            int columns = Mathf.Max(1, (int)((position.width - 30) / (size + 6)));
            propScroll = EditorGUILayout.BeginScrollView(propScroll, GUILayout.Height(Mathf.Min(3, Mathf.CeilToInt(props.Count / (float)columns)) * (size + 22) + 6));
            GUIContent[] contents = props.Select(p => new GUIContent(AssetPreview.GetAssetPreview(p) ?? AssetPreview.GetMiniThumbnail(p), p.name)).ToArray();
            propIndex = GUILayout.SelectionGrid(propIndex, contents, columns, GUILayout.Width(columns * (size + 6)));
            EditorGUILayout.EndScrollView();
            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
            EditorGUILayout.LabelField(props[propIndex].name, EditorStyles.miniBoldLabel);

            propRandomYaw = EditorGUILayout.Toggle("Rotation aléatoire", propRandomYaw);
            if (!propRandomYaw) propYaw = EditorGUILayout.Slider("Rotation (R : +45°)", propYaw, 0f, 360f);
            EditorGUILayout.MinMaxSlider("Échelle", ref propMinScale, ref propMaxScale, 0.5f, 2f);
            EditorGUILayout.LabelField(" ", $"{propMinScale:0.00} – {propMaxScale:0.00}", EditorStyles.miniLabel);
            propAlignToSurface = EditorGUILayout.Toggle("Suivre la pente / le mur", propAlignToSurface);
            EditorGUILayout.HelpBox("Clic : poser le prop sur la surface visée. Maj + clic : retirer le prop visé.", MessageType.None);
        }

        private void DrawRoomList()
        {
            List<RoomOutline> rooms = LevelBuilder.FindAll<RoomOutline>();
            EditorGUILayout.LabelField($"Salles ({rooms.Count})", EditorStyles.boldLabel);
            foreach (RoomOutline room in rooms)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{room.name} — {(room.level > 0 ? $"étage {room.level}, " : "")}" +
                                                   $"{(!room.hasWalls ? "sol seul" : room.closed ? "fermée" : "ouverte")}, {room.SegmentCount} côté(s)" +
                                                   $"{(room.stairs.Count > 0 ? $", {room.stairs.Count} escalier(s)" : "")}", EditorStyles.boldLabel);
                        if (GUILayout.Button("Voir", GUILayout.Width(50)))
                        {
                            Selection.activeGameObject = room.gameObject;
                            SceneView.lastActiveSceneView?.FrameSelected();
                        }
                        if (GUILayout.Button("Supprimer", GUILayout.Width(75)))
                        {
                            Undo.DestroyObjectImmediate(room.gameObject);
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    var palette = (PrefabPalette)EditorGUILayout.ObjectField("Palette", room.palette, typeof(PrefabPalette), false);
                    PrefabPalette p = palette != null ? palette : room.palette;
                    int wall = room.wallVariant, pillar = room.pillarVariant, floorV = room.floorVariant, ceilingV = room.ceilingVariant;
                    if (p != null)
                    {
                        wall = VariantPopup("Mur", p.walls, wall, false);
                        pillar = VariantPopup("Poteau", p.pillars, pillar, true);
                    }
                    bool floor = room.hasFloor, ceiling = room.hasCeiling;
                    using (new EditorGUI.DisabledScope(!room.closed))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            floor = EditorGUILayout.ToggleLeft("Sol", floor, GUILayout.Width(60));
                            if (p != null) using (new EditorGUI.DisabledScope(!floor)) floorV = VariantPopup(null, p.floorTiles, floorV, false);
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            ceiling = EditorGUILayout.ToggleLeft("Plafond", ceiling, GUILayout.Width(60));
                            if (p != null) using (new EditorGUI.DisabledScope(!ceiling)) ceilingV = VariantPopup(null, p.ceilingTiles, ceilingV, false);
                        }
                    }
                    bool flip = EditorGUILayout.ToggleLeft("Retourner les murs", room.flipWalls);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(room, "Modifier la salle");
                        if (wall != room.wallVariant)
                        {
                            room.openings.RemoveAll(o => o.kind == RoomModuleKind.Wall && o.variant == wall);
                            // Floor and ceiling follow the new wall's width.
                            float width = RoomBuilder.ModuleWidth(p, wall);
                            if (p.floorTiles.Count > 0) floorV = RoomBuilder.BestTileFor(p.floorTiles, width);
                            if (p.ceilingTiles.Count > 0) ceilingV = RoomBuilder.BestTileFor(p.ceilingTiles, width);
                            if (Mathf.Abs(width - RoomBuilder.ModuleWidth(room)) > 0.02f)
                                Debug.LogWarning($"[Level Maker] {room.name} : le nouveau mur fait {width:0.##} m de large au lieu de " +
                                                 $"{RoomBuilder.ModuleWidth(room):0.##} m — le tracé n'en est plus un nombre entier, retrace la salle avec ce mur.", room);
                        }
                        room.palette = palette;
                        room.wallVariant = wall;
                        room.pillarVariant = pillar;
                        room.floorVariant = floorV;
                        room.ceilingVariant = ceilingV;
                        room.hasFloor = floor;
                        room.hasCeiling = ceiling;
                        room.flipWalls = flip;
                        RoomBuilder.Build(room);
                        MarkDirty();
                    }
                    if (GUILayout.Button("Reconstruire (après modification des prefabs du kit)"))
                    {
                        RoomBuilder.Build(room);
                        MarkDirty();
                    }
                }
            }
        }

        // Variant names in a popup (the room list); withNone = "Aucun" at -1.
        private static int VariantPopup(string label, List<GameObject> list, int selected, bool withNone)
        {
            var names = new List<string>();
            if (withNone) names.Add("Aucun");
            for (int i = 0; i < list.Count; i++) names.Add($"{i + 1}. {(list[i] != null ? list[i].name : "(vide)")}");
            if (names.Count == 0) return selected;
            int offset = withNone ? 1 : 0;
            int index = Mathf.Clamp(selected + offset, 0, names.Count - 1);
            index = label == null ? EditorGUILayout.Popup(index, names.ToArray()) : EditorGUILayout.Popup(label, index, names.ToArray());
            return index - offset;
        }

        // A side is built from whole wall modules: one whose length isn't a
        // multiple of the module gets its walls squeezed together or spread
        // apart (overlapping or gaping).
        private bool WholeModules(Vector3 a, Vector3 b)
        {
            float w = TraceStep;
            float length = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            int modules = Mathf.RoundToInt(length / w);
            return modules >= 1 && Mathf.Abs(length - modules * w) < 0.02f;
        }

        // Corners to add before closing back to the first point so every side
        // stays whole: none when the straight way back already is; else one
        // corner making two straight sides along X and Z (as long as it
        // doesn't fold back over the last side). Null when neither works.
        private List<Vector3> ClosingCorners()
        {
            if (trace.Count < 2) return null;
            Vector3 last = trace[trace.Count - 1], first = trace[0];
            if (trace.Count >= 3 && WholeModules(last, first)) return new List<Vector3>();

            Vector3 lastDir = LevelBuilder.Flat(last - trace[trace.Count - 2]);
            foreach (Vector3 corner in new[] { new Vector3(first.x, last.y, last.z), new Vector3(last.x, last.y, first.z) })
            {
                if ((corner - last).sqrMagnitude < 0.0001f || (corner - first).sqrMagnitude < 0.0001f) continue;
                if (Vector3.Dot(LevelBuilder.Flat(corner - last), lastDir) < -0.99f) continue;   // back over the last side
                if (WholeModules(last, corner) && WholeModules(corner, first)) return new List<Vector3> { corner };
            }
            return null;
        }

        private void FinishTrace(bool closed)
        {
            if (Palette == null || trace.Count < 2) return;
            if (closed && trace.Count > 3 && (trace[trace.Count - 1] - trace[0]).sqrMagnitude < 0.0001f)
                trace.RemoveAt(trace.Count - 1);
            if (closed)
            {
                List<Vector3> corners = ClosingCorners();
                if (corners == null)
                {
                    Debug.LogWarning("[Level Maker] Impossible de fermer la salle avec des murs entiers depuis ce point : " +
                                     "ajoute un point aligné (même X ou même Z) sur le premier, puis ferme.");
                    return;
                }
                trace.AddRange(corners);
                if (trace.Count < 3) return;
            }
            if (floorOnlyTrace && !closed) return;   // a floor zone needs an inside
            RoomOutline room = RoomBuilder.CreateRoom(trace, closed, Palette, wallBrush, pillarBrush, floorBrush, ceilingBrush, activeLevel,
                build: false, hasWalls: !floorOnlyTrace);
            // A room inside a room: no second floor, no wall built twice.
            RoomOutline host = floorOnlyTrace ? null : RoomBuilder.FitIntoHost(room);
            if (host != null)
                Debug.Log($"[Level Maker] {room.name} est dans {host.name} : pas de sol propre (celui de {host.name} sert), " +
                          "murs déjà présents non reconstruits.", room);
            RoomBuilder.Build(room);
            Selection.activeGameObject = room.gameObject;
            // Ceilings below / floor holes above depend on this room.
            foreach (RoomOutline other in RoomBuilder.AllRooms())
                if (other != room && Mathf.Abs(other.level - room.level) == 1) RoomBuilder.Build(other);
            trace.Clear();
            MarkDirty();
            Repaint();
        }

        // ---------- Scene view ----------

        private void RoomSceneGUI()
        {
            DrawRoomOutlines();
            if (roomTool == RoomTool.None || Palette == null) return;

            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);   // clicks build instead of selecting

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (roomTool == RoomTool.Walls && trace.Count > 0) trace.Clear();
                else roomTool = RoomTool.None;
                e.Use();
                Repaint();
                return;
            }

            switch (roomTool)
            {
                case RoomTool.Walls: WallToolGUI(e); break;
                case RoomTool.Openings: OpeningToolGUI(e); break;
                case RoomTool.Floors: FloorToolGUI(e); break;
                case RoomTool.Stairs: StairsToolGUI(e); break;
                case RoomTool.Props: PropToolGUI(e); break;
            }
        }

        // Click on a closed room's floor: stairs from there up to the next
        // level, centered on the click (on the grid), climbing toward the
        // arrow. Shift + click removes the nearest flight.
        private void StairsToolGUI(Event e)
        {
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                stairsYaw = Mathf.Repeat(stairsYaw + 90f, 360f);
                e.Use();
                Repaint();
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Physics.SyncTransforms();
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();

            if (e.shift)
            {
                Handles.color = Color.red;
                Handles.DrawWireDisc(hit.point, Vector3.up, 1.5f, 2f);
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    RoomBuilder.RemoveStairsNear(hit.point, 1.5f);
                    e.Use();
                    MarkDirty();
                }
                return;
            }

            RoomOutline room = RoomBuilder.RoomAt(hit.point);
            GameObject prefab = Palette.Stairs(stairsBrush);
            float step = Mathf.Max(0.05f, settings.gridStep);
            Vector3 center = new Vector3(Mathf.Round(hit.point.x / step) * step, room != null ? room.points[0].y : hit.point.y,
                Mathf.Round(hit.point.z / step) * step);
            if (prefab == null || !RoomBuilder.TryStairsShape(Palette, prefab, out Bounds b, out Vector3 uphillLocal))
            {
                Handles.Label(hit.point + Vector3.up * 0.3f, "Pas d'escalier dans la palette", EditorStyles.whiteBoldLabel);
                return;
            }

            var flight = new RoomStairs { position = center, uphillYaw = stairsYaw, variant = stairsBrush };
            RoomBuilder.StairsFootprint f = RoomBuilder.Footprint(flight, b, uphillLocal);
            Vector3 across = Vector3.Cross(Vector3.up, f.uphill);
            Vector3 l = f.uphill * f.halfLength, w = across * f.halfWidth;
            bool hasAbove = room != null && RoomBuilder.AllRooms().Any(r => r.level == room.level + 1 && RoomBuilder.Inside(center, r.points));
            Handles.color = room == null ? Color.red : hasAbove ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.8f, 0.2f);
            Vector3 up = Vector3.up * 0.03f;
            Handles.DrawAAPolyLine(3f, center - l - w + up, center + l - w + up, center + l + w + up, center - l + w + up, center - l - w + up);
            Handles.DrawAAPolyLine(5f, center - l * 0.6f + up, center + l * 0.8f + up);
            if (e.type == EventType.Repaint)
                Handles.ConeHandleCap(0, center + l * 0.8f + up, Quaternion.LookRotation(f.uphill), 0.25f, EventType.Repaint);
            Handles.Label(center + Vector3.up * 0.5f,
                room == null ? "Hors d'une salle fermée" : hasAbove ? $"Monte vers l'étage {room.level + 1}" : "Aucune salle au-dessus (l'escalier mènera dans le vide)",
                EditorStyles.whiteBoldLabel);

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && room != null)
            {
                RoomBuilder.AddStairs(room, flight);
                e.Use();
                MarkDirty();
            }
        }

        private void DrawRoomOutlines()
        {
            Handles.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            foreach (RoomOutline room in LevelBuilder.FindAll<RoomOutline>())
                for (int i = 0; i < room.SegmentCount; i++)
                {
                    RoomBuilder.SegmentEnds(room, i, out Vector3 a, out Vector3 b);
                    Handles.DrawLine(a + Vector3.up * 0.02f, b + Vector3.up * 0.02f, 1.5f);
                }
        }

        private void WallToolGUI(Event e)
        {
            float floorY = TraceY;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 cursor = ray.GetPoint(distance);
            Vector3 snapped = SnapTracePoint(cursor, e.shift);
            bool closing = trace.Count >= 2 && snapped == trace[0];
            // How the room would close from here: extra corner if the
            // straight way back isn't a whole number of modules; null = impossible.
            List<Vector3> closingCorners = closing ? ClosingCorners() : null;

            if (showGrid) DrawGrid(snapped, floorY);

            // Trace so far and the segment being drawn, with module ticks.
            float w = TraceStep;
            Handles.color = new Color(1f, 0.75f, 0.2f);
            for (int i = 0; i + 1 < trace.Count; i++) DrawSegment(trace[i], trace[i + 1], w);
            if (trace.Count > 0 && closing)
            {
                Vector3 last = trace[trace.Count - 1];
                if (closingCorners != null)
                {
                    Handles.color = new Color(0.3f, 1f, 0.4f);
                    Vector3 from = last;
                    foreach (Vector3 corner in closingCorners.Append(trace[0])) { DrawSegment(from, corner, w); from = corner; }
                    Handles.Label(snapped + Vector3.up * 0.3f,
                        closingCorners.Count == 0 ? "Fermer" : "Fermer (coin ajouté pour garder des murs entiers)", EditorStyles.whiteBoldLabel);
                }
                else
                {
                    Handles.color = Color.red;
                    Handles.DrawDottedLine(last + Vector3.up * 0.03f, trace[0] + Vector3.up * 0.03f, 4f);
                    Handles.Label(snapped + Vector3.up * 0.3f,
                        "Fermeture impossible avec des murs entiers : ajoute un point pour t'aligner sur le premier", EditorStyles.whiteBoldLabel);
                }
            }
            else if (trace.Count > 0)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.9f);
                DrawSegment(trace[trace.Count - 1], snapped, w);
                float length = Vector3.Distance(trace[trace.Count - 1], snapped);
                Handles.Label(snapped + Vector3.up * 0.3f,
                    $"{Mathf.Max(1, Mathf.RoundToInt(length / w))} module(s) — {length:0.##} m", EditorStyles.whiteBoldLabel);
            }
            foreach (Vector3 p in trace) Handles.DrawWireDisc(p, Vector3.up, 0.12f, 2f);
            Handles.color = Color.white;
            Handles.DrawWireDisc(snapped, Vector3.up, 0.15f, 2f);
            Handles.DrawLine(snapped, snapped + Vector3.up * RoomBuilder.WallHeight(Palette, wallBrush), 1f);

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) SceneView.RepaintAll();

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                if (closing) { if (closingCorners != null) FinishTrace(true); }
                else if (trace.Count == 0 || snapped != trace[trace.Count - 1]) trace.Add(snapped);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                FinishTrace(false);
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace && trace.Count > 0)
            {
                trace.RemoveAt(trace.Count - 1);
                e.Use();
                Repaint();
            }
        }

        // First point on the grid; then directions by 45° (free with Shift)
        // and lengths in whole wall modules, so every segment is filled
        // exactly. Close to the first point snaps onto it (closes the room).
        private Vector3 SnapTracePoint(Vector3 p, bool freeAngle)
        {
            float floorY = TraceY;
            float step = Mathf.Max(0.05f, settings.gridStep);
            if (trace.Count == 0)
                return new Vector3(Mathf.Round(p.x / step) * step, floorY, Mathf.Round(p.z / step) * step);

            float w = TraceStep;
            Vector3 last = trace[trace.Count - 1];
            Vector3 flat = new Vector3(p.x, floorY, p.z);
            // Two points are enough: closing may add the third corner.
            if (trace.Count >= 2 && (flat - trace[0]).magnitude < w * 0.5f) return trace[0];

            Vector3 delta = flat - last;
            if (delta.magnitude < 0.01f) return last;
            Vector3 dir = delta.normalized;
            if (!freeAngle)
            {
                float angle = Mathf.Round(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg / 45f) * 45f;
                dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            }
            int modules = Mathf.Max(1, Mathf.RoundToInt(Vector3.Dot(delta, dir) / w));
            return last + dir * (modules * w);
        }

        private static void DrawSegment(Vector3 a, Vector3 b, float moduleWidth)
        {
            Handles.DrawLine(a + Vector3.up * 0.03f, b + Vector3.up * 0.03f, 3f);
            float length = Vector3.Distance(a, b);
            int modules = Mathf.Max(1, Mathf.RoundToInt(length / moduleWidth));
            for (int i = 1; i < modules; i++)
            {
                Vector3 tick = Vector3.Lerp(a, b, i / (float)modules);
                Handles.DrawLine(tick, tick + Vector3.up * 0.3f, 2f);
            }
        }

        private void DrawGrid(Vector3 around, float y)
        {
            float step = Mathf.Max(0.05f, settings.gridStep);
            const float half = 10f;
            float cx = Mathf.Round(around.x / step) * step, cz = Mathf.Round(around.z / step) * step;
            int lines = Mathf.Min(200, Mathf.CeilToInt(half / step));
            Handles.color = new Color(1f, 1f, 1f, 0.12f);
            for (int i = -lines; i <= lines; i++)
            {
                Handles.DrawLine(new Vector3(cx + i * step, y, cz - half), new Vector3(cx + i * step, y, cz + half));
                Handles.DrawLine(new Vector3(cx - half, y, cz + i * step), new Vector3(cx + half, y, cz + i * step));
            }
        }

        private void OpeningToolGUI(Event e)
        {
            // Empty wall cells have no module to click: a sphere stands in.
            Handles.color = Color.white;
            foreach (RoomOutline room in RoomBuilder.AllRooms())
                foreach (var (segment, cell, center) in RoomBuilder.EmptyCells(room).ToList())
                    if (Handles.Button(center + Vector3.up * 1f, Quaternion.identity, 0.2f, 0.25f, Handles.SphereHandleCap))
                    {
                        RoomBuilder.SetCell(room, segment, cell, OpeningKinds[openingBrush], OpeningVariant);
                        MarkDirty();
                        return;
                    }

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDown)
            {
                GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
                RoomModule module = picked != null ? picked.GetComponentInParent<RoomModule>() : null;
                if (module != null && (module.segment < 0 || (module.kind != RoomModuleKind.Wall && module.kind != RoomModuleKind.Door
                    && module.kind != RoomModuleKind.Window && module.kind != RoomModuleKind.Railing))) module = null;
                if (module != hoveredModule) { hoveredModule = module; SceneView.RepaintAll(); }
            }

            if (hoveredModule != null && LevelBuilder.TryGetBounds(hoveredModule.gameObject, out Bounds b))
            {
                Handles.color = new Color(0.3f, 1f, 0.6f);
                Handles.DrawWireCube(b.center, b.size);
                RoomOutline hoveredRoom = hoveredModule.GetComponentInParent<RoomOutline>();
                int span = hoveredRoom != null && OpeningKinds[openingBrush] != RoomModuleKind.None
                    ? RoomBuilder.SpanOf(Palette, RoomBuilder.PieceFor(Palette, OpeningKinds[openingBrush], OpeningVariant), RoomBuilder.ModuleWidth(hoveredRoom))
                    : 1;
                string what = insertMode
                    ? (OpeningKinds[openingBrush] == RoomModuleKind.None ? "retirer la pièce imbriquée" : $"{OpeningNames[openingBrush]} imbriquée")
                    : OpeningNames[openingBrush] + (span > 1 ? $" ({span} modules)" : "");
                Handles.Label(b.center + Vector3.up * (b.extents.y + 0.2f), $"→ {what}", EditorStyles.whiteBoldLabel);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hoveredModule != null)
            {
                RoomBuilder.SetOpening(hoveredModule, OpeningKinds[openingBrush], OpeningVariant, insertMode);
                hoveredModule = null;
                e.Use();
                MarkDirty();
            }
        }

        // Click on any piece of a closed room: it gets the selected floor
        // (and ceiling) variants.
        private void FloorToolGUI(Event e)
        {
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDown)
            {
                GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
                RoomModule module = picked != null ? picked.GetComponentInParent<RoomModule>() : null;
                RoomOutline owner = module != null ? module.GetComponentInParent<RoomOutline>() : null;
                if (owner == null || !owner.closed) module = null;
                if (module != hoveredModule) { hoveredModule = module; SceneView.RepaintAll(); }
            }

            RoomOutline room = hoveredModule != null ? hoveredModule.GetComponentInParent<RoomOutline>() : null;
            if (room != null)
            {
                Handles.color = new Color(0.3f, 1f, 0.6f);
                for (int i = 0; i < room.points.Count; i++)
                    Handles.DrawLine(room.points[i] + Vector3.up * 0.05f, room.points[(i + 1) % room.points.Count] + Vector3.up * 0.05f, 3f);
                Handles.Label(room.points[0] + Vector3.up * 0.5f, $"{room.name} → sol {floorBrush + 1}{(brushCeiling ? $", plafond {ceilingBrush + 1}" : ", sans plafond")}",
                    EditorStyles.whiteBoldLabel);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && room != null)
            {
                Undo.RecordObject(room, "Sol et plafond");
                ApplyFloorBrush(room);
                RoomBuilder.Build(room);
                hoveredModule = null;
                e.Use();
                MarkDirty();
            }
        }

        private void PropToolGUI(Event e)
        {
            List<GameObject> props = Palette.props.Where(p => p != null).ToList();
            if (props.Count == 0) return;
            GameObject prefab = props[Mathf.Clamp(propIndex, 0, props.Count - 1)];

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R && !propRandomYaw)
            {
                propYaw = Mathf.Repeat(propYaw + 45f, 360f);
                e.Use();
                Repaint();
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Physics.SyncTransforms();
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            bool erasing = e.shift;
            if (erasing)
            {
                RoomModule target = hit.collider.GetComponentInParent<RoomModule>();
                Handles.color = target != null && target.kind == RoomModuleKind.Prop ? Color.red : new Color(1f, 0.4f, 0.4f, 0.4f);
                if (target != null && target.kind == RoomModuleKind.Prop && LevelBuilder.TryGetBounds(target.gameObject, out Bounds tb))
                    Handles.DrawWireCube(tb.center, tb.size);
                else
                    Handles.DrawWireDisc(hit.point, hit.normal, 0.2f, 2f);
            }
            else
            {
                float radius = RoomBuilder.TryMeasure(prefab, out Bounds pb) ? Mathf.Max(pb.extents.x, pb.extents.z) * propMaxScale : 0.3f;
                Handles.color = new Color(0.3f, 1f, 1f);
                Handles.DrawWireDisc(hit.point, hit.normal, radius, 2f);
                if (!propRandomYaw)
                {
                    Vector3 facing = Quaternion.Euler(0f, propYaw, 0f) * Vector3.forward;
                    Handles.DrawLine(hit.point, hit.point + facing * radius, 2f);
                }
                Handles.Label(hit.point + hit.normal * 0.3f, prefab.name, EditorStyles.whiteBoldLabel);
            }
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();

            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;
            if (erasing)
            {
                RoomModule target = hit.collider.GetComponentInParent<RoomModule>();
                if (target != null && target.kind == RoomModuleKind.Prop) Undo.DestroyObjectImmediate(target.gameObject);
            }
            else
            {
                float yaw = propRandomYaw ? Random.Range(0f, 360f) : propYaw;
                float scale = Random.Range(propMinScale, propMaxScale);
                RoomBuilder.PlaceProp(prefab, hit, yaw, scale, propAlignToSurface, Palette);
            }
            e.Use();
            MarkDirty();
        }
    }
}
#endif
