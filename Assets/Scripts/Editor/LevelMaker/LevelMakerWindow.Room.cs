#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // ② Salle: trace walls on a grid, turn modules into doors and windows,
    // fill floors and ceilings, paint props. Each tool shows its palette
    // variants as thumbnails. Rooms are rebuilt from their RoomOutline, so
    // changing kit keeps the layout.
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
        private RoomOutline hoveredRoom;   // Sol / plafond tool
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
        // Rolled before the click so the ghost shows what will be placed.
        private float nextRandomYaw, nextScale = 1f;
        private bool nextRolled;

        // Ghosts of what each tool is about to build (RoomBuilder.Preview*,
        // drawn by PrefabGhost). Reused lists: rebuilt on every repaint.
        private readonly List<RoomBuilder.GhostPiece> ghostPieces = new List<RoomBuilder.GhostPiece>();
        private readonly List<Vector3> ghostTrace = new List<Vector3>();
        private static readonly Color GhostColor = new Color(0.3f, 1f, 1f, 0.4f);
        private static readonly Color GhostReplaceColor = new Color(0.4f, 1f, 0.5f, 0.45f);
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
            showGrid = EditorGUILayout.Toggle("Afficher la grille", showGrid);

            DrawLevelOptions();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Outil (vue Scène)", EditorStyles.boldLabel);
            var tool = (RoomTool)GUILayout.Toolbar((int)roomTool, RoomToolNames, GUILayout.Height(24));
            if (tool != roomTool)
            {
                trace.Clear();
                hoveredModule = null;
                hoveredRoom = null;
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
            stairsYaw = EditorGUILayout.Slider("Sens de montée (Ctrl + molette, + Alt : 45° ; R : +90°)", stairsYaw, 0f, 360f);
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
                    RoomBuilder.BuildWithNeighbours(room);
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

        private const int MaxLevel = 5;

        private void DrawLevelOptions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Étages", EditorStyles.boldLabel);
            int level = EditorGUILayout.IntSlider(new GUIContent("Étage actif", "Étage sur lequel on trace (0 = rez-de-chaussée). " +
                "Un étage = la hauteur des murs du kit."), activeLevel, 0, MaxLevel);
            if (level != activeLevel) SetActiveLevel(level);
            List<RoomOutline> rooms = RoomBuilder.AllRooms();
            int here = rooms.Count(r => r.level == activeLevel), below = rooms.Count(r => r.level == activeLevel - 1);
            EditorGUILayout.LabelField(" ", $"Tracé à {TraceY:0.##} m (étage de {RoomBuilder.StoreyHeight(Palette, wallBrush):0.##} m) — " +
                $"{here} salle(s) à cet étage{(activeLevel > 0 ? $", {below} en dessous" : "")}", EditorStyles.miniLabel);

            RoomOutline selected = SelectedRoom;
            bool canStack = selected != null && selected.closed && selected.hasWalls && selected.level < MaxLevel;
            using (new EditorGUI.DisabledScope(!canStack))
                if (GUILayout.Button(canStack ? $"Créer l'étage au-dessus de « {selected.name} »"
                        : "Sélectionne une salle fermée pour créer l'étage au-dessus"))
                    CreateRoomAbove(selected);
            EditorGUILayout.HelpBox("Pour faire un étage :\n" +
                                    "1. Sélectionne la salle du bas, puis « Créer l'étage au-dessus » : même contour, mêmes murs, un étage plus haut " +
                                    "(son sol remplace le plafond du dessous). Ou passe l'étage actif à 1 et trace la salle avec l'outil Murs : " +
                                    "le contour de l'étage du dessous est dessiné en pointillé pour servir de repère.\n" +
                                    "2. Outil Escaliers : clic sur le sol de la salle du bas ; l'escalier garde sa taille (choisis-en un à la hauteur des murs) et perce le sol du dessus.",
                MessageType.None);
        }

        private void SetActiveLevel(int level)
        {
            activeLevel = Mathf.Clamp(level, 0, MaxLevel);
            trace.Clear();
            SceneView.RepaintAll();
            Repaint();
        }

        // Same outline, walls and tiles one level up; the room below loses its
        // ceiling under it (BuildWithNeighbours).
        private void CreateRoomAbove(RoomOutline below)
        {
            float storey = RoomBuilder.StoreyHeight(below.palette, below.wallVariant);
            List<Vector3> points = below.points.Select(p => p + Vector3.up * storey).ToList();
            RoomOutline room = RoomBuilder.CreateRoom(points, true, below.palette, below.wallVariant, below.pillarVariant,
                below.floorVariant, below.ceilingVariant, below.level + 1, build: false);
            room.flipWalls = below.flipWalls;
            room.hasCeiling = below.hasCeiling;
            RoomBuilder.FitIntoHost(room);   // a room already there on that level
            RoomBuilder.BuildWithNeighbours(room);
            Selection.activeGameObject = room.gameObject;
            SetActiveLevel(room.level);
            MarkDirty();
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
            if (!propRandomYaw) propYaw = EditorGUILayout.Slider("Rotation", propYaw, 0f, 360f);
            EditorGUILayout.MinMaxSlider("Échelle", ref propMinScale, ref propMaxScale, 0.5f, 2f);
            EditorGUILayout.LabelField(" ", $"{propMinScale:0.00} – {propMaxScale:0.00}", EditorStyles.miniLabel);
            if (nextScale < propMinScale || nextScale > propMaxScale) nextScale = Random.Range(propMinScale, propMaxScale);
            propAlignToSurface = EditorGUILayout.Toggle("Suivre la pente / le mur", propAlignToSurface);
            EditorGUILayout.HelpBox("Le fantôme montre le prop tel qu'il sera posé. Clic : le poser. Ctrl + molette : le faire tourner (Maj = plus fin ; Alt = par 45°), R : +45°. " +
                                    "Tourner coupe la rotation aléatoire. Maj + clic : retirer le prop visé.", MessageType.None);
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
                            // The room around it gets its floor back, the levels
                            // around their ceiling and floor.
                            List<RoomOutline> related = RoomBuilder.Related(room);
                            Undo.DestroyObjectImmediate(room.gameObject);
                            foreach (RoomOutline other in related) RoomBuilder.Build(other);
                            MarkDirty();
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
                        RoomBuilder.BuildWithNeighbours(room);
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
            // Ceilings below, floor holes above and the room around depend on it.
            foreach (RoomOutline other in RoomBuilder.Related(room)) RoomBuilder.Build(other);
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
            if (e.type == EventType.Repaint && room != null)
            {
                ghostPieces.Clear();
                RoomBuilder.PreviewStairs(room, flight, ghostPieces);
                DrawGhost(GhostColor);
            }
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

        // The active level's rooms stand out, the others are faint. While
        // tracing above the ground, the level below is repeated dotted at the
        // trace height, to trace on top of it.
        private void DrawRoomOutlines()
        {
            bool guide = roomTool == RoomTool.Walls && activeLevel > 0 && Palette != null;
            float traceY = guide ? TraceY : 0f;
            foreach (RoomOutline room in LevelBuilder.FindAll<RoomOutline>())
            {
                bool active = room.level == activeLevel;
                Handles.color = active ? new Color(1f, 0.6f, 0.1f, 0.9f) : new Color(1f, 0.6f, 0.1f, 0.25f);
                for (int i = 0; i < room.SegmentCount; i++)
                {
                    RoomBuilder.SegmentEnds(room, i, out Vector3 a, out Vector3 b);
                    Handles.DrawLine(a + Vector3.up * 0.02f, b + Vector3.up * 0.02f, active ? 2.5f : 1f);
                    if (guide && room.level == activeLevel - 1)
                    {
                        Handles.color = new Color(0.4f, 0.8f, 1f, 0.9f);
                        Handles.DrawDottedLine(new Vector3(a.x, traceY + 0.02f, a.z), new Vector3(b.x, traceY + 0.02f, b.z), 4f);
                        Handles.color = active ? new Color(1f, 0.6f, 0.1f, 0.9f) : new Color(1f, 0.6f, 0.1f, 0.25f);
                    }
                }
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

            // Ghost of the walls (and floor, once closing) as they'd be built.
            if (e.type == EventType.Repaint && trace.Count > 0)
            {
                bool closedGhost = closing && closingCorners != null;
                ghostTrace.Clear();
                ghostTrace.AddRange(trace);
                if (closedGhost) ghostTrace.AddRange(closingCorners);
                else if (!closing && snapped != trace[trace.Count - 1]) ghostTrace.Add(snapped);
                ghostPieces.Clear();
                RoomBuilder.PreviewTrace(ghostTrace, closedGhost, true, Palette, wallBrush, pillarBrush, floorBrush, !floorOnlyTrace, ghostPieces);
                DrawGhost(GhostColor);
            }

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
                if (e.type == EventType.Repaint && hoveredRoom != null)
                {
                    ghostPieces.Clear();
                    RoomBuilder.PreviewOpening(hoveredRoom, hoveredModule.segment, hoveredModule.cell, OpeningKinds[openingBrush], OpeningVariant,
                        insertMode, ghostPieces);
                    DrawGhost(GhostReplaceColor);
                }
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
                RoomOutline found = PickRoom(e.mousePosition);
                if (found != hoveredRoom) { hoveredRoom = found; SceneView.RepaintAll(); }
            }

            RoomOutline room = hoveredRoom;
            if (room != null)
            {
                Handles.color = new Color(0.3f, 1f, 0.6f);
                for (int i = 0; i < room.points.Count; i++)
                    Handles.DrawLine(room.points[i] + Vector3.up * 0.05f, room.points[(i + 1) % room.points.Count] + Vector3.up * 0.05f, 3f);
                Handles.Label(room.points[0] + Vector3.up * 0.5f, $"{room.name} → sol {floorBrush + 1}{(brushCeiling ? $", plafond {ceilingBrush + 1}" : ", sans plafond")}",
                    EditorStyles.whiteBoldLabel);
                if (e.type == EventType.Repaint)
                {
                    ghostPieces.Clear();
                    RoomBuilder.PreviewFloors(room, floorBrush, brushCeiling ? ceilingBrush : -1, ghostPieces);
                    DrawGhost(GhostReplaceColor);
                }
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && room != null)
            {
                Undo.RecordObject(room, "Sol et plafond");
                ApplyFloorBrush(room);
                RoomBuilder.BuildWithNeighbours(room);
                hoveredRoom = null;
                e.Use();
                MarkDirty();
            }
        }

        // The closed room under the mouse: the innermost one at the point hit
        // (a room inside another has no floor of its own at first, so the
        // host's floor is what gets hit), else the owner of the clicked piece.
        private static RoomOutline PickRoom(Vector2 mousePosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Physics.SyncTransforms();
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                RoomOutline atPoint = RoomBuilder.RoomAt(hit.point);
                if (atPoint != null) return atPoint;
            }
            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            RoomOutline owner = picked != null ? picked.GetComponentInParent<RoomOutline>() : null;
            return owner != null && owner.closed ? owner : null;
        }

        private void PropToolGUI(Event e)
        {
            List<GameObject> props = Palette.props.Where(p => p != null).ToList();
            if (props.Count == 0) return;
            GameObject prefab = props[Mathf.Clamp(propIndex, 0, props.Count - 1)];

            if (!nextRolled) RollNextProp();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                RotateNextProp(45f);
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
                float radius = RoomBuilder.TryMeasure(prefab, out Bounds pb) ? Mathf.Max(pb.extents.x, pb.extents.z) * nextScale : 0.3f;
                RoomBuilder.PropPose(prefab, hit.point, propAlignToSurface ? hit.normal : Vector3.up, NextYaw, nextScale,
                    out Vector3 ghostPosition, out Quaternion ghostRotation, out Vector3 ghostScale);
                PrefabGhost.Draw(prefab, ghostPosition, ghostRotation, ghostScale, GhostColor);
                Handles.color = new Color(0.3f, 1f, 1f);
                Handles.DrawWireDisc(hit.point, hit.normal, radius, 2f);
                Vector3 facing = Quaternion.Euler(0f, NextYaw, 0f) * Vector3.forward;
                Handles.DrawLine(hit.point, hit.point + facing * radius, 2f);
                Handles.Label(hit.point + hit.normal * 0.3f, $"{prefab.name}  {NextYaw:0}°", EditorStyles.whiteBoldLabel);
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
                RoomBuilder.PlaceProp(prefab, hit, NextYaw, nextScale, propAlignToSurface, Palette);
                RollNextProp();
            }
            e.Use();
            MarkDirty();
        }

        private void DrawGhost(Color color)
        {
            foreach (RoomBuilder.GhostPiece piece in ghostPieces)
                PrefabGhost.Draw(piece.prefab, piece.position, piece.rotation, piece.scale, color);
        }

        private float NextYaw => propRandomYaw ? nextRandomYaw : propYaw;

        private const float NotchScrollUnits = 3f;
        private float notchScroll;

        // The next multiple of 45° after yaw, in direction dir (±1): from 13°,
        // up = 45°, down = 0°; from 45°, up = 90°.
        private static float SnapStep(float yaw, int dir)
        {
            const float notch = 45f;
            float k = yaw / notch;
            return (dir > 0 ? Mathf.Floor(k + 1e-3f) + 1f : Mathf.Ceil(k - 1e-3f) - 1f) * notch;
        }

        private void RollNextProp()
        {
            nextRandomYaw = Random.Range(0f, 360f);
            nextScale = Random.Range(propMinScale, propMaxScale);
            nextRolled = true;
        }

        // Turning by hand takes over from the random rotation, from where
        // the ghost was.
        private void RotateNextProp(float degrees)
        {
            if (propRandomYaw)
            {
                propYaw = nextRandomYaw;
                propRandomYaw = false;
            }
            propYaw = Mathf.Repeat(propYaw + degrees, 360f);
        }

        // Ctrl + wheel turns the Props and Stairs ghost (Shift = finer),
        // Ctrl + Alt + wheel by 45° notches. Caught before the Scene view
        // zooms. Shift + wheel arrives as a horizontal scroll on Windows.
        private void OnBeforeSceneGUI(SceneView view)
        {
            Event e = Event.current;
            if (e.type != EventType.ScrollWheel || !(e.control || e.command) || EditorApplication.isPlaying) return;
            if (step != Step.Room || (roomTool != RoomTool.Props && roomTool != RoomTool.Stairs) || Palette == null) return;
            float delta = Mathf.Abs(e.delta.y) >= Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
            float current = roomTool == RoomTool.Stairs ? stairsYaw : NextYaw;
            float degrees;
            if (e.alt)
            {
                // One notch per wheel click: a touchpad's many small scrolls
                // add up to one first (≈ 3 units = one click on Windows).
                notchScroll += delta;
                if (Mathf.Abs(notchScroll) < NotchScrollUnits) { e.Use(); return; }
                int dir = notchScroll > 0f ? 1 : -1;
                notchScroll = 0f;
                degrees = SnapStep(current, dir) - current;
            }
            else degrees = delta * settings.propWheelDegrees * (e.shift ? 0.25f : 1f);
            // Stairs: any angle works, the floor hole above follows the
            // flight's own direction (StairsFootprint).
            if (roomTool == RoomTool.Stairs) stairsYaw = Mathf.Repeat(stairsYaw + degrees, 360f);
            else RotateNextProp(degrees);
            e.Use();
            view.Repaint();
            Repaint();
        }
    }
}
#endif
