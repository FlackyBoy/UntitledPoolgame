#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.PoolEditor
{
    // Tools > Pool > Text FX Studio: write an animated text (TextFx tags)
    // with buttons, see it play live in a preview that looks like the game
    // (fonts, colours, background), edit the real texts of the settings
    // assets (HUD, menu, loading, levels) and save them back, tune
    // TextFxSettings next to the preview, and turn a text into a Feel
    // sequence (MMF_Player prefab with our MMF_PoolHudText) linked to a moment
    // of the match in GameFeelSettings. Works outside Play mode.
    public class TextFxStudio : EditorWindow
    {
        [MenuItem("Tools/Pool/Text FX Studio")]
        private static void Open() => GetWindow<TextFxStudio>("Text FX Studio").minSize = new Vector2(760, 620);

        private const string SequenceFolder = "Assets/Feel/Sequences";

        private enum PlayMode { Fixe, Arrivée, MachineÀÉcrire }
        private enum Backdrop { Bar, Tapis, Carte, Gris }

        // A text of a settings asset that can be loaded and saved back.
        private class Source
        {
            public string label;
            public UnityEngine.Object asset;
            public string path;
        }

        private readonly List<Source> sources = new List<Source>();
        private Source current;

        private TextField markup;
        private Label preview, info;
        private VisualElement stage;
        private Slider amplitude, speed, fontSize;
        private EnumField playMode, backdrop, feelEvent;
        private ColorField textColor;
        private Toggle titleFont, outline, loop;
        private PopupField<string> sourcePicker;
        private int selStart, selEnd;
        private double replayAt = -1;

        private void CreateGUI()
        {
            GameFlowTools.EnsureAssets();
            CollectSources();
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = 8;

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            root.Add(scroll);

            // ---------- Source ----------
            var sourceRow = Row(scroll);
            var names = new List<string> { "— Texte libre —" };
            foreach (Source s in sources) names.Add(s.label);
            sourcePicker = new PopupField<string>("Texte du jeu", names, 0);
            sourcePicker.style.flexGrow = 1;
            sourcePicker.tooltip = "Charger un texte des réglages (HUD, menu, chargement, niveaux) pour le modifier et l'enregistrer.";
            sourcePicker.RegisterValueChangedCallback(e => LoadSource(sourcePicker.index));
            sourceRow.Add(sourcePicker);
            var save = new Button(SaveSource) { text = "Enregistrer dans le jeu", tooltip = "Écrit ce texte dans le réglage choisi (annulable avec Ctrl+Z)." };
            sourceRow.Add(save);

            // ---------- Text ----------
            markup = new TextField { multiline = true, value = "<pop>C'est</pop> <wave>parti</wave> <shake>!</shake>" };
            markup.style.minHeight = 64;
            markup.style.whiteSpace = WhiteSpace.Normal;
            markup.RegisterValueChangedCallback(e => Replay());
            markup.RegisterCallback<KeyUpEvent>(e => RememberSelection());
            markup.RegisterCallback<PointerUpEvent>(e => RememberSelection());
            scroll.Add(markup);

            // ---------- Tag buttons ----------
            Section(scroll, "Effets (tant que le texte est affiché) — sélectionne des lettres puis clique ; sans sélection, tout le texte");
            var effects = Row(scroll);
            foreach (var (tag, tip) in new[]
            {
                ("wave", "ondule"), ("bounce", "rebondit"), ("shake", "tremble"), ("wiggle", "gigote"),
                ("pulse", "grossit / rapetisse"), ("swing", "se balance"), ("rainbow", "arc-en-ciel"),
            })
                effects.Add(new Button(() => Wrap(tag, true)) { text = tag, tooltip = tip });

            Section(scroll, "Apparition des lettres");
            var entrances = Row(scroll);
            foreach (var (tag, tip) in new[]
            {
                ("pop", "grossit avec un rebond"), ("drop", "tombe d'en haut"), ("fade", "fondu"),
                ("slide", "glisse depuis la droite"), ("grow", "pousse depuis le bas"),
            })
                entrances.Add(new Button(() => Wrap(tag, false)) { text = tag, tooltip = tip });
            entrances.Add(new Button(() => Insert("<pause=0.5>")) { text = "pause 0,5 s", tooltip = "Machine à écrire : attend une demi-seconde à cet endroit." });
            entrances.Add(new Button(() => Wrap("speed=2", false, "speed")) { text = "speed ×2", tooltip = "Machine à écrire : tape deux fois plus vite sur la sélection." });
            entrances.Add(new Button(StripTags) { text = "Tout retirer", tooltip = "Enlève toutes les balises d'animation." });

            var tuning = Row(scroll);
            amplitude = new Slider("Force (a=)", 0.2f, 3f) { value = 1f, showInputField = true, tooltip = "Force ajoutée à la balise posée (1 = normal, pas écrite)." };
            amplitude.style.flexGrow = 1;
            speed = new Slider("Vitesse (s=)", 0.2f, 3f) { value = 1f, showInputField = true, tooltip = "Vitesse ajoutée à la balise posée (1 = normal, pas écrite)." };
            speed.style.flexGrow = 1;
            tuning.Add(amplitude);
            tuning.Add(speed);

            // ---------- Preview ----------
            Section(scroll, "Prévisualisation");
            var options = Row(scroll);
            playMode = new EnumField("Lecture", PlayMode.Arrivée) { tooltip = "Fixe : effets seuls. Arrivée : lettres une à une. Machine à écrire : comme les astuces du chargement." };
            playMode.RegisterValueChangedCallback(e => Replay());
            options.Add(playMode);
            backdrop = new EnumField("Fond", Backdrop.Bar);
            backdrop.RegisterValueChangedCallback(e => ApplyLook());
            options.Add(backdrop);
            loop = new Toggle("Boucle") { value = true };
            options.Add(loop);
            options.Add(new Button(Replay) { text = "▶ Rejouer" });
            options.Add(new Button(() => TextFx.Hide(preview)) { text = "Sortie" });

            var look = Row(scroll);
            fontSize = new Slider("Taille", 16f, 160f) { value = 90f, showInputField = true };
            fontSize.style.flexGrow = 1;
            fontSize.RegisterValueChangedCallback(e => ApplyLook());
            look.Add(fontSize);
            titleFont = new Toggle("Police titre") { value = true };
            titleFont.RegisterValueChangedCallback(e => ApplyLook());
            look.Add(titleFont);
            outline = new Toggle("Contour") { value = true };
            outline.RegisterValueChangedCallback(e => ApplyLook());
            look.Add(outline);
            textColor = new ColorField("Couleur") { value = MenuSettings.Instance.yellow };
            textColor.RegisterValueChangedCallback(e => ApplyLook());
            look.Add(textColor);

            stage = new VisualElement();
            stage.style.height = 260;
            stage.style.marginTop = 6;
            stage.style.alignItems = Align.Center;
            stage.style.justifyContent = Justify.Center;
            stage.style.overflow = Overflow.Hidden;
            stage.style.borderTopLeftRadius = stage.style.borderTopRightRadius = stage.style.borderBottomLeftRadius = stage.style.borderBottomRightRadius = 10;
            preview = new Label();
            preview.style.unityTextAlign = TextAnchor.MiddleCenter;
            preview.style.whiteSpace = WhiteSpace.Normal;
            preview.style.maxWidth = Length.Percent(92);
            stage.Add(preview);
            scroll.Add(stage);
            info = new Label();
            info.style.unityFontStyleAndWeight = FontStyle.Italic;
            info.style.marginTop = 4;
            scroll.Add(info);

            // ---------- Feel ----------
            Section(scroll, "Feel : jouer ce texte à un moment de la partie");
            var feelRow = Row(scroll);
            feelEvent = new EnumField("Moment", GameFeelEvent.Foul);
            feelEvent.style.flexGrow = 1;
            feelRow.Add(feelEvent);
            feelRow.Add(new Button(CreateFeelSequence)
            {
                text = "Créer la séquence Feel",
                tooltip = "Crée un prefab avec un MMF Player contenant ce texte (feedback « Texte du HUD »), le relie au moment choisi dans GameFeelSettings et le sélectionne : ajoute-lui ensuite son, vibration, arrêt sur image… dans l'Inspector de Feel.",
            });
            feelRow.Add(new Button(() =>
            {
                if (!Application.isPlaying) { info.text = "En jeu seulement : lance le Play pour voir le texte sur le HUD."; return; }
                MatchHud.ShowShout(-1, markup.value, textColor.value, false);
            }) { text = "Tester sur le HUD (Play)" });

            // ---------- Settings ----------
            Section(scroll, "Réglages communs (TextFxSettings) — s'appliquent à tous les textes du jeu");
            TextFxSettings settings = AssetDatabase.LoadAssetAtPath<TextFxSettings>("Assets/Resources/TextFxSettings.asset");
            if (settings != null) scroll.Add(new InspectorElement(settings));

            ApplyLook();
            Replay();
            rootVisualElement.schedule.Execute(LoopTick).Every(200);
        }

        // ---------- Building helpers ----------

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            parent.Add(row);
            return row;
        }

        private static void Section(VisualElement parent, string title)
        {
            var l = new Label(title);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 10;
            l.style.marginBottom = 2;
            parent.Add(l);
        }

        // ---------- Editing ----------

        private void RememberSelection()
        {
            selStart = Mathf.Min(markup.cursorIndex, markup.selectIndex);
            selEnd = Mathf.Max(markup.cursorIndex, markup.selectIndex);
        }

        private void Wrap(string tag, bool tunable, string closing = null)
        {
            string text = markup.value ?? "";
            int start = Mathf.Clamp(selStart, 0, text.Length), end = Mathf.Clamp(selEnd, 0, text.Length);
            if (end <= start) { start = 0; end = text.Length; }
            string open = tag;
            if (tunable)
            {
                if (Mathf.Abs(amplitude.value - 1f) > 0.01f) open += " a=" + amplitude.value.ToString("0.##", CultureInfo.InvariantCulture);
                if (Mathf.Abs(speed.value - 1f) > 0.01f) open += " s=" + speed.value.ToString("0.##", CultureInfo.InvariantCulture);
            }
            markup.value = text.Substring(0, start) + "<" + open + ">" + text.Substring(start, end - start) + "</" + (closing ?? tag) + ">" + text.Substring(end);
            selStart = selEnd = 0;
        }

        private void Insert(string snippet)
        {
            string text = markup.value ?? "";
            int at = Mathf.Clamp(selEnd > 0 ? selEnd : markup.cursorIndex, 0, text.Length);
            markup.value = text.Insert(at, snippet);
        }

        private void StripTags()
        {
            // Our tags only; Unity's rich text (<b>, <color>…) stays.
            markup.value = TextFx.Strip(markup.value);
        }

        // ---------- Preview ----------

        private void ApplyLook()
        {
            if (stage == null) return;
            MenuSettings m = MenuSettings.Instance;
            stage.style.backgroundImage = StyleKeyword.None;
            switch ((Backdrop)backdrop.value)
            {
                case Backdrop.Bar: stage.style.backgroundColor = new Color(0.16f, 0.1f, 0.07f); break;
                case Backdrop.Tapis: stage.style.backgroundColor = m.felt; break;
                case Backdrop.Carte: stage.style.backgroundColor = m.card; break;
                default: stage.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f); break;
            }
            Font font = titleFont.value ? UiKit.TitleFont : UiKit.BodyFont;
            if (font != null) preview.style.unityFontDefinition = FontDefinition.FromFont(font);
            preview.style.fontSize = fontSize.value;
            preview.style.color = textColor.value;
            preview.style.unityTextOutlineWidth = outline.value ? Mathf.Max(1f, fontSize.value / 30f) : 0f;
            preview.style.unityTextOutlineColor = m.ink;
        }

        private void Replay()
        {
            if (preview == null) return;
            string text = markup.value ?? "";
            switch ((PlayMode)playMode.value)
            {
                case PlayMode.Fixe: TextFx.Detach(preview); TextFx.Set(preview, text); break;
                case PlayMode.Arrivée: TextFx.Reveal(preview, text); break;
                default: TextFx.Type(preview, text); break;
            }
            replayAt = -1;
            info.text = current != null ? $"Texte du jeu : {current.label}" : "Texte libre (rien n'est enregistré).";
        }

        // Replays once the text is in and has stayed a moment.
        private void LoopTick()
        {
            if (preview == null || loop == null || !loop.value || (PlayMode)playMode.value == PlayMode.Fixe) return;
            double now = EditorApplication.timeSinceStartup;
            if (TextFx.IsAnimating(preview)) { replayAt = -1; return; }
            if (replayAt < 0) replayAt = now + 1.5;
            else if (now >= replayAt) Replay();
        }

        // ---------- Game texts ----------

        private void CollectSources()
        {
            sources.Clear();
            foreach (var (path, prefix) in new[]
            {
                ("Assets/Resources/HudSettings.asset", "HUD"),
                ("Assets/Resources/MenuSettings.asset", "Menu"),
                ("Assets/Resources/LoadingScreenSettings.asset", "Chargement"),
                ("Assets/Resources/GameFlowSettings.asset", "Niveaux"),
            })
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                SerializedProperty p = so.GetIterator();
                bool enter = true;
                while (p.NextVisible(enter))
                {
                    enter = true;
                    if (p.name == "m_Script") { enter = false; continue; }
                    if (p.propertyType != SerializedPropertyType.String) continue;
                    if (p.name == "sceneName" || p.name == "menuScene" || p.name == "id") continue;
                    sources.Add(new Source { asset = asset, path = p.propertyPath, label = prefix + " / " + Pretty(p.propertyPath) });
                }
            }
        }

        private static string Pretty(string propertyPath)
        {
            string s = propertyPath.Replace(".Array.data[", " [");
            return string.Join(" / ", Array.ConvertAll(s.Split('.'), ObjectNames.NicifyVariableName));
        }

        private void LoadSource(int index)
        {
            current = index > 0 && index - 1 < sources.Count ? sources[index - 1] : null;
            if (current == null) { Replay(); return; }
            var so = new SerializedObject(current.asset);
            SerializedProperty p = so.FindProperty(current.path);
            if (p != null) markup.SetValueWithoutNotify(p.stringValue);
            Replay();
        }

        private void SaveSource()
        {
            if (current == null) { info.text = "Choisis d'abord un texte du jeu dans la liste du haut."; return; }
            var so = new SerializedObject(current.asset);
            SerializedProperty p = so.FindProperty(current.path);
            if (p == null) return;
            p.stringValue = markup.value;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            info.text = $"Enregistré : {current.label}";
        }

        // ---------- Feel ----------

        private void CreateFeelSequence()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Feel")) AssetDatabase.CreateFolder("Assets", "Feel");
            if (!AssetDatabase.IsValidFolder(SequenceFolder)) AssetDatabase.CreateFolder("Assets/Feel", "Sequences");
            GameFeelEvent gameEvent = (GameFeelEvent)feelEvent.value;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{SequenceFolder}/{gameEvent}.prefab");

            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                MMF_Player player = go.AddComponent<MMF_Player>();
                var text = (MMF_PoolHudText)player.AddFeedback(typeof(MMF_PoolHudText));
                text.text = markup.value;
                text.customColor = true;
                text.color = textColor.value;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);

                GameFeelSettings settings = AssetDatabase.LoadAssetAtPath<GameFeelSettings>("Assets/Resources/GameFeelSettings.asset");
                if (settings != null)
                {
                    Undo.RecordObject(settings, "Lier une séquence Feel");
                    settings.entries.Add(new GameFeelEntry { gameEvent = gameEvent, feedback = prefab.GetComponent<MMF_Player>(), intensity = 1f });
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                info.text = $"Séquence créée : {path}, jouée au moment « {gameEvent} » (GameFeelSettings). Ajoute-lui son, vibration, arrêt sur image… dans son Inspector.";
            }
            finally
            {
                DestroyImmediate(go);
            }
        }
    }
}
#endif
