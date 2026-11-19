#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // ② Salle, "Remplacer la sélection": the wall modules, pillars, tiles,
    // railings or stairs selected in the scene take another variant of their
    // room's palette — the selected pieces only, or all those of the room.
    // Only what is replaced is rebuilt: changes made by hand to the rest of
    // the room stay. The choice is also written in the room's data (wall
    // cells, pieceVariants, room or flight variants) so a later rebuild
    // gives the same result.
    public partial class LevelMakerWindow
    {
        private static readonly RoomModuleKind[] CellKinds =
            { RoomModuleKind.Wall, RoomModuleKind.Door, RoomModuleKind.Window, RoomModuleKind.Railing };
        private static readonly string[] CellKindNames = { "Mur", "Porte", "Fenêtre", "Garde-corps" };

        private RoomModule replaceSource;   // module the panel was last set up from
        private RoomModuleKind replaceKind;
        private int replaceVariant;
        private Vector2 replaceScroll;
        private PrefabPalette replacePalette;   // kit the replacement is picked from

        private bool selectByType;
        private readonly List<RoomModule> hoveredPieces = new List<RoomModule>();

        // During a preview, Unity's selection stays empty: the preview's
        // pieces are kept in previewSelection (outlined in green) and only
        // selected on Valider. Selected, they'd get Inspectors (LODGroup)
        // that throw when the revert destroys them, and Unity's own
        // selection changes around undo made the selection drift. Anything
        // the user selects meanwhile (Hierarchy click) drops the preview.
        private void OnSelectionChange()
        {
            if (previewing && !selectingForPreview && Selection.objects.Any(o => o != null))
                RevertPreview(restoreSelection: false);
            Repaint();
        }

        private void SetSelection(Object[] objects)
        {
            selectingForPreview = true;
            try { Selection.objects = objects; }
            finally { selectingForPreview = false; }
        }

        private IEnumerable<GameObject> PreviewPieces() =>
            previewSelection == null ? Enumerable.Empty<GameObject>() : previewSelection.OfType<GameObject>().Where(go => go != null);

        [System.NonSerialized] private Object[] previewSelection;

        // "Sélection" tool: click a piece of a room to select it — or, by
        // type, every piece of that type in the room — then pick what
        // replaces it below.
        private void DrawSelectToolOptions()
        {
            selectByType = GUILayout.Toolbar(selectByType ? 1 : 0, new[] { "Un élément", "Tout le type dans la salle" }) == 1;
            EditorGUILayout.HelpBox("Clic sur un mur, une dalle de sol ou de plafond, un poteau, un escalier : " +
                                    (selectByType ? "sélectionne tous ceux du même type dans sa salle (ex. tous les murs). "
                                                  : "le sélectionne. ") +
                                    "Maj ou Ctrl + clic : ajouter à la sélection. Clic dans le vide : tout désélectionner. " +
                                    "Choisis ci-dessous par quoi la remplacer : la sélection prend la pièce choisie tout de suite, Valider la garde, Annuler la retire.", MessageType.None);
            DrawReplaceSelection();
            RunPendingReplace();
        }

        // The Replace button runs after the panel is laid out: it rebuilds
        // the modules the panel was drawn from.
        private System.Action pendingReplace;

        private void RunPendingReplace()
        {
            if (pendingReplace == null) return;
            System.Action run = pendingReplace;
            pendingReplace = null;
            run();
            GUIUtility.ExitGUI();
        }

        private List<RoomModule> PiecesUnder(Vector2 mousePosition)
        {
            var result = new List<RoomModule>();
            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            RoomModule m = picked != null ? picked.GetComponentInParent<RoomModule>() : null;
            if (m == null || !Replaceable(m) || RoomOf(m) == null) return result;
            if (selectByType) result.AddRange(SameType(m));
            else result.Add(m);
            return result;
        }

        private void SelectToolGUI(Event e)
        {
            if (e.type == EventType.MouseMove)
            {
                hoveredPieces.Clear();
                if (!previewing) hoveredPieces.AddRange(PiecesUnder(e.mousePosition));
                SceneView.RepaintAll();
            }
            if (e.type == EventType.Repaint)
            {
                Handles.color = new Color(1f, 0.85f, 0.2f);
                foreach (RoomModule m in hoveredPieces)
                    if (m != null && LevelBuilder.TryGetBounds(m.gameObject, out Bounds b)) Handles.DrawWireCube(b.center, b.size);
                Handles.color = new Color(0.3f, 1f, 0.6f);
                foreach (GameObject go in previewing ? PreviewPieces() : Selection.gameObjects)
                    if (go != null && go.GetComponent<RoomModule>() != null && LevelBuilder.TryGetBounds(go, out Bounds b)) Handles.DrawWireCube(b.center, b.size);

            }
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;

            GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
            RoomModule hit = picked != null ? picked.GetComponentInParent<RoomModule>() : null;
            bool add = e.shift || e.control || e.command;
            if (previewing)
            {
                // A click on the preview itself keeps it.
                if (hit != null && PreviewPieces().Contains(hit.gameObject)) { e.Use(); return; }
                // Anything else: the preview, not validated, is removed. The
                // revert rebuilds pieces (a whole side for a wide one): the
                // clicked one is found again by where it stands.
                var key = hit != null ? (room: RoomOf(hit), hit.kind, hit.segment, hit.cell, center: CenterOf(hit)) : default;
                RevertPreview(restoreSelection: add);
                SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("Aperçu non validé : retiré"), 1f);
                hit = hit != null ? FindPieces(new[] { key }).Select(go => go.GetComponent<RoomModule>()).FirstOrDefault() : null;
            }

            var pieces = new List<RoomModule>();
            if (hit != null && Replaceable(hit) && RoomOf(hit) != null)
            {
                if (selectByType) pieces.AddRange(SameType(hit));
                else pieces.Add(hit);
            }
            var selection = add ? Selection.objects.ToList() : new List<Object>();
            foreach (RoomModule m in pieces)
                if (!selection.Contains(m.gameObject)) selection.Add(m.gameObject);
            Selection.objects = selection.ToArray();
            e.Use();
            Repaint();
        }

        // What a selected module is replaced as: a wall cell (wall, door,
        // window, railing on a side: one family, it can become any of them),
        // or the tile, pillar, void railing or flight it is.
        private static RoomModuleKind Family(RoomModule m) =>
            m.segment >= 0 && CellKinds.Contains(m.kind) ? RoomModuleKind.Wall : m.kind;

        private static bool Replaceable(RoomModule m) => m.segment >= 0 && CellKinds.Contains(m.kind)
            || m.kind is RoomModuleKind.Pillar or RoomModuleKind.Floor or RoomModuleKind.Ceiling
                or RoomModuleKind.Stairs or RoomModuleKind.Railing;

        private static RoomOutline RoomOf(RoomModule m) => m.GetComponentInParent<RoomOutline>();

        // Level Maker pieces in the selection (a child mesh counts as its
        // piece), of the same family as the first one.
        private static List<RoomModule> SelectedModules()
        {
            var result = new List<RoomModule>();
            foreach (GameObject go in Selection.gameObjects)
            {
                RoomModule m = go.GetComponentInParent<RoomModule>();
                if (m == null || result.Contains(m) || !Replaceable(m)) continue;
                RoomOutline room = RoomOf(m);
                if (room == null || room.palette == null) continue;
                result.Add(m);
            }
            if (result.Count > 0)
            {
                RoomModuleKind family = Family(result[0]);
                result.RemoveAll(m => Family(m) != family);
            }
            return result;
        }

        private static List<GameObject> VariantsOf(PrefabPalette p, RoomModuleKind kind) => p == null ? new List<GameObject>() : kind switch
        {
            RoomModuleKind.Wall => p.walls,
            RoomModuleKind.Door => p.doors,
            RoomModuleKind.Window => p.windows,
            RoomModuleKind.Railing => p.railings,
            RoomModuleKind.Pillar => p.pillars,
            RoomModuleKind.Floor => p.floorTiles,
            RoomModuleKind.Ceiling => p.ceilingTiles,
            RoomModuleKind.Stairs => p.stairs,
            _ => new List<GameObject>(),
        };

        private static string FamilyName(RoomModuleKind family) => family switch
        {
            RoomModuleKind.Wall => "module(s) de mur",
            RoomModuleKind.Pillar => "poteau(x)",
            RoomModuleKind.Floor => "dalle(s) de sol",
            RoomModuleKind.Ceiling => "dalle(s) de plafond",
            RoomModuleKind.Railing => "garde-corps du vide",
            RoomModuleKind.Stairs => "escalier(s)",
            _ => "pièce(s)",
        };

        private static Vector3 CenterOf(RoomModule m) =>
            LevelBuilder.TryGetBounds(m.gameObject, out Bounds b) ? b.center : m.transform.position;

        // The flight a stairs piece (steps or their invisible ramp) belongs
        // to: the closest one, on the horizontal.
        private static int FlightIndex(RoomOutline room, RoomModule m) =>
            Closest(room.stairs.Select(f => f.position).ToList(), CenterOf(m));

        // The outline point a pillar stands on.
        private static int PillarIndex(RoomOutline room, RoomModule m) => Closest(room.points, CenterOf(m));

        private static int Closest(List<Vector3> points, Vector3 at)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 d = points[i] - at;
                float distance = d.x * d.x + d.z * d.z;
                if (distance < bestDistance) { best = i; bestDistance = distance; }
            }
            return best;
        }

        private static int RoomVariant(RoomOutline room, RoomModuleKind kind) => kind switch
        {
            RoomModuleKind.Floor => room.floorVariant,
            RoomModuleKind.Ceiling => room.ceilingVariant,
            RoomModuleKind.Pillar => room.pillarVariant,
            RoomModuleKind.Railing => room.railingVariant,
            _ => room.wallVariant,
        };

        // Where a tile or pillar's own variant is stored (RoomOutline.pieceVariants).
        private static Vector3 PieceKey(RoomOutline room, RoomModule m)
        {
            if (m.kind == RoomModuleKind.Pillar)
            {
                int i = PillarIndex(room, m);
                return i >= 0 ? room.points[i] : CenterOf(m);
            }
            return CenterOf(m);
        }

        // The variant, the palette it comes from and (for a wall cell) the
        // kind a module stands for.
        private static int CurrentVariant(RoomModule m, RoomOutline room, out RoomModuleKind kind, out PrefabPalette from)
        {
            kind = m.kind;
            from = room.palette;
            switch (Family(m))
            {
                case RoomModuleKind.Wall:
                    RoomOpening o = room.CellAt(m.segment, room.OwnerCell(m.segment, m.cell));
                    if (!CellKinds.Contains(o.kind)) { kind = RoomModuleKind.Wall; return room.wallVariant; }
                    kind = o.kind;
                    if (o.palette != null) from = o.palette;
                    return o.variant;
                case RoomModuleKind.Floor:
                case RoomModuleKind.Ceiling:
                case RoomModuleKind.Pillar:
                    return room.PieceVariant(m.kind, PieceKey(room, m), RoomVariant(room, m.kind), out from);
                case RoomModuleKind.Railing: return room.railingVariant;
                case RoomModuleKind.Stairs:
                    int i = FlightIndex(room, m);
                    if (i < 0) return 0;
                    if (room.stairs[i].palette != null) from = room.stairs[i].palette;
                    return room.stairs[i].variant;
                default: return 0;
            }
        }

        private static string VariantName(PrefabPalette p, RoomModuleKind kind, int variant)
        {
            if (variant < 0) return "aucun";
            if (p == null) return "?";
            List<GameObject> list = VariantsOf(p, kind);
            return variant < list.Count && list[variant] != null ? list[variant].name : "(vide)";
        }

        private static string KindName(RoomModuleKind kind) =>
            System.Array.IndexOf(CellKinds, kind) is int i && i >= 0 ? CellKindNames[i].ToLowerInvariant() : "";

        // Why nothing can be replaced: said instead of an empty panel.
        private static string NothingToReplace()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0) return "Rien n'est sélectionné : clique sur une pièce de la salle dans la vue Scène.";
            GameObject go = selected[0];
            RoomModule m = go.GetComponentInParent<RoomModule>();
            if (m == null) return $"« {go.name} » n'est pas une pièce posée par le Level Maker.";
            if (!Replaceable(m)) return $"« {go.name} » ({m.kind}) ne se remplace pas ici (props et lumières : outils Props et Lumières).";
            RoomOutline room = RoomOf(m);
            if (room == null) return $"« {go.name} » n'est plus dans une salle.";
            return $"La salle « {room.name} » n'a pas de palette : choisis-la dans la liste des salles.";
        }

        // Live preview: picking a thumbnail replaces the selection for real
        // at once, inside one Undo group; Valider keeps it, Annuler (or
        // leaving: another selection, another tool) reverts to that group.
        [System.NonSerialized] private bool previewing;
        [System.NonSerialized] private int previewGroup;
        // The pieces the preview replaced, by where they stand: reverting
        // brings them back as new objects, the old references stay null.
        [System.NonSerialized] private List<(RoomOutline room, RoomModuleKind kind, int segment, int cell, Vector3 center)> previewOriginals;
        [System.NonSerialized] private bool selectingForPreview;
        // What the pieces were before the preview, shown as "Actuel".
        [System.NonSerialized] private RoomModuleKind previewFamily, previewFromKind;
        [System.NonSerialized] private int previewFromVariant;
        [System.NonSerialized] private PrefabPalette previewFromPalette;

        private void StartPreview(List<RoomModule> modules, RoomModuleKind family, RoomModuleKind fromKind, int fromVariant, PrefabPalette fromPalette)
        {
            previewOriginals = modules.Select(m => (RoomOf(m), m.kind, m.segment, m.cell, CenterOf(m))).ToList();
            previewFamily = family;
            previewFromKind = fromKind;
            previewFromVariant = fromVariant;
            previewFromPalette = fromPalette;
            Undo.IncrementCurrentGroup();
            previewGroup = Undo.GetCurrentGroup();
            selectingForPreview = true;
            try { ApplyReplace(modules, family, fromKind, fromVariant, fromPalette); }
            finally { selectingForPreview = false; }
            previewSelection = Selection.objects;
            SetSelection(new Object[0]);
            previewing = true;
        }

        // restoreSelection: back to the pieces that were selected (Annuler,
        // another thumbnail); not when the user selected something else.
        private void RevertPreview(bool restoreSelection)
        {
            if (!previewing) return;
            previewing = false;
            selectingForPreview = true;
            try
            {
                // An Inspector still showing the preview's pieces (destroyed
                // by the revert) throws from its undo callback: empty it first.
                Selection.objects = new Object[0];
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                DestroyStaleLodEditors();
                // Unity's own undo callbacks may throw (an Inspector on a
                // destroyed piece): the revert is done, carry on.
                try { Undo.RevertAllDownToGroup(previewGroup); }
                catch (System.Exception ex) { Debug.LogWarning("[Level Maker] " + ex.Message); }
                if (restoreSelection && previewOriginals != null) Selection.objects = FindOriginals().ToArray();
            }
            finally { selectingForPreview = false; }
            replaceSource = null;
            MarkDirty();
            SceneView.RepaintAll();
        }

        // Unity's LODGroupEditor resets every LODGroup Inspector still alive
        // on undo, and throws on one whose piece is gone (the Forest Bar
        // pieces have LOD groups): the leftovers are removed first.
        private static void DestroyStaleLodEditors()
        {
            foreach (Editor editor in Resources.FindObjectsOfTypeAll<Editor>())
                if (editor != null && editor.GetType().Name == "LODGroupEditor" && editor.targets.All(t => t == null))
                    DestroyImmediate(editor);
        }

        // The restored pieces: same room, kind, wall cell and place (5 cm).
        private IEnumerable<Object> FindOriginals() => FindPieces(previewOriginals).Cast<Object>();

        private static IEnumerable<GameObject> FindPieces(IList<(RoomOutline room, RoomModuleKind kind, int segment, int cell, Vector3 center)> keys)
        {
            foreach (RoomOutline room in keys.Select(o => o.room).Where(r => r != null).Distinct())
                foreach (RoomModule m in room.GetComponentsInChildren<RoomModule>(true))
                    if (keys.Any(o => o.room == room && o.kind == m.kind && o.segment == m.segment && o.cell == m.cell
                                      && (o.center - CenterOf(m)).sqrMagnitude < 0.0025f))
                        yield return m.gameObject;
        }

        private void ValidatePreview()
        {
            previewing = false;
            Undo.SetCurrentGroupName("Remplacer la sélection");
            Undo.CollapseUndoOperations(previewGroup);
            SetSelection(PreviewPieces().ToArray<Object>());
            replaceSource = null;
        }

        private void DrawReplaceSelection()
        {
            // During a preview, its pieces — whatever Unity did to the selection meanwhile.
            List<RoomModule> modules = previewing
                ? PreviewPieces().Select(go => go.GetComponent<RoomModule>()).Where(m => m != null).ToList()
                : SelectedModules();
            if (modules.Count == 0)
            {
                EditorGUILayout.HelpBox(NothingToReplace(), MessageType.Info);
                return;
            }
            RoomModule first = modules[0];
            RoomOutline firstRoom = RoomOf(first);
            RoomModuleKind family = previewing ? previewFamily : Family(first);
            RoomModuleKind currentKind;
            PrefabPalette currentPalette;
            int current;
            if (previewing)
            {
                currentKind = previewFromKind;
                current = previewFromVariant;
                currentPalette = previewFromPalette;
            }
            else current = CurrentVariant(first, firstRoom, out currentKind, out currentPalette);
            // replaceSource survives a script reload (the window is
            // serialized) while a field added since may still be empty.
            if (!previewing && (first != replaceSource || replacePalette == null))
            {
                replaceSource = first;
                replaceKind = currentKind;
                // The kit chosen at the top of the step: that's where the
                // replacement is usually picked from.
                replacePalette = Palette != null ? Palette : currentPalette;
                replaceVariant = replacePalette == currentPalette ? current : -2;   // -2: nothing picked yet
            }
            // Void railings have one variant per room, from the room's palette.
            if (family == RoomModuleKind.Railing) replacePalette = firstRoom.palette;
            bool cell = family == RoomModuleKind.Wall;
            var before = (replaceKind, replaceVariant, replacePalette);

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Remplacer la sélection", EditorStyles.boldLabel);
                int roomCount = modules.Select(RoomOf).Distinct().Count();
                EditorGUILayout.LabelField($"{modules.Count} {FamilyName(family)} — " + (roomCount > 1 ? $"{roomCount} salles" : firstRoom.name),
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Avant : {(cell ? KindName(currentKind) + " " : "")}{VariantName(currentPalette, currentKind, current)}" +
                                           $"  ({(currentPalette != null ? currentPalette.name : "?")})", EditorStyles.miniBoldLabel);

                using (new EditorGUI.DisabledScope(family == RoomModuleKind.Railing))
                {
                    var picked = (PrefabPalette)EditorGUILayout.ObjectField("Prendre dans la palette", replacePalette, typeof(PrefabPalette), false);
                    if (picked != null && picked != replacePalette) { replacePalette = picked; replaceVariant = -2; }
                }
                if (cell)
                {
                    int k = Mathf.Max(0, System.Array.IndexOf(CellKinds, replaceKind));
                    int pickedKind = GUILayout.Toolbar(k, CellKindNames);
                    if (pickedKind != k)
                    {
                        replaceKind = CellKinds[pickedKind];
                        replaceVariant = -2;
                    }
                }
                List<GameObject> variants = VariantsOf(replacePalette, replaceKind);
                replaceVariant = VariantGrid("Clic sur une vignette : la sélection prend cette pièce (aperçu)", variants,
                    replaceVariant, ref replaceScroll, withNone: replaceKind == RoomModuleKind.Pillar);

                var after = (replaceKind, replaceVariant, replacePalette);
                bool backToStart = replaceKind == currentKind && replaceVariant == current && replacePalette == currentPalette;
                if (after != before && replaceVariant != -2 && variants.Count > 0)
                {
                    // Applied after the panel is laid out: it rebuilds the pieces drawn above.
                    RoomModuleKind fromKind = currentKind;
                    int fromVariant = current;
                    PrefabPalette fromPalette = currentPalette;
                    var choice = after;
                    pendingReplace = () =>
                    {
                        RevertPreview(restoreSelection: true);
                        // The revert can redraw this panel before it returns
                        // (no preview then: the choice is reset to "nothing
                        // picked", -2): the thumbnail clicked is put back.
                        (replaceKind, replaceVariant, replacePalette) = choice;
                        if (backToStart) return;
                        List<RoomModule> originals = SelectedModules();
                        if (originals.Count > 0) StartPreview(originals, family, fromKind, fromVariant, fromPalette);
                    };
                }

                if (cell && GUILayout.Button(new GUIContent("Retourner (180°)",
                        "Pour un modèle dont la face visible est de l'autre côté. Pendant un aperçu, fait partie de l'aperçu.")))
                {
                    List<RoomModule> toFlip = modules;
                    pendingReplace = () => FlipCells(toFlip);
                }

                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(!previewing))
                {
                    if (GUILayout.Button("Valider", GUILayout.Height(24))) ValidatePreview();
                    if (GUILayout.Button("Annuler", GUILayout.Height(24))) pendingReplace = () => RevertPreview(restoreSelection: true);
                }
                EditorGUILayout.LabelField(previewing
                        ? "  Aperçu en place : Valider le garde, Annuler (ou changer de sélection, d'outil) le retire."
                        : "  Rien n'est changé tant qu'aucune vignette n'est choisie.", EditorStyles.wordWrappedMiniLabel);
                if (family == RoomModuleKind.Railing)
                    EditorGUILayout.LabelField("  Les garde-corps du vide changent tous ensemble, dans la palette de la salle.", EditorStyles.miniLabel);
            }
        }

        private void ApplyReplace(List<RoomModule> modules, RoomModuleKind family, RoomModuleKind fromKind, int fromVariant, PrefabPalette fromPalette)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remplacer la sélection");
            List<RoomOutline> rooms = modules.Select(RoomOf).Distinct().ToList();
            foreach (RoomOutline room in rooms) Undo.RecordObject(room, "Remplacer la sélection");
            var created = new List<Object>();
            PrefabPalette newPalette = replacePalette;

            switch (family)
            {
                case RoomModuleKind.Wall:
                {
                    // Read the cells first: rebuilding destroys their modules.
                    var cells = modules.Select(m => (room: RoomOf(m), m.segment, cell: RoomOf(m).OwnerCell(m.segment, m.cell))).Distinct().ToList();

                    foreach (RoomOutline room in rooms)
                    {
                        // Every plain wall of the room selected (selection by type),
                        // new wall from the room's own kit: the room's wall changes.
                        bool roomWall = fromKind == RoomModuleKind.Wall && fromVariant == room.wallVariant && fromPalette == room.palette
                                        && replaceKind == RoomModuleKind.Wall && newPalette == room.palette
                                        && PlainWallCells(room).All(c => cells.Contains((room, c.segment, c.cell)));
                        if (roomWall && !SameWidth(room, replaceVariant))
                        {
                            room.openings.RemoveAll(o => o.kind == RoomModuleKind.Wall && o.variant == replaceVariant && o.palette == null);
                            room.wallVariant = replaceVariant;
                            RoomBuilder.Build(room);   // other module count: the whole room
                            created.AddRange(room.GetComponentsInChildren<RoomModule>().Where(m => Family(m) == RoomModuleKind.Wall).Select(m => m.gameObject));
                            continue;
                        }
                        if (roomWall)
                        {
                            room.openings.RemoveAll(o => o.kind == RoomModuleKind.Wall && o.variant == replaceVariant && o.palette == null);
                            room.wallVariant = replaceVariant;
                        }
                        foreach ((RoomOutline r, int segment, int c) in cells)
                            if (r == room) ReplaceCell(room, segment, c, roomWall, newPalette);
                    }
                    // Gathered once all is done: a piece wider than one cell
                    // rebuilds its whole side, which destroys the pieces
                    // placed there by the cells before.
                    foreach (RoomOutline room in rooms)
                        created.AddRange(room.GetComponentsInChildren<RoomModule>(true)
                            .Where(m => Family(m) == RoomModuleKind.Wall
                                        && (cells.Contains((room, m.segment, m.cell)) || cells.Contains((room, m.segment, room.OwnerCell(m.segment, m.cell)))))
                            .Select(m => m.gameObject));
                    break;
                }

                case RoomModuleKind.Floor:
                case RoomModuleKind.Ceiling:
                case RoomModuleKind.Pillar:
                    foreach (RoomOutline room in rooms)
                    {
                        GameObject prefab = family switch
                        {
                            RoomModuleKind.Floor => newPalette.FloorTile(replaceVariant),
                            RoomModuleKind.Ceiling => newPalette.CeilingTile(replaceVariant),
                            _ => replaceVariant >= 0 ? newPalette.Pillar(replaceVariant) : null,
                        };
                        List<RoomModule> targets = modules.Where(m => RoomOf(m) == room).ToList();
                        // All of them selected (selection by type): the room's own choice changes.
                        bool wholeRoom = targets.Count >= room.GetComponentsInChildren<RoomModule>(true).Count(m => m.kind == family);
                        if (wholeRoom)
                        {
                            bool sameGrid = family == RoomModuleKind.Pillar || SameTileGrid(room, family, prefab, newPalette);
                            int previous = RoomVariant(room, family);
                            PrefabPalette own = newPalette == room.palette ? null : newPalette;
                            if (family == RoomModuleKind.Floor) { room.floorVariant = replaceVariant; room.floorPalette = own; }
                            else if (family == RoomModuleKind.Ceiling) { room.ceilingVariant = replaceVariant; room.ceilingPalette = own; }
                            else { room.pillarVariant = replaceVariant; room.pillarPalette = own; }
                            room.pieceVariants?.RemoveAll(p => p.kind == family);
                            if (!sameGrid || prefab == null || previous < 0)
                            {
                                // Another tile size lays another grid (or pillars
                                // come back from none): that part is laid again.
                                RoomBuilder.RebuildPart(room, family);
                                created.AddRange(room.GetComponentsInChildren<RoomModule>().Where(m => m.kind == family).Select(m => m.gameObject));
                                continue;
                            }
                            // Same grid: each piece standing is swapped where it is,
                            // so tiles moved or removed by hand stay so.
                            targets = room.GetComponentsInChildren<RoomModule>(true).Where(m => m.kind == family).ToList();
                        }
                        foreach (RoomModule m in targets)
                        {
                            if (!wholeRoom) room.SetPieceVariant(family, PieceKey(room, m), replaceVariant, newPalette, RoomVariant(room, family));
                            if (prefab == null) { Undo.DestroyObjectImmediate(m.gameObject); continue; }   // pillar: none
                            GameObject placed = RoomBuilder.SwapPiece(m.gameObject, prefab, family, newPalette);
                            if (placed != null) created.Add(placed);
                        }
                    }
                    break;

                case RoomModuleKind.Railing:
                    foreach (RoomOutline room in rooms)
                    {
                        room.railingVariant = replaceVariant;
                        RoomBuilder.RebuildPart(room, RoomModuleKind.Railing);
                        created.AddRange(room.GetComponentsInChildren<RoomModule>().Where(m => m.kind == RoomModuleKind.Railing && m.segment < 0).Select(m => m.gameObject));
                    }
                    break;

                case RoomModuleKind.Stairs:
                    foreach (RoomOutline room in rooms)
                    {
                        var flights = modules.Where(m => RoomOf(m) == room).Select(m => FlightIndex(room, m)).Where(i => i >= 0).Distinct().ToList();
                        foreach (int i in flights)
                        {
                            RoomStairs flight = room.stairs[i];
                            flight.variant = replaceVariant;
                            flight.palette = newPalette == room.palette ? null : newPalette;
                            room.stairs[i] = flight;
                        }
                        // The stairs only (their ramps with them); the hole in the
                        // floor above follows at that room's next rebuild.
                        RoomBuilder.RebuildPart(room, RoomModuleKind.Stairs);
                        created.AddRange(room.GetComponentsInChildren<RoomModule>()
                            .Where(m => m.kind == RoomModuleKind.Stairs && flights.Contains(FlightIndex(room, m))).Select(m => m.gameObject));
                    }
                    break;
            }
            Undo.CollapseUndoOperations(group);

            Selection.objects = created.Where(o => o != null).Distinct().ToArray();
            replaceSource = null;
            MarkDirty();
        }

        // One wall cell takes the new piece. Same width: that cell only is
        // rebuilt; a wider or narrower piece takes or frees cells, so the
        // side is rebuilt (RoomBuilder.SetCell). roomWall: the cell follows
        // the room's (new) wall, nothing of its own is stored.
        private void ReplaceCell(RoomOutline room, int segment, int cell, bool roomWall, PrefabPalette from)
        {
            RoomOpening old = room.CellAt(segment, cell);
            int span = RoomBuilder.SpanOf(from, RoomBuilder.PieceFor(from, replaceKind, replaceVariant), RoomBuilder.ModuleWidth(room));
            PrefabPalette own = from == room.palette ? null : from;
            if (span == 1 && old.Span == 1)
            {
                if (roomWall) room.openings.RemoveAll(o => o.segment == segment && o.cell == cell);
                else room.SetCell(new RoomOpening { segment = segment, cell = cell, kind = replaceKind, variant = replaceVariant, span = 1, palette = own });
                RoomBuilder.RebuildWallCell(room, segment, cell);
                return;
            }
            RoomBuilder.SetCell(room, segment, cell, replaceKind, replaceVariant, from: own);
        }

        // Turns the selected wall cells' pieces round (RoomOpening.flipped),
        // each cell rebuilt alone.
        private void FlipCells(List<RoomModule> modules)
        {
            var cells = modules.Where(m => m != null && Family(m) == RoomModuleKind.Wall)
                .Select(m => (room: RoomOf(m), m.segment, cell: RoomOf(m).OwnerCell(m.segment, m.cell))).Distinct().ToList();
            if (cells.Count == 0) return;
            if (!previewing) Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Retourner la pièce");
            var created = new List<Object>();
            foreach ((RoomOutline room, int segment, int c) in cells)
            {
                Undo.RecordObject(room, "Retourner la pièce");
                RoomOpening entry = room.CellAt(segment, c);
                entry.flipped = !entry.flipped;
                room.SetCell(entry);
                GameObject placed = RoomBuilder.RebuildWallCell(room, segment, c);
                if (placed != null) created.Add(placed);
            }
            if (previewing) previewSelection = created.ToArray();
            else SetSelection(created.ToArray());
            MarkDirty();
        }

        // Wall cells of a room that have nothing of their own (the room's wall).
        private static IEnumerable<(int segment, int cell)> PlainWallCells(RoomOutline room)
        {
            for (int s = 0; s < room.SegmentCount; s++)
                for (int c = 0, n = RoomBuilder.CellCount(room, s, room.palette); c < n; c++)
                {
                    RoomOpening o = room.CellAt(s, c);
                    if (o.kind == RoomModuleKind.Wall && o.variant == room.wallVariant && o.palette == null) yield return (s, c);
                }
        }

        // Every piece of the room of the same type as m: all its walls (or
        // doors, windows…, whatever their variant), its whole floor, ceiling,
        // pillars, void railings or stairs.
        private static IEnumerable<RoomModule> SameType(RoomModule m)
        {
            RoomOutline room = RoomOf(m);
            if (room == null) return new[] { m };
            RoomModuleKind family = Family(m);
            return room.GetComponentsInChildren<RoomModule>(true)
                .Where(o => o.kind == m.kind && Family(o) == family && Replaceable(o));
        }

        // The new tile lays the same grid as the room's: both stretched to
        // the wall module, or the same size (within 1 cm).
        private static bool SameTileGrid(RoomOutline room, RoomModuleKind kind, GameObject tile, PrefabPalette tilePalette)
        {
            PrefabPalette oldPalette = room.PartPalette(kind);
            GameObject old = kind == RoomModuleKind.Ceiling ? oldPalette.CeilingTile(room.ceilingVariant) : oldPalette.FloorTile(room.floorVariant);
            if (old == null || tile == null) return false;
            float module = RoomBuilder.ModuleWidth(room);
            Vector2 Cell(GameObject t, PrefabPalette p)
            {
                Vector2 n = RoomBuilder.TileSize(t);
                bool fits = room.hasWalls && p.fitTilesToModule
                            && Mathf.Abs(n.x - module) <= module * p.tileFitTolerance && Mathf.Abs(n.y - module) <= module * p.tileFitTolerance;
                return fits ? new Vector2(module, module) : n;
            }
            return (Cell(old, oldPalette) - Cell(tile, tilePalette)).sqrMagnitude < 0.0001f;
        }

        // A wall of another width changes how many modules each side has.
        private static bool SameWidth(RoomOutline room, int wallVariant)
        {
            float width = RoomBuilder.ModuleWidth(room.palette, wallVariant), current = RoomBuilder.ModuleWidth(room);
            if (Mathf.Abs(width - current) <= 0.02f) return true;
            Debug.LogWarning($"[Level Maker] {room.name} : le nouveau mur fait {width:0.##} m de large au lieu de {current:0.##} m — " +
                             "la salle est reconstruite en entier, et le tracé n'en est plus un nombre entier : retrace la salle avec ce mur.", room);
            return false;
        }
    }
}
#endif
