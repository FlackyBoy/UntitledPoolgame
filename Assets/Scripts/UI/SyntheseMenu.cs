using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.Core
{
    // The main menu of the validated UI direction (prototypes
    // docs/ui/synthese.html and docs/ui/parcours.html, "Menu A · billes"):
    // the pool table in the bar at night; on the table pages every choice is
    // a ball that the cue aims at and shoots, the other choices are "pop"
    // cards and pills.
    //
    // Every screen (Title, NewGame, Load, Level, Mode, Multiplayer, Lobby,
    // Settings) is a MenuScreen drawn by MenuRenderer: elements, positions,
    // colours, texts, actions, effects and Feel sequences are edited in
    // Tools > Pool > Menu Studio. This class brings them to life: aiming
    // and shooting, the action of each element, and the parts it fills itself
    // (elements found by id: "context", "levels", "rules", "slot1"/"slot2"
    // and their "-label", "aim-hint").
    //
    // Replaces PoolMatchRules' OnGUI mode select (PoolMatchRules.ExternalMenu)
    // and starts the match through PoolMatchRules.RequestStart or the
    // LevelLoader. Mouse, keyboard or any gamepad.
    public class SyntheseMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        // Shown in the main menu scene (MainMenuScene marker), and in a level
        // played straight from the editor (a PoolMatchRules, no session from
        // the menu) — not in a level the menu just launched.
        private static void TryCreate()
        {
            if (FindAnyObjectByType<SyntheseMenu>() != null) return;
            bool menuScene = FindAnyObjectByType<MainMenuScene>() != null;
            bool levelPlayedDirectly = FindAnyObjectByType<PoolMatchRules>() != null && !GameSession.Active;
            if (!menuScene && !levelPlayedDirectly) return;
            var go = new GameObject("Menu (Synthèse)");
            go.SetActive(false);
            go.AddComponent<UIDocument>().panelSettings = K.CreatePanel(100);
            go.AddComponent<SyntheseMenu>();
            go.SetActive(true);
        }

        // ---------- State ----------

        private class Option
        {
            public VisualElement visual;      // what grows when focused (and flies when shot)
            public Vector2 pos;               // percent of the screen, center
            public float radius;              // px, for the aim line
            public Action shoot;
            public float focus;               // 0..1, eased
            public bool card;                 // not a ball: no flight
            public float focusScale = 0.18f;
            public MenuElement data;          // null for the generated level cards
            public Color baseColor;
        }

        private class Page
        {
            public string id;
            public MenuView view;
            public readonly List<Option> options = new List<Option>();
            public int focused;
            public Label context;             // "Just for fun · Le bar"…
            public VisualElement aimHint;
            public VisualElement Root => view.root;
            public bool HasCue => view.cueBall != null;
            public Vector2 CuePos => view.screen.cuePosition;
            public float CueRadius => view.screen.cueSize / 2f;
        }

        private MenuSettings cfg;
        private UIDocument document;
        private VisualElement root, stage, aimLine, cueStick, toast;
        private readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
        private Page title, level, mode, lobby, settingsPage, current;
        private KeyBindingsPanel settings;
        private Label ruleTitle, ruleText, ruleOption;
        private readonly VisualElement[] slots = new VisualElement[2];
        private readonly Label[] slotLabels = new Label[2];
        private int knownPlayers = -1;

        private bool local;                   // multiplayer path (the lobby) or Just for fun
        private string levelName = "";
        private int levelIndex;

        // J1 is whoever drives the menu (the device of the last confirm);
        // J2 joins in the lobby by pressing a button on another device.
        // Handed to the level through GameSession.
        private InputDevice menuDevice;
        private readonly InputDevice[] slotDevices = new InputDevice[2];
        private bool joinedThisFrame;
        private PoolPartyMode chosenParty = PoolPartyMode.Classic;
        private int targetScore = 25;

        // Shot animation: pull back, thrust, the ball flies off, then the action.
        private Option shooting, lastFocused;
        private float shotTime;
        private float toastTime;
        private Action pendingAction;
        private float pendingTime;
        private Vector2 lastMouse;
        private readonly Dictionary<Gamepad, Vector2> padLatch = new Dictionary<Gamepad, Vector2>();

        private void OnEnable()
        {
            PoolMatchRules.ExternalMenu = true;
            cfg = MenuSettings.Instance;
            if (K.BodyFont == null || K.TitleFont == null)
                Debug.LogWarning("[Menu] Polices introuvables dans Resources/UI/Fonts (Titan One, Bowlby One) : police par défaut.");

            document = GetComponent<UIDocument>();
            if (document == null)
            {
                document = gameObject.AddComponent<UIDocument>();
                document.panelSettings = K.CreatePanel(100);
            }
            if (document.rootVisualElement == null)
            {
                Debug.LogWarning("[Menu] Pas de panneau UI Toolkit : ancien menu conservé.");
                PoolMatchRules.ExternalMenu = false;
                return;
            }

            Build();
            Show(title);
        }

        private void OnDisable() => PoolMatchRules.ExternalMenu = false;

        // ---------- Building ----------

        private void Build()
        {
            root = document.rootVisualElement;
            K.Fill(root);
            stage = K.Box(root, "stage");
            K.Fill(stage);

            foreach (string id in new[] { MenuLayouts.Title, MenuLayouts.NewGame, MenuLayouts.Load, MenuLayouts.Level,
                                          MenuLayouts.Mode, MenuLayouts.Multiplayer, MenuLayouts.Lobby, MenuLayouts.Settings })
                pages[id] = BuildPage(id);
            title = pages[MenuLayouts.Title];
            level = pages[MenuLayouts.Level];
            mode = pages[MenuLayouts.Mode];
            lobby = pages[MenuLayouts.Lobby];
            settingsPage = pages[MenuLayouts.Settings];

            BuildLevelCards(level);
            BuildRulesCard(mode);
            BuildLobbySlots(lobby);

            aimLine = K.Box(stage, "aim");
            aimLine.style.position = Position.Absolute;
            aimLine.style.height = 5;
            aimLine.style.backgroundColor = new Color(1f, 1f, 1f, 0.5f);
            aimLine.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));

            cueStick = K.Box(stage, "cue-stick");
            cueStick.style.position = Position.Absolute;
            cueStick.style.width = 620;
            cueStick.style.height = 16;
            cueStick.style.flexDirection = FlexDirection.Row;
            cueStick.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            K.Radius(cueStick, 8);
            cueStick.style.overflow = Overflow.Hidden;
            foreach (var (share, color) in new[] { (2f, K.Hex("#3a80c8")), (3f, K.Hex("#f4ecd6")), (55f, K.Hex("#c8914f")), (40f, K.Hex("#2b1a0f")) })
            {
                VisualElement part = K.Box(cueStick, "part");
                part.style.flexGrow = share;
                part.style.backgroundColor = color;
            }

            settings = new KeyBindingsPanel(stage);

            toast = K.Text(stage, "", K.TitleFont, 44, K.Chalk).parent;
            K.Place(toast, 50f, 46f);
            toast.style.opacity = 0f;
        }

        private Page BuildPage(string id)
        {
            var page = new Page { id = id, view = MenuRenderer.Build(stage, MenuLayouts.Get(id)) };
            page.view.Stop();
            page.Root.style.display = DisplayStyle.None;
            page.context = page.view.Item("context")?.text;
            page.aimHint = page.view.Item("aim-hint")?.visual;
            foreach (MenuNode item in page.view.Selectable)
            {
                MenuElement data = item.data;
                page.options.Add(new Option
                {
                    visual = item.visual, pos = data.position, radius = item.Radius, card = item.IsCard,
                    focusScale = data.focusScale, data = data, baseColor = data.color, shoot = () => Do(data),
                });
            }
            return page;
        }

        // One postcard per level of GameFlowSettings, in a row centered on
        // the "levels" zone of the Level screen.
        private void BuildLevelCards(Page page)
        {
            MenuNode zone = page.view.Item("levels");
            Vector2 center = zone != null ? zone.data.position : new Vector2(50f, 54f);
            float width = zone != null ? zone.data.size.x / 1920f * 100f : 78f;
            List<LevelEntry> levels = GameFlowSettings.Instance.levels;
            int n = Mathf.Max(1, levels.Count);
            float spacing = Mathf.Min(22f, width / n);
            for (int i = 0; i < levels.Count; i++)
            {
                LevelEntry entry = levels[i];
                int index = i;
                float tilt = i % 3 == 0 ? -3f : i % 3 == 1 ? 1f : 3f;
                AddLevelCard(page, center.x + (i - (n - 1) / 2f) * spacing, center.y, tilt, entry, index);
            }
        }

        private int FirstPlayableLevel()
        {
            List<LevelEntry> levels = GameFlowSettings.Instance.levels;
            for (int i = 0; i < levels.Count; i++) if (!levels[i].comingSoon) return i;
            return 0;
        }

        private void AddLevelCard(Page page, float x, float y, float tilt, LevelEntry entry, int index)
        {
            VisualElement holder = K.Box(page.Root, "level-card");
            holder.style.width = 400;
            holder.style.height = 470;
            K.Place(holder, x, y);
            VisualElement card = K.PopCard(holder, "card");
            K.Fill(card);
            K.Pad(card, 14, 14);
            card.style.rotate = new Rotate(new Angle(tilt, AngleUnit.Degree));
            VisualElement pic = K.Box(card, "picture");
            pic.style.height = 270;
            pic.style.overflow = Overflow.Hidden;
            K.Radius(pic, 10);
            K.Border(pic, 3, K.Ink);
            LevelPictures.Fill(pic, entry, index);
            bool locked = entry.comingSoon;
            if (locked)
            {
                VisualElement veil = K.Box(pic, "veil");
                K.Fill(veil);
                veil.style.backgroundColor = new Color(0f, 0f, 0f, 0.4f);
            }
            Label n = K.Text(card, entry.displayName, K.TitleFont, 40, K.Ink, false);
            n.style.unityTextAlign = TextAnchor.MiddleLeft;
            n.style.marginTop = 12;
            Label l = K.Text(card, entry.menuLine, K.BodyFont, 22, new Color(K.Ink.r, K.Ink.g, K.Ink.b, 0.65f), false);
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.whiteSpace = WhiteSpace.Normal;
            if (locked)
            {
                VisualElement stamp = K.Box(card, "stamp");
                stamp.style.position = Position.Absolute;
                stamp.style.right = -14;
                stamp.style.top = 36;
                stamp.style.backgroundColor = K.Red;
                K.Pad(stamp, 16, 4);
                K.Radius(stamp, 8);
                K.Border(stamp, 3, K.Ink);
                stamp.style.rotate = new Rotate(new Angle(10f, AngleUnit.Degree));
                K.Text(stamp, cfg.comingSoonStamp, K.TitleFont, 28, Color.white, false);
            }
            page.options.Add(new Option
            {
                visual = card, pos = new Vector2(x, y), radius = 200f, card = true, focusScale = 0.06f, baseColor = K.Card,
                shoot = locked ? (Action)(() => Toast(cfg.comingSoonToast))
                               : () => { levelName = TextFx.Strip(entry.displayName); levelIndex = index; Show(mode); },
            });
        }

        // The "rules" panel of the Mode screen: its title and line show the
        // aimed game type; an option line is added under them.
        private void BuildRulesCard(Page page)
        {
            MenuNode rules = page.view.Item("rules");
            if (rules == null) return;
            ruleTitle = rules.text;
            ruleText = rules.sub;
            if (ruleTitle != null) ruleTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            if (ruleText != null) ruleText.style.unityTextAlign = TextAnchor.MiddleLeft;
            ruleOption = K.Text(rules.visual, "", K.BodyFont, Mathf.Max(12f, rules.data.subTextSize * 0.87f), rules.data.textColor, false);
            ruleOption.style.marginTop = 14;
            ruleOption.style.alignSelf = Align.FlexStart;
            ruleOption.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
            K.Pad(ruleOption, 14, 4);
            K.Radius(ruleOption, 10);
        }

        private void BuildLobbySlots(Page page)
        {
            for (int i = 0; i < 2; i++)
            {
                slots[i] = page.view.Item("slot" + (i + 1))?.visual;
                slotLabels[i] = page.view.Item("slot" + (i + 1) + "-label")?.text;
                if (slots[i] != null)
                {
                    slots[i].style.alignItems = Align.Center;
                    slots[i].style.justifyContent = Justify.Center;
                }
            }
        }

        // ---------- Actions ----------

        private void Do(MenuElement e)
        {
            MenuFeel.Play(e.feelOnPress);
            switch (e.action)
            {
                case MenuAction.OpenScreen:
                    if (pages.TryGetValue(e.targetScreen ?? "", out Page target)) Show(target);
                    else Debug.LogWarning($"[Menu] Écran « {e.targetScreen} » introuvable (élément {e.id}).");
                    break;
                case MenuAction.Back: GoBack(); break;
                case MenuAction.NewGameSlot:
                    Toast(cfg.newGameToast);
                    local = false;
                    levelIndex = FirstPlayableLevel();
                    Later(1.3f, () => Launch(PoolGameMode.EightBall));
                    break;
                case MenuAction.LoadSlot: Toast(cfg.emptySlotToast); break;
                case MenuAction.JustForFun: local = false; Show(level); break;
                case MenuAction.PlayLocal: local = true; Show(lobby); break;
                case MenuAction.PlayOnline: Toast(cfg.onlineToast); break;
                case MenuAction.LobbyGo: Show(level); break;
                case MenuAction.StartMode: Launch(e.mode); break;
                case MenuAction.OpenSettings: Show(settingsPage); break;
                case MenuAction.Quit: Quit(); break;
            }
        }

        private void GoBack()
        {
            if (current == level) { Show(local ? lobby : title); return; }
            string back = current?.view.screen.backScreen;
            if (!string.IsNullOrEmpty(back) && pages.TryGetValue(back, out Page target)) Show(target);
        }

        // ---------- Pages ----------

        private void Show(Page page)
        {
            foreach (Page p in pages.Values)
            {
                bool on = p == page;
                p.Root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) p.view.Stop();
            }
            if (current == settingsPage && page != settingsPage) settings.Close();
            current = page;
            page.view.Replay();
            page.focused = 0;
            if (page == mode)
            {
                int classic = page.options.FindIndex(o => o.data != null && o.data.action == MenuAction.StartMode && o.data.mode == PoolGameMode.EightBall);
                page.focused = Mathf.Max(0, classic);
            }
            if (page == lobby) { slotDevices[0] = menuDevice ?? slotDevices[0]; knownPlayers = -1; }
            if (page == level)
            {
                // Start on the first level that can be played (the cards come after the screen's own options).
                int first = FirstPlayableLevel(), cardsStart = page.options.FindIndex(o => o.data == null);
                if (cardsStart >= 0 && cardsStart + first < page.options.Count) page.focused = cardsStart + first;
            }
            foreach (Option o in page.options) { o.focus = 0f; ResetVisual(o); }
            shooting = null;
            lastFocused = null;

            string context = local ? cfg.localContext : cfg.justForFunContext;
            if (level.context != null) TextFx.Set(level.context, context);
            if (mode.context != null) TextFx.Set(mode.context, context + " · " + levelName);
            if (page == settingsPage) settings.Open();
        }

        private void Hide()
        {
            root.style.display = DisplayStyle.None;
            foreach (Page p in pages.Values) p.view.Stop();
            current = null;
        }

        // The chosen level with this game type: loaded behind the loading
        // screen, with J1 (and J2 in local multiplayer). In a level played
        // straight from the editor, choosing that same level just starts the
        // match in place.
        private void Launch(PoolGameMode gameMode)
        {
            List<LevelEntry> levels = GameFlowSettings.Instance.levels;
            LevelEntry entry = levelIndex >= 0 && levelIndex < levels.Count ? levels[levelIndex] : null;
            PoolMatchRules matchRules = PoolMatchRules.Instance;
            if (entry == null || (matchRules != null && SceneManager.GetActiveScene().name == entry.sceneName))
            {
                if (matchRules != null) matchRules.RequestStart(gameMode, chosenParty, targetScore);
                Hide();
                return;
            }
            var devices = new List<InputDevice>();
            if (local) { foreach (InputDevice d in slotDevices) if (d != null) devices.Add(d); }
            else devices.Add(menuDevice ?? (InputDevice)Keyboard.current ?? Gamepad.current);
            LevelLoader.Load(entry, levelIndex, gameMode, chosenParty, targetScore, devices);
            Hide();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Toast(string text)
        {
            TextFx.Reveal(toast.Q<Label>(), text);
            toastTime = cfg.toastDuration;
        }

        // A text from MenuSettings with its {0}; a typo in it must not throw.
        private static string Format(string format, object value)
        {
            try { return string.Format(format ?? "", value); }
            catch (FormatException) { return format; }
        }

        private void Later(float delay, Action action)
        {
            pendingAction = action;
            pendingTime = delay;
        }

        // ---------- Frame ----------

        private void Update()
        {
            PoolMatchRules matchRules = PoolMatchRules.Instance;
            if (current == null || (matchRules != null && matchRules.MatchStarted) || LevelLoader.Busy)
            {
                if (current != null) Hide();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (toastTime > 0f) toastTime -= dt;
            toast.style.opacity = Mathf.Clamp01(toastTime * 3f);
            if (current.aimHint != null)
                current.aimHint.style.opacity = 0.55f + 0.45f * Mathf.Round(Mathf.Repeat(Time.unscaledTime / cfg.blinkPeriod, 1f));

            if (pendingAction != null)
            {
                pendingTime -= dt;
                if (pendingTime <= 0f) { Action a = pendingAction; pendingAction = null; a(); return; }
            }

            if (current == settingsPage)
            {
                aimLine.style.display = cueStick.style.display = DisplayStyle.None;
                if (!settings.Tick(dt)) GoBack();
                return;
            }

            if (current == lobby) UpdateSlots();
            if (current == mode) UpdateRules();

            // A J2 joining with its button doesn't also press "C'est parti".
            if (shooting == null && pendingAction == null && !joinedThisFrame) ReadInput();
            joinedThisFrame = false;
            FocusFeel();
            Animate(dt);
        }

        private void FocusFeel()
        {
            if (current.options.Count == 0) return;
            Option o = current.options[Mathf.Clamp(current.focused, 0, current.options.Count - 1)];
            if (o == lastFocused) return;
            if (lastFocused != null && o.data != null) MenuFeel.Play(o.data.feelOnFocus);
            lastFocused = o;
        }

        // J1 = the device driving the menu; J2 = the first other device that
        // presses its button (A / Start on a pad, Enter / Space on the
        // keyboard when J1 plays with a pad).
        private void UpdateSlots()
        {
            joinedThisFrame = false;
            if (slotDevices[0] == null) slotDevices[0] = menuDevice ?? (InputDevice)Keyboard.current ?? Gamepad.current;
            if (slotDevices[1] == null)
            {
                foreach (Gamepad pad in Gamepad.all)
                    if (pad != slotDevices[0] && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
                    { slotDevices[1] = pad; joinedThisFrame = true; break; }
                Keyboard kb = Keyboard.current;
                if (slotDevices[1] == null && kb != null && !(slotDevices[0] is Keyboard)
                    && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                { slotDevices[1] = kb; joinedThisFrame = true; }
            }

            int players = slotDevices[1] != null ? 2 : slotDevices[0] != null ? 1 : 0;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4.5f);
            for (int i = 0; i < 2; i++)
                if (slots[i] != null && i >= players) slots[i].style.scale = new Scale(new Vector3(pulse, pulse, 1f));
            if (players == knownPlayers) return;
            knownPlayers = players;
            if (joinedThisFrame) Toast(cfg.player2Joined);
            for (int i = 0; i < 2; i++)
            {
                if (slots[i] == null) continue;
                slots[i].Clear();
                slots[i].style.scale = new Scale(Vector3.one);
                float size = Mathf.Max(40f, slots[i].resolvedStyle.width > 1f ? slots[i].resolvedStyle.width : 170f);
                if (i < players)
                {
                    K.BallVisual(slots[i], size, i == 0 ? K.P1 : K.P2, "J" + (i + 1), false);
                    if (slotLabels[i] != null) TextFx.Reveal(slotLabels[i], Format(cfg.readyFormat, DeviceName(slotDevices[i])));
                }
                else
                {
                    VisualElement ring = K.Box(slots[i], "empty");
                    ring.style.width = ring.style.height = size;
                    K.Radius(ring, size / 2f);
                    K.Border(ring, 6, new Color(1f, 1f, 1f, 0.55f));
                    ring.style.justifyContent = Justify.Center;
                    Label call = K.Text(ring, Format(cfg.joinCallFormat, i + 1), K.BodyFont, size * 0.21f, K.Chalk, false);
                    call.style.unityTextAlign = TextAnchor.MiddleCenter;
                    if (slotLabels[i] != null) TextFx.Set(slotLabels[i], cfg.joinHint);
                }
            }
        }

        private string DeviceName(InputDevice device) => device is Gamepad ? cfg.gamepadName : cfg.keyboardName;

        private void UpdateRules()
        {
            if (current.options.Count == 0 || ruleTitle == null) return;
            Option focused = current.options[Mathf.Clamp(current.focused, 0, current.options.Count - 1)];
            MenuElement data = focused.data;
            if (data == null || data.action != MenuAction.StartMode) return;
            // The rules come in letter by letter when the aimed ball changes.
            if (TextFx.Strip(data.text) != ruleTitle.text) TextFx.Reveal(ruleTitle, data.text);
            if (ruleText != null && TextFx.Strip(data.description) != ruleText.text) TextFx.Reveal(ruleText, data.description);
            if (ruleOption != null)
                TextFx.Set(ruleOption, data.mode switch
                {
                    PoolGameMode.FourteenOne => Format(cfg.targetScoreFormat, targetScore),
                    PoolGameMode.Party => cfg.powersOption,
                    _ => cfg.noPowersOption,
                });
        }

        // ---------- Input ----------

        private void ReadInput()
        {
            Vector2 nav = Vector2.zero;
            bool confirm = false, back = false;
            int adjust = 0;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) nav = Vector2.down;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) nav = Vector2.up;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) nav = Vector2.left;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) nav = Vector2.right;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    confirm = true;
                    menuDevice = kb;
                }
                back |= kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) adjust = -1;
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame) adjust = 1;
            }

            foreach (Gamepad pad in Gamepad.all)
            {
                Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                Vector2 dir = stick.magnitude > 0.6f
                    ? (Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2(Mathf.Sign(stick.x), 0f) : new Vector2(0f, -Mathf.Sign(stick.y)))
                    : Vector2.zero;
                padLatch.TryGetValue(pad, out Vector2 held);
                if (dir != Vector2.zero && dir != held) nav = dir;
                padLatch[pad] = dir;
                if (pad.buttonSouth.wasPressedThisFrame)
                {
                    confirm = true;
                    menuDevice = pad;
                }
                back |= pad.buttonEast.wasPressedThisFrame;
                if (pad.leftShoulder.wasPressedThisFrame) adjust = -1;
                if (pad.rightShoulder.wasPressedThisFrame) adjust = 1;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && root.panel != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                Option hovered = null;
                foreach (Option o in current.options)
                    if (o.visual.worldBound.Contains(panelPos)) hovered = o;
                if (hovered != null && (screen - lastMouse).sqrMagnitude > 1f) current.focused = current.options.IndexOf(hovered);
                lastMouse = screen;
                if (mouse.leftButton.wasPressedThisFrame && hovered != null)
                {
                    current.focused = current.options.IndexOf(hovered);
                    confirm = true;
                    if (Keyboard.current != null) menuDevice = Keyboard.current;
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.1f) adjust = 1;
                else if (wheel < -0.1f) adjust = -1;
            }

            if (adjust != 0 && current == mode && current.options.Count > 0)
            {
                MenuElement data = current.options[current.focused].data;
                if (data != null && data.action == MenuAction.StartMode && data.mode == PoolGameMode.FourteenOne)
                    targetScore = Mathf.Clamp(targetScore + adjust * 5, 5, 200);
            }
            if (nav != Vector2.zero && current.options.Count > 1) Step(nav);
            if (back) GoBack();
            else if (confirm && current.options.Count > 0)
            {
                shooting = current.options[current.focused];
                shotTime = 0f;
            }
        }

        // To the option that best matches the pressed direction on screen
        // (y down), nearer ones winning over farther ones in line.
        private void Step(Vector2 dir)
        {
            Option from = current.options[current.focused];
            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < current.options.Count; i++)
            {
                if (i == current.focused) continue;
                Vector2 offset = current.options[i].pos - from.pos;
                float distance = offset.magnitude;
                if (distance < 0.01f) continue;
                float alignment = Vector2.Dot(offset / distance, dir);
                if (alignment < 0.3f) continue;
                float score = alignment / distance;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0) current.focused = best;
        }

        // ---------- Animation ----------

        private void Animate(float dt)
        {
            Vector2 size = current.Root.layout.size;
            for (int i = 0; i < current.options.Count; i++)
            {
                Option o = current.options[i];
                if (o == shooting && !o.card) continue;
                o.focus = Mathf.MoveTowards(o.focus, i == current.focused ? 1f : 0f, dt * 7f);
                float s = 1f + o.focusScale * Ease(o.focus);
                o.visual.style.scale = new Scale(new Vector3(s, s, 1f));
                o.visual.style.translate = new Translate(0, -14f * Ease(o.focus));
                o.visual.style.opacity = 1f;
                FocusLook(o);
            }

            // No cue on this screen: the choice applies at once.
            if (!current.HasCue || float.IsNaN(size.x) || size.x < 1f || current.options.Count == 0)
            {
                aimLine.style.display = cueStick.style.display = DisplayStyle.None;
                if (shooting != null)
                {
                    Option shot = shooting;
                    shooting = null;
                    shot.shoot?.Invoke();
                }
                return;
            }
            aimLine.style.display = cueStick.style.display = DisplayStyle.Flex;

            Option target = shooting ?? current.options[current.focused];
            Vector2 cue = Vector2.Scale(current.CuePos / 100f, size);
            Vector2 aimAt = Vector2.Scale(target.pos / 100f, size) + new Vector2(0f, -14f * Ease(target.focus));
            Vector2 dir = (aimAt - cue).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // Pull back, then thrust; then the ball flies off along the shot
            // (cards and pills don't fly: the choice applies after the thrust).
            float pull = 18f;
            if (shooting != null)
            {
                shotTime += dt * cfg.shotSpeed;
                if (shotTime < 0.16f) pull = Mathf.Lerp(18f, 70f, shotTime / 0.16f);
                else if (shotTime < 0.24f) pull = Mathf.Lerp(70f, -6f, (shotTime - 0.16f) / 0.08f);
                else if (shooting.card)
                {
                    Option shot = shooting;
                    shooting = null;
                    shot.shoot?.Invoke();
                    return;
                }
                else
                {
                    pull = -6f;
                    float k = Mathf.Clamp01((shotTime - 0.24f) / 0.32f);
                    Vector2 fly = dir * (420f * k);
                    shooting.visual.style.translate = new Translate(fly.x, fly.y - 14f);
                    float s = Mathf.Lerp(1f + shooting.focusScale, 0.35f, k);
                    shooting.visual.style.scale = new Scale(new Vector3(s, s, 1f));
                    shooting.visual.style.opacity = 1f - k;
                    if (k >= 1f)
                    {
                        Option shot = shooting;
                        shooting = null;
                        ResetVisual(shot);
                        shot.shoot?.Invoke();
                        return;
                    }
                }
            }

            Vector2 start = cue + dir * (current.CueRadius + 6f);
            float length = Mathf.Max(0f, Vector2.Distance(start, aimAt) - target.radius * (1f + target.focusScale));
            aimLine.style.left = start.x;
            aimLine.style.top = start.y - 2.5f;
            aimLine.style.width = length;
            aimLine.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            aimLine.style.opacity = shooting != null && shotTime > 0.24f ? 0f : 1f;

            Vector2 tip = cue - dir * (current.CueRadius + pull);
            cueStick.style.left = tip.x;
            cueStick.style.top = tip.y - 8f;
            cueStick.style.rotate = new Rotate(new Angle(angle + 180f, AngleUnit.Degree));
        }

        // Cards light up, pills turn yellow with an ink edge when aimed at.
        private static void FocusLook(Option o)
        {
            MenuElementKind kind = o.data != null ? o.data.kind : MenuElementKind.Card;
            if (kind == MenuElementKind.Card)
                o.visual.style.backgroundColor = Color.Lerp(o.baseColor, Color.white, o.focus);
            else if (kind == MenuElementKind.Pill)
            {
                bool on = o.focus > 0.5f;
                o.visual.style.backgroundColor = Color.Lerp(new Color(0f, 0f, 0f, 0.08f), K.Yellow, o.focus);
                K.Border(o.visual, on ? 3 : 0, K.Ink);
                if (on) o.visual.style.borderBottomWidth = 6;
            }
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t);

        private static void ResetVisual(Option o)
        {
            o.visual.style.translate = new Translate(0, 0);
            o.visual.style.scale = new Scale(Vector3.one);
            o.visual.style.opacity = 1f;
        }
    }
}
