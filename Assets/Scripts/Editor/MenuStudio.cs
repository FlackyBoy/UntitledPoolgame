#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UntitledPoolGame.Core;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.PoolEditor
{
    // Tools > Pool > Menu Studio: the graphic editor of the menu screens
    // (main menu pages, settings, pause, loading). The screen is drawn by the
    // same MenuRenderer as the game; elements are picked and dragged on it
    // (handle at the corner: size), added from the toolbar, and every
    // property — text, colours, picture, action, effects, Feel sequences —
    // edited on the right, with undo. Edits are a draft: the screen's asset
    // (Resources/Menus/<id>), which the game reads, only changes on
    // "Enregistrer"; switching screen, closing the window or entering Play
    // mode with a draft asks first.
    public class MenuStudio : EditorWindow
    {
        [MenuItem("Tools/Pool/Menu Studio")]
        private static void Open() => GetWindow<MenuStudio>("Menu Studio").minSize = new Vector2(1100, 640);

        private const float W = 1920f, H = 1080f;
        private const string SequenceFolder = "Assets/Feel/Sequences";

        // Draft mode: every edit goes to a copy of the screen ("screen"); the
        // real asset ("original") only changes on Save. The copy is kept in
        // the window as JSON (draftJson) so a script reload or entering Play
        // mode doesn't lose it.
        [SerializeField] private string screenId = MenuLayouts.Title;
        [SerializeField] private string draftJson;
        [SerializeField] private string draftOf;
        private MenuScreen original, screen;
        private SerializedObject so;
        private MenuView view;
        private int selected = -1;
        private bool unsaved;

        private VisualElement host, canvas, selectionBox, handle, list, inspector, banner;
        private PopupField<string> screens;
        private Toggle snap;
        private Label status, bannerText;

        private enum Drag { None, Move, Resize }
        private Drag drag;
        private Vector2 dragStartMouse, dragStartValue;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            saveChangesMessage = "Menu Studio : l'écran a des modifications non enregistrées. Les enregistrer ?";
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.playModeStateChanged -= OnPlayMode;
        }

        private void OnDestroy()
        {
            if (screen != null) DestroyImmediate(screen);
        }

        // Unity's own prompt when the window closes with unsaved changes.
        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            draftJson = null;
            base.DiscardChanges();
        }

        // The game reads the saved screens: offer to save before playing.
        private void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || !unsaved) return;
            if (EditorUtility.DisplayDialog("Menu Studio",
                    $"L'écran « {screenId} » a des modifications en brouillon. Le jeu utilise la version enregistrée.\n\nEnregistrer le brouillon pour le voir en jeu ?",
                    "Enregistrer", "Garder en brouillon"))
                Save();
        }

        private void OnUndo()
        {
            so?.Update();
            Rebuild();
            BuildInspector();
        }

        // ---------- Layout of the window ----------

        private void CreateGUI()
        {
            GameFlowTools.EnsureAssets();
            VisualElement root = rootVisualElement;

            var toolbar = new Toolbar();
            screens = new PopupField<string>(new List<string>(MenuLayouts.All), screenId) { tooltip = "L'écran à modifier." };
            screens.style.width = 170;
            screens.RegisterValueChangedCallback(e =>
            {
                if (!ConfirmLeave()) { screens.SetValueWithoutNotify(e.previousValue); return; }
                LoadScreen(e.newValue);
            });
            toolbar.Add(screens);
            toolbar.Add(new ToolbarSpacer());
            foreach (var (kind, label) in new[]
            {
                (MenuElementKind.Ball, "+ Bille"), (MenuElementKind.Card, "+ Carte"), (MenuElementKind.Text, "+ Texte"),
                (MenuElementKind.Image, "+ Image"), (MenuElementKind.Panel, "+ Panneau"), (MenuElementKind.Pill, "+ Pilule"),
            })
                toolbar.Add(new ToolbarButton(() => AddElement(kind)) { text = label });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(Duplicate) { text = "Dupliquer", tooltip = "Ctrl+D" });
            toolbar.Add(new ToolbarButton(Delete) { text = "Supprimer", tooltip = "Suppr" });
            toolbar.Add(new ToolbarButton(() => Reorder(-1)) { text = "▼ Derrière", tooltip = "Dessiné avant les autres (passe derrière)." });
            toolbar.Add(new ToolbarButton(() => Reorder(1)) { text = "▲ Devant", tooltip = "Dessiné après les autres (passe devant). L'ordre est aussi celui des choix de la pause." });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(Replay) { text = "▶ Rejouer les effets" });
            snap = new Toggle("Grille") { value = true, tooltip = "Aimante les positions au demi-pourcent. Maj en glissant : sans grille." };
            toolbar.Add(snap);
            toolbar.Add(new ToolbarButton(ResetScreen) { text = "Réinitialiser l'écran", tooltip = "Revient à la disposition d'origine de cet écran (annulable)." });
            root.Add(toolbar);

            // Draft banner: shown while the screen differs from its saved file.
            banner = new VisualElement();
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.paddingLeft = 8;
            banner.style.paddingTop = banner.style.paddingBottom = 3;
            banner.style.backgroundColor = new Color(0.55f, 0.38f, 0.05f);
            bannerText = new Label();
            bannerText.style.flexGrow = 1;
            bannerText.style.color = Color.white;
            bannerText.style.unityFontStyleAndWeight = FontStyle.Bold;
            banner.Add(bannerText);
            banner.Add(new Button(Save) { text = "Enregistrer", tooltip = "Écrit le brouillon dans le fichier de l'écran : le jeu l'utilisera." });
            banner.Add(new Button(Discard) { text = "Abandonner", tooltip = "Oublie le brouillon et revient à la version enregistrée." });
            root.Add(banner);

            var main = new VisualElement();
            main.style.flexDirection = FlexDirection.Row;
            main.style.flexGrow = 1;
            root.Add(main);

            var left = new ScrollView();
            left.style.width = 190;
            left.style.borderRightWidth = 1;
            left.style.borderRightColor = new Color(0f, 0f, 0f, 0.3f);
            list = left.contentContainer;
            main.Add(left);

            host = new VisualElement { focusable = true };
            host.style.flexGrow = 1;
            host.style.overflow = Overflow.Hidden;
            host.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            host.RegisterCallback<GeometryChangedEvent>(e => Fit());
            host.RegisterCallback<KeyDownEvent>(OnKey);
            main.Add(host);

            canvas = new VisualElement();
            canvas.style.position = Position.Absolute;
            canvas.style.width = W;
            canvas.style.height = H;
            canvas.style.transformOrigin = new TransformOrigin(0, 0);
            canvas.style.overflow = Overflow.Hidden;
            canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            host.Add(canvas);

            var right = new ScrollView();
            right.style.width = 400;
            right.style.borderLeftWidth = 1;
            right.style.borderLeftColor = new Color(0f, 0f, 0f, 0.3f);
            right.style.paddingLeft = right.style.paddingRight = 6;
            inspector = right.contentContainer;
            main.Add(right);

            status = new Label();
            status.style.paddingLeft = 6;
            status.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(status);

            rootVisualElement.schedule.Execute(UpdateSelectionBox).Every(50);
            rootVisualElement.schedule.Execute(CheckDraft).Every(400);
            LoadScreen(screenId, restoreDraft: true);
        }

        private void Fit()
        {
            if (host == null || canvas == null) return;
            float w = host.resolvedStyle.width, h = host.resolvedStyle.height;
            if (float.IsNaN(w) || w < 10f) return;
            float s = Mathf.Min(w / W, h / H);
            canvas.style.scale = new Scale(new Vector3(s, s, 1f));
            canvas.style.left = (w - W * s) / 2f;
            canvas.style.top = (h - H * s) / 2f;
        }

        // ---------- Screen ----------

        // restoreDraft: after a script reload / Play mode, the draft kept in
        // the window comes back instead of the saved file.
        private void LoadScreen(string id, bool restoreDraft = false)
        {
            screenId = id;
            original = AssetDatabase.LoadAssetAtPath<MenuScreen>($"Assets/Resources/Menus/{id}.asset");
            if (screen != null) DestroyImmediate(screen);
            screen = null;
            if (original == null) { status.text = $"Écran « {id} » introuvable : lance Tools > Pool > Ensure Config Assets Exist."; return; }
            screen = Instantiate(original);
            screen.name = original.name;
            screen.hideFlags = HideFlags.DontSave;
            if (restoreDraft && draftOf == id && !string.IsNullOrEmpty(draftJson))
            {
                EditorJsonUtility.FromJsonOverwrite(draftJson, screen);
                screen.name = original.name;
                screen.hideFlags = HideFlags.DontSave;
            }
            else
                draftJson = null;
            draftOf = id;
            so = new SerializedObject(screen);
            selected = -1;
            Rebuild();
            BuildInspector();
            Replay();
            CheckDraft();
            status.text = "Brouillon : rien ne change dans le jeu avant « Enregistrer ». Clique sur un élément pour le choisir, glisse-le, tire le coin jaune pour la taille. Flèches, Suppr, Ctrl+D.";
        }

        // Is the draft different from the saved file? Kept in the window too.
        private void CheckDraft()
        {
            if (screen == null || original == null || banner == null) return;
            // Compared with the same flags: only the content counts.
            screen.hideFlags = original.hideFlags;
            string draft = EditorJsonUtility.ToJson(screen);
            screen.hideFlags = HideFlags.DontSave;
            unsaved = draft != EditorJsonUtility.ToJson(original);
            draftJson = unsaved ? draft : null;
            hasUnsavedChanges = unsaved;
            banner.style.display = unsaved ? DisplayStyle.Flex : DisplayStyle.None;
            bannerText.text = $"Brouillon de « {screenId} » : modifications non enregistrées (le jeu utilise encore la version enregistrée).";
            titleContent.text = unsaved ? "Menu Studio *" : "Menu Studio";
        }

        private void Save()
        {
            if (screen == null || original == null) return;
            Undo.RecordObject(original, "Enregistrer l'écran");
            string keepName = original.name;
            HideFlags keepFlags = original.hideFlags;
            // The draft's "don't save" flag must not reach the real file.
            screen.hideFlags = keepFlags;
            EditorUtility.CopySerialized(screen, original);
            screen.hideFlags = HideFlags.DontSave;
            original.name = keepName;
            original.hideFlags = keepFlags;
            EditorUtility.SetDirty(original);
            AssetDatabase.SaveAssetIfDirty(original);
            CheckDraft();
            status.text = $"Écran « {screenId} » enregistré.";
        }

        private void Discard()
        {
            if (!unsaved) return;
            if (!EditorUtility.DisplayDialog("Abandonner le brouillon ?",
                    $"Les modifications de « {screenId} » depuis le dernier enregistrement seront perdues.", "Abandonner", "Annuler"))
                return;
            draftJson = null;
            LoadScreen(screenId);
            status.text = "Brouillon abandonné : retour à la version enregistrée.";
        }

        // Before showing another screen: save, discard or stay.
        private bool ConfirmLeave()
        {
            CheckDraft();
            if (!unsaved) return true;
            int choice = EditorUtility.DisplayDialogComplex("Modifications non enregistrées",
                $"L'écran « {screenId} » a des modifications en brouillon.", "Enregistrer", "Rester", "Abandonner");
            if (choice == 1) return false;
            if (choice == 0) Save();
            draftJson = null;
            return true;
        }

        private void Rebuild()
        {
            if (canvas == null) return;
            view?.Stop();
            canvas.Clear();
            if (screen == null) return;
            view = MenuRenderer.Build(canvas, screen, editor: true);
            view.openedAt = Time.realtimeSinceStartup - 100f;   // final state; ▶ replays
            selectionBox = new VisualElement { pickingMode = PickingMode.Ignore };
            selectionBox.style.position = Position.Absolute;
            K.Border(selectionBox, 3, new Color(1f, 0.82f, 0.25f));
            handle = new VisualElement { pickingMode = PickingMode.Ignore };
            handle.style.position = Position.Absolute;
            handle.style.width = handle.style.height = 22;
            handle.style.right = handle.style.bottom = -12;
            handle.style.backgroundColor = new Color(1f, 0.82f, 0.25f);
            selectionBox.Add(handle);
            canvas.Add(selectionBox);
            RebuildList();
            UpdateSelectionBox();
        }

        private void Replay() => view?.Replay();

        private void RebuildList()
        {
            list.Clear();
            if (screen == null) return;
            var header = new Label("Éléments (de derrière à devant)");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 4;
            header.style.marginBottom = 4;
            list.Add(header);
            var screenButton = new Button(() => Select(-1)) { text = "⚙ Écran (fond, queue, retour)" };
            if (selected < 0) screenButton.style.backgroundColor = new Color(0.25f, 0.45f, 0.7f);
            list.Add(screenButton);
            for (int i = 0; i < screen.elements.Count; i++)
            {
                MenuElement e = screen.elements[i];
                if (e == null) continue;
                int index = i;
                string action = e.action != MenuAction.None ? " ▸" : "";
                var b = new Button(() => Select(index)) { text = $"{e.id}  ({Kind(e.kind)}){action}" };
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                if (!e.visible) b.style.opacity = 0.5f;
                if (i == selected) b.style.backgroundColor = new Color(0.25f, 0.45f, 0.7f);
                list.Add(b);
            }
        }

        private static string Kind(MenuElementKind k) => k switch
        {
            MenuElementKind.Ball => "bille", MenuElementKind.Card => "carte", MenuElementKind.Text => "texte",
            MenuElementKind.Image => "image", MenuElementKind.Panel => "panneau", MenuElementKind.Pill => "pilule",
            _ => "zone du jeu",
        };

        private void Select(int index)
        {
            selected = index;
            RebuildList();
            BuildInspector();
            UpdateSelectionBox();
            host.Focus();
        }

        // ---------- Inspector ----------

        // Rebuilt on each selection: a fresh container, so the change
        // tracking of the previous one goes away with it.
        private VisualElement panel;

        private void BuildInspector()
        {
            inspector.Clear();
            if (so == null) return;
            so.Update();
            panel = new VisualElement();
            inspector.Add(panel);
            if (selected < 0 || selected >= screen.elements.Count)
            {
                Title("Écran « " + screenId + " »");
                foreach (string p in new[] { "backdrop", "backgroundColor", "backgroundImage", "barLights", "table",
                                             "hasCue", "cuePosition", "cueSize", "backScreen", "feelOnOpen" })
                    panel.Add(new PropertyField(so.FindProperty(p)));
                panel.Add(FeelButton("Créer la séquence Feel d'ouverture", "feelOnOpen", null));
                Help("Les éléments avec une action sont les choix du joueur. Ceux que le jeu remplit (context, levels, rules, slot1…) gardent leur nom.");
            }
            else
            {
                MenuElement e = screen.elements[selected];
                Title($"{e.id} ({Kind(e.kind)})");
                SerializedProperty element = so.FindProperty($"elements.Array.data[{selected}]");
                SerializedProperty end = element.GetEndProperty();
                SerializedProperty child = element.Copy();
                if (child.NextVisible(true))
                    do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        panel.Add(new PropertyField(child.Copy()));
                    } while (child.NextVisible(false));
                panel.Add(FeelButton("Créer la séquence Feel « visé »", null, "feelOnFocus"));
                panel.Add(FeelButton("Créer la séquence Feel « choisi »", null, "feelOnPress"));
                Help("Texte : balises de texte animé acceptées (<wave>, <shake>, <pop>…), à essayer dans Tools > Pool > Text FX Studio.");
            }
            panel.Bind(so);
            panel.TrackSerializedObjectValue(so, _ => Rebuild());
        }

        private void Title(string text)
        {
            var l = new Label(text);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.fontSize = 14;
            l.style.marginTop = 6;
            l.style.marginBottom = 6;
            panel.Add(l);
        }

        private void Help(string text)
        {
            var box = new HelpBox(text, HelpBoxMessageType.Info);
            box.style.marginTop = 8;
            panel.Add(box);
        }

        // ---------- Pointer ----------

        private Vector2 Local(Vector3 world) => canvas.WorldToLocal((Vector2)world);

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || view == null) return;
            host.Focus();
            Vector2 world = evt.position;

            if (selected >= 0 && handle != null && selectionBox.style.display != DisplayStyle.None && handle.worldBound.Contains(world))
            {
                StartDrag(Drag.Resize, evt, screen.elements[selected].size);
                return;
            }
            for (int i = view.items.Count - 1; i >= 0; i--)
            {
                MenuNode item = view.items[i];
                if (!item.holder.worldBound.Contains(world)) continue;
                if (item.index != selected) Select(item.index);
                StartDrag(Drag.Move, evt, screen.elements[selected].position);
                return;
            }
            Select(-1);
        }

        private void StartDrag(Drag mode, PointerDownEvent evt, Vector2 startValue)
        {
            Undo.RecordObject(screen, mode == Drag.Move ? "Déplacer un élément" : "Redimensionner un élément");
            drag = mode;
            dragStartMouse = Local(evt.position);
            dragStartValue = startValue;
            canvas.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (drag == Drag.None || selected < 0) return;
            MenuElement e = screen.elements[selected];
            Vector2 delta = Local(evt.position) - dragStartMouse;
            bool grid = snap.value && !evt.shiftKey;
            if (drag == Drag.Move)
            {
                Vector2 p = dragStartValue + new Vector2(delta.x / W * 100f, delta.y / H * 100f);
                if (grid) p = new Vector2(Mathf.Round(p.x * 2f) / 2f, Mathf.Round(p.y * 2f) / 2f);
                e.position = p;
                MenuNode item = view.items.Find(i => i.index == selected);
                if (item != null)
                {
                    item.holder.style.left = Length.Percent(p.x);
                    item.holder.style.top = Length.Percent(p.y);
                }
                status.text = $"{e.id} : {p.x:0.#} %, {p.y:0.#} %";
            }
            else
            {
                // The handle sits on the corner of a centered element: the
                // size grows twice as fast as the mouse moves.
                Vector2 s = dragStartValue + delta * 2f;
                if (grid) s = new Vector2(Mathf.Round(s.x / 5f) * 5f, Mathf.Round(s.y / 5f) * 5f);
                s = Vector2.Max(s, new Vector2(10f, 10f));
                if (e.kind == MenuElementKind.Ball) s.y = s.x;
                e.size = s;
                status.text = $"{e.id} : {s.x:0} × {s.y:0} px";
                Rebuild();
            }
            UpdateSelectionBox();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (drag == Drag.None) return;
            drag = Drag.None;
            canvas.ReleasePointer(evt.pointerId);
            EditorUtility.SetDirty(screen);
            so.Update();
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (screen == null) return;
            if (evt.keyCode == KeyCode.Delete) { Delete(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.D && (evt.ctrlKey || evt.commandKey)) { Duplicate(); evt.StopPropagation(); return; }
            if (selected < 0) return;
            float step = evt.shiftKey ? 2f : 0.5f;
            Vector2 move = evt.keyCode switch
            {
                KeyCode.LeftArrow => new Vector2(-step, 0f), KeyCode.RightArrow => new Vector2(step, 0f),
                KeyCode.UpArrow => new Vector2(0f, -step), KeyCode.DownArrow => new Vector2(0f, step),
                _ => Vector2.zero,
            };
            if (move == Vector2.zero) return;
            Undo.RecordObject(screen, "Déplacer un élément");
            screen.elements[selected].position += move;
            EditorUtility.SetDirty(screen);
            so.Update();
            Rebuild();
            evt.StopPropagation();
        }

        private void UpdateSelectionBox()
        {
            if (selectionBox == null || view == null) return;
            MenuNode item = selected >= 0 ? view.items.Find(i => i.index == selected) : null;
            if (item == null) { selectionBox.style.display = DisplayStyle.None; return; }
            Rect r = item.holder.worldBound;
            if (r.width < 1f && item.visual != null) r = item.visual.worldBound;
            Vector2 min = canvas.WorldToLocal(r.min), max = canvas.WorldToLocal(r.max);
            selectionBox.style.display = DisplayStyle.Flex;
            selectionBox.style.left = min.x - 4f;
            selectionBox.style.top = min.y - 4f;
            selectionBox.style.width = Mathf.Max(8f, max.x - min.x + 8f);
            selectionBox.style.height = Mathf.Max(8f, max.y - min.y + 8f);
            selectionBox.BringToFront();
        }

        // ---------- Editing ----------

        private void AddElement(MenuElementKind kind)
        {
            if (screen == null) return;
            Undo.RecordObject(screen, "Ajouter un élément");
            MenuElement e = NewElement(kind);
            e.id = UniqueId(Kind(kind));
            screen.elements.Add(e);
            Commit();
            Select(screen.elements.Count - 1);
        }

        private static MenuElement NewElement(MenuElementKind kind)
        {
            var e = new MenuElement { kind = kind, position = new Vector2(50f, 50f), borderColor = K.Ink, textColor = K.Chalk };
            switch (kind)
            {
                case MenuElementKind.Ball:
                    e.size = new Vector2(110f, 110f); e.color = K.Hex("#d8261c"); e.number = "1"; e.text = "Bille"; break;
                case MenuElementKind.Card:
                    e.size = new Vector2(330f, 300f); e.color = K.Card; e.text = "Carte"; e.titleFont = true; e.textSize = 40f;
                    e.textColor = K.Ink; e.textShadow = false; e.focusScale = 0.06f; break;
                case MenuElementKind.Text:
                    e.size = Vector2.zero; e.text = "Texte"; e.textSize = 60f; break;
                case MenuElementKind.Image:
                    e.size = new Vector2(300f, 200f); e.color = Color.white; break;
                case MenuElementKind.Panel:
                    e.size = new Vector2(500f, 200f); e.color = K.Cube; e.text = "Panneau"; e.textColor = Color.white;
                    e.textShadow = false; e.focusScale = 0.06f; break;
                case MenuElementKind.Pill:
                    e.size = new Vector2(480f, 80f); e.color = K.Hex("#f4c20d"); e.number = "1"; e.text = "Choix"; e.titleFont = true;
                    e.textSize = 40f; e.textColor = K.Ink; e.textShadow = false; e.focusScale = 0.05f; break;
            }
            return e;
        }

        private string UniqueId(string baseName)
        {
            for (int n = 1; ; n++)
            {
                string id = baseName + n;
                if (screen.Find(id) == null) return id;
            }
        }

        private void Duplicate()
        {
            if (screen == null || selected < 0) return;
            Undo.RecordObject(screen, "Dupliquer un élément");
            MenuElement copy = screen.elements[selected].Clone();
            copy.id = UniqueId(copy.id + "-copie");
            copy.position += new Vector2(2f, 2f);
            screen.elements.Insert(selected + 1, copy);
            Commit();
            Select(selected + 1);
        }

        private void Delete()
        {
            if (screen == null || selected < 0) return;
            MenuElement e = screen.elements[selected];
            if (e.kind == MenuElementKind.Slot && !EditorUtility.DisplayDialog("Supprimer une zone du jeu ?",
                    $"« {e.id} » est remplie par le jeu ; sans elle, son contenu ne s'affichera plus ou reprendra sa place par défaut.", "Supprimer", "Annuler"))
                return;
            Undo.RecordObject(screen, "Supprimer un élément");
            screen.elements.RemoveAt(selected);
            Commit();
            Select(-1);
        }

        private void Reorder(int direction)
        {
            if (screen == null || selected < 0) return;
            int to = selected + direction;
            if (to < 0 || to >= screen.elements.Count) return;
            Undo.RecordObject(screen, "Changer l'ordre");
            (screen.elements[selected], screen.elements[to]) = (screen.elements[to], screen.elements[selected]);
            Commit();
            Select(to);
        }

        private void ResetScreen()
        {
            if (screen == null || !EditorUtility.DisplayDialog("Réinitialiser l'écran ?",
                    $"L'écran « {screenId} » reprend sa disposition d'origine. Annulable avec Ctrl+Z.", "Réinitialiser", "Annuler"))
                return;
            Undo.RecordObject(screen, "Réinitialiser l'écran");
            MenuScreen fresh = MenuDefaults.Create(screenId);
            string keepName = screen.name;
            EditorUtility.CopySerialized(fresh, screen);
            screen.name = keepName;
            DestroyImmediate(fresh);
            selected = -1;
            Commit();
            BuildInspector();
            Replay();
        }

        private void Commit()
        {
            EditorUtility.SetDirty(screen);
            so.Update();
            Rebuild();
        }

        // ---------- Feel ----------

        // A new empty Feel sequence for this screen / element, assigned to
        // the field and selected, to be filled in Feel's inspector.
        private VisualElement FeelButton(string label, string screenField, string elementField)
        {
            return new Button(() =>
            {
                if (!AssetDatabase.IsValidFolder("Assets/Feel")) AssetDatabase.CreateFolder("Assets", "Feel");
                if (!AssetDatabase.IsValidFolder(SequenceFolder)) AssetDatabase.CreateFolder("Assets/Feel", "Sequences");
                string owner = elementField != null && selected >= 0 ? screen.elements[selected].id : "ecran";
                string suffix = elementField == "feelOnFocus" ? "Vise" : elementField == "feelOnPress" ? "Choisi" : "Ouverture";
                string path = AssetDatabase.GenerateUniqueAssetPath($"{SequenceFolder}/Menu_{screenId}_{owner}_{suffix}.prefab");
                var go = new GameObject(Path.GetFileNameWithoutExtension(path));
                try
                {
                    go.AddComponent<MMF_Player>();
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                    MMF_Player player = prefab.GetComponent<MMF_Player>();
                    Undo.RecordObject(screen, "Lier une séquence Feel");
                    if (elementField == "feelOnFocus") screen.elements[selected].feelOnFocus = player;
                    else if (elementField == "feelOnPress") screen.elements[selected].feelOnPress = player;
                    else screen.feelOnOpen = player;
                    Commit();
                    BuildInspector();
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                    status.text = $"Séquence créée : {path}. Ajoute-lui ses feedbacks (son, vibration, Texte du HUD…) dans l'Inspector.";
                }
                finally
                {
                    DestroyImmediate(go);
                }
            }) { text = label, tooltip = "Crée une séquence Feel vide, la relie ici et la sélectionne pour la remplir dans l'Inspector de Feel." };
        }
    }
}
#endif
