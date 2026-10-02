using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // The pre-match menu of the validated UI direction (prototype
    // docs/ui/synthese.html): title, players, game mode — the pool table in
    // the bar at night, lit by its lamp; every choice is a ball that the cue
    // aims at and shoots. 2D for now (UI Toolkit, built in code); the 3D
    // version on a real table comes later. Replaces PoolMatchRules' OnGUI
    // mode select (PoolMatchRules.ExternalMenu) and starts the match through
    // PoolMatchRules.RequestStart. Created automatically in any scene that
    // has a PoolMatchRules. Mouse, keyboard or any gamepad.
    public class SyntheseMenu : MonoBehaviour
    {
        // ---------- Palette (prototype CSS) ----------
        private static readonly Color Felt = Hex("#1d7f4b"), FeltDark = Hex("#125c35"), Wood = Hex("#6b3a1e"), WoodDark = Hex("#3e200f");
        private static readonly Color Cream = Hex("#fff7e3"), ChalkShadow = Hex("#0d4527"), BallCream = Hex("#f6f1e4");
        private static readonly Color Cube = Hex("#3d8fe0"), CubeDark = Hex("#1f5c99"), P1 = Hex("#2f7cf6"), P2 = Hex("#ef4a3c"), Ink = Hex("#1c1510");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        // The UIDocument gets its panel settings while the object is still
        // inactive, so it attaches to the panel when it comes alive.
        private static void TryCreate()
        {
            if (FindFirstObjectByType<PoolMatchRules>() == null || FindFirstObjectByType<SyntheseMenu>() != null) return;
            var go = new GameObject("Menu (Synthèse)");
            go.SetActive(false);
            go.AddComponent<UIDocument>().panelSettings = CreatePanel();
            go.AddComponent<SyntheseMenu>();
            go.SetActive(true);
        }

        // Full screen over the game, scaled from 1920 × 1080.
        private static PanelSettings CreatePanel()
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/MenuTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 100;
            return panel;
        }

        // ---------- State ----------

        private class Option
        {
            public VisualElement visual;      // what grows when focused and flies when shot
            public Vector2 pos;               // percent of the screen, center
            public float radius;              // px, for the aim line
            public Action shoot;
            public float focus;               // 0..1, eased
        }

        private class Page
        {
            public VisualElement root;
            public readonly List<Option> options = new List<Option>();
            public int focused;
            public Page back;
            public Vector2 cuePos;            // percent
            public float cueRadius = 50f;
            public VisualElement cueBall;
        }

        private UIDocument document;
        private VisualElement root, stage, aimLine, cueStick, toast;
        private Page title, lobby, mode, current;
        private Font menuFont, titleFont;
        private Label blinkHint, ruleTitle, ruleText, ruleOption;
        private VisualElement[] slots = new VisualElement[2];
        private Label[] slotLabels = new Label[2];
        private int knownPlayers = -1;

        private PoolGameMode chosenMode = PoolGameMode.EightBall;
        private PoolPartyMode chosenParty = PoolPartyMode.Classic;
        private int targetScore = 25;
        private bool partyFromTitle;

        // Shot animation: pull back, thrust, the ball flies off, then the action.
        private Option shooting;
        private float shotTime;
        private float toastTime;
        private Vector2 lastMouse;
        private readonly Dictionary<Gamepad, Vector2> padLatch = new Dictionary<Gamepad, Vector2>();

        private void OnEnable()
        {
            PoolMatchRules.ExternalMenu = true;
            menuFont = Resources.Load<Font>("UI/Fonts/TitanOne-Regular");
            titleFont = Resources.Load<Font>("UI/Fonts/BowlbyOne-Regular");
            if (menuFont == null || titleFont == null)
                Debug.LogWarning("[Menu] Polices introuvables dans Resources/UI/Fonts (Titan One, Bowlby One) : police par défaut.");

            document = GetComponent<UIDocument>();
            if (document == null)
            {
                document = gameObject.AddComponent<UIDocument>();
                document.panelSettings = CreatePanel();
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
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0;

            stage = Box(root, "stage");
            Fill(stage);
            BuildRoom(stage);

            title = BuildTitle();
            lobby = BuildLobby();
            mode = BuildMode();
            lobby.back = title;
            mode.back = lobby;

            aimLine = Box(stage, "aim");
            aimLine.style.position = Position.Absolute;
            aimLine.style.height = 5;
            aimLine.style.backgroundColor = new Color(1f, 1f, 1f, 0.5f);
            aimLine.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            aimLine.pickingMode = PickingMode.Ignore;

            cueStick = Box(stage, "cue-stick");
            cueStick.style.position = Position.Absolute;
            cueStick.style.width = 620;
            cueStick.style.height = 16;
            cueStick.style.flexDirection = FlexDirection.Row;
            cueStick.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            cueStick.pickingMode = PickingMode.Ignore;
            Radius(cueStick, 8);
            cueStick.style.overflow = Overflow.Hidden;
            foreach (var (share, color) in new[] { (2f, Hex("#3a80c8")), (3f, Hex("#f4ecd6")), (55f, Hex("#c8914f")), (40f, Hex("#2b1a0f")) })
            {
                VisualElement part = Box(cueStick, "part");
                part.style.flexGrow = share;
                part.style.backgroundColor = color;
            }

            blinkHint = Text(stage, "vise une bille, tire !", menuFont, 34, Cream);
            Place(blinkHint.parent, 84f, 89f);
            Label keys = Text(stage, "Viser : souris · flèches · stick    Tirer : clic · Entrée · A    Retour : Échap · B", menuFont, 20, new Color(1f, 1f, 1f, 0.6f));
            Place(keys.parent, 50f, 97f);

            toast = Text(stage, "", titleFont, 44, Cream).parent;
            Place(toast, 50f, 46f);
            toast.style.opacity = 0f;
        }

        // The bar at night and the pool table lit by its lamp (every page).
        private void BuildRoom(VisualElement parent)
        {
            VisualElement room = Box(parent, "room");
            Fill(room);
            room.style.backgroundImage = Radial(Hex("#4d3121"), Hex("#140d09"), 0.5f, 0.4f, 1.1f);
            Stretch(room);

            // Blurred bar lights in the background.
            (float x, float y, float r, Color c)[] bokeh =
            {
                (7, 20, 60, new Color(1f, 0.67f, 0.31f, 0.55f)), (15, 34, 42, new Color(1f, 0.47f, 0.35f, 0.45f)),
                (24, 16, 48, new Color(0.47f, 0.78f, 1f, 0.35f)), (79, 18, 66, new Color(1f, 0.78f, 0.47f, 0.5f)),
                (88, 32, 48, new Color(1f, 0.35f, 0.55f, 0.4f)), (94, 12, 42, new Color(0.55f, 1f, 0.78f, 0.3f)),
                (70, 8, 36, new Color(1f, 0.9f, 0.63f, 0.35f)), (33, 6, 36, new Color(1f, 0.59f, 0.35f, 0.35f)),
            };
            foreach (var (x, y, r, c) in bokeh)
            {
                VisualElement glow = Disc(parent, x, y, r * 2.4f, Color.clear);
                glow.style.backgroundImage = Radial(c, new Color(c.r, c.g, c.b, 0f), 0.5f, 0.5f, 0.8f);
                Stretch(glow);
            }

            VisualElement tableShadow = Box(parent, "table-shadow");
            Rect(tableShadow, 11f, 18f, 11f, 1f);
            tableShadow.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            Radius(tableShadow, 40);

            VisualElement felt = Box(parent, "felt");
            Rect(felt, 10f, 14f, 10f, 5f);
            felt.style.backgroundImage = Radial(Felt, FeltDark, 0.5f, 0.6f, 1.2f);
            Stretch(felt);
            Radius(felt, 22);
            Border(felt, 30, Wood);

            foreach (var (x, y) in new[] { (12.6f, 19f), (50f, 17f), (87.4f, 19f), (12.6f, 90.5f), (50f, 92.5f), (87.4f, 90.5f) })
            {
                VisualElement pocket = Disc(parent, x, y, 66, Color.black);
                Border(pocket, 6, WoodDark);
            }

            VisualElement lampLight = Box(parent, "lamplight");
            Fill(lampLight);
            lampLight.pickingMode = PickingMode.Ignore;
            lampLight.style.backgroundImage = Radial(new Color(1f, 0.89f, 0.63f, 0.2f), new Color(1f, 0.89f, 0.63f, 0f), 0.5f, 0.52f, 0.9f);
            Stretch(lampLight);

            VisualElement shade = Box(parent, "lampshade");
            shade.style.position = Position.Absolute;
            shade.style.left = Length.Percent(33);
            shade.style.width = Length.Percent(34);
            shade.style.top = 0;
            shade.style.height = Length.Percent(7.5f);
            shade.style.backgroundColor = Hex("#174a31");
            shade.style.borderBottomLeftRadius = shade.style.borderBottomRightRadius = 10;
            shade.style.borderBottomWidth = 8;
            shade.style.borderBottomColor = Hex("#f7d58a");

            VisualElement vignette = Box(parent, "vignette");
            Fill(vignette);
            vignette.pickingMode = PickingMode.Ignore;
            vignette.style.backgroundImage = Radial(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.55f), 0.5f, 0.45f, 2.2f);
            Stretch(vignette);
        }

        private Page NewPage(string name, Vector2 cue, float cueSize)
        {
            var page = new Page { root = Box(stage, name), cuePos = cue, cueRadius = cueSize / 2f };
            Fill(page.root);
            page.root.pickingMode = PickingMode.Ignore;
            page.cueBall = Ball(page.root, cue.x, cue.y, cueSize, BallCream, null, false).Q("ball");
            return page;
        }

        private Page BuildTitle()
        {
            Page page = NewPage("title", new Vector2(50f, 85f), 96f);
            VisualElement logo = Box(page.root, "logo");
            logo.style.alignItems = Align.Center;
            Place(logo, 50f, 28f);
            Shadowed(Text(logo, "Untitled", menuFont, 60, Cream, false));
            Shadowed(Text(logo, "POOL GAME", titleFont, 116, Cream, false), 8);

            AddBall(page, 26f, 58f, Hex("#f4c20d"), "1", false, "Jouer", () => { partyFromTitle = false; Show(lobby); });
            AddBall(page, 42f, 58f, Hex("#6a2c91"), "12", true, "Party", () => { partyFromTitle = true; Show(lobby); });
            AddBall(page, 58f, 58f, Hex("#138a3a"), "6", false, "Réglages", () => Toast("Bientôt !"));
            AddBall(page, 74f, 58f, Hex("#111111"), "8", false, "Quitter", Quit);
            return page;
        }

        private Page BuildLobby()
        {
            Page page = NewPage("lobby", new Vector2(17f, 82f), 70f);
            Shadowed(Text(page.root, "Qui joue ce soir ?", menuFont, 84, Cream));
            Place(page.root[page.root.childCount - 1], 50f, 24f);

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 32f : 68f;
                slots[i] = Box(page.root, "slot" + i);
                Place(slots[i], x, 48f);
                slotLabels[i] = Text(page.root, "", menuFont, 40, Cream);
                Shadowed(slotLabels[i]);
                Place(slotLabels[i].parent, x, 66f);
            }

            // "C'est parti": a pill with a small ball, like the prototype's button.
            VisualElement go = Box(page.root, "go");
            Place(go, 50f, 84f);
            VisualElement pill = Box(go, "pill");
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.paddingLeft = pill.style.paddingTop = pill.style.paddingBottom = 12;
            pill.style.paddingRight = 34;
            pill.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            Radius(pill, 60);
            VisualElement small = BallVisual(pill, 64, Hex("#f07b12"), "5", false);
            small.style.marginRight = 16;
            Shadowed(Text(pill, "C'est parti", titleFont, 46, Cream, false));
            page.options.Add(new Option { visual = pill, pos = new Vector2(50f, 84f), radius = 150f, shoot = () => Show(mode) });
            return page;
        }

        private Page BuildMode()
        {
            Page page = NewPage("mode", new Vector2(50f, 84f), 70f);
            Shadowed(Text(page.root, "On joue à quoi ?", menuFont, 74, Cream));
            Place(page.root[page.root.childCount - 1], 27f, 24f);

            AddModeBall(page, 18f, 42f, Hex("#111111"), "8", false, "8-ball", PoolGameMode.EightBall,
                "Pleines contre rayées. La 8 en dernier, dans la poche annoncée.");
            AddModeBall(page, 34f, 42f, Hex("#f4c20d"), "9", true, "9-ball", PoolGameMode.NineBall,
                "Touche toujours la plus petite bille. Qui rentre la 9 gagne.");
            AddModeBall(page, 18f, 70f, Hex("#d8261c"), "3", false, "14.1", PoolGameMode.FourteenOne,
                "Chaque bille rentrée rapporte 1 point.");
            AddModeBall(page, 34f, 70f, Hex("#6a2c91"), "12", true, "Party", PoolGameMode.Party,
                "Les règles du 8-ball, plus des caisses et une bille à pouvoir sur la table.");

            // The rules card ("chalk cube").
            VisualElement card = Box(page.root, "rules");
            card.style.position = Position.Absolute;
            card.style.left = Length.Percent(51);
            card.style.top = Length.Percent(33);
            card.style.width = Length.Percent(33);
            card.style.backgroundColor = Cube;
            card.style.paddingLeft = card.style.paddingRight = 32;
            card.style.paddingTop = card.style.paddingBottom = 26;
            card.style.borderBottomWidth = 12;
            card.style.borderBottomColor = CubeDark;
            card.style.rotate = new Rotate(new Angle(2f, AngleUnit.Degree));
            Radius(card, 14);
            ruleTitle = Text(card, "", titleFont, 54, Color.white, false);
            ruleText = Text(card, "", menuFont, 30, Color.white, false);
            ruleText.style.whiteSpace = WhiteSpace.Normal;
            ruleText.style.marginTop = 8;
            ruleOption = Text(card, "", menuFont, 26, Color.white, false);
            ruleOption.style.marginTop = 14;
            ruleOption.style.alignSelf = Align.FlexStart;
            ruleOption.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
            ruleOption.style.paddingLeft = ruleOption.style.paddingRight = 14;
            ruleOption.style.paddingTop = ruleOption.style.paddingBottom = 4;
            Radius(ruleOption, 10);
            return page;
        }

        private readonly List<(Option option, PoolGameMode mode, string name, string rules)> modeOptions =
            new List<(Option, PoolGameMode, string, string)>();

        private void AddModeBall(Page page, float x, float y, Color color, string number, bool stripe, string label,
            PoolGameMode gameMode, string rules)
        {
            Option option = AddBall(page, x, y, color, number, stripe, label, () =>
            {
                chosenMode = gameMode;
                PoolMatchRules match = PoolMatchRules.Instance;
                if (match != null) match.RequestStart(gameMode, chosenParty, targetScore);
                Hide();
            });
            modeOptions.Add((option, gameMode, label, rules));
        }

        private Option AddBall(Page page, float x, float y, Color color, string number, bool stripe, string label, Action shoot)
        {
            const float size = 110f;
            VisualElement holder = Ball(page.root, x, y, size, color, number, stripe);
            Label text = Text(page.root, label, menuFont, 44, Cream);
            Shadowed(text);
            Place(text.parent, x, y + 9f);
            var option = new Option { visual = holder.Q("ball"), pos = new Vector2(x, y), radius = size / 2f, shoot = shoot };
            page.options.Add(option);
            return option;
        }

        // ---------- Pages ----------

        private void Show(Page page)
        {
            foreach (Page p in new[] { title, lobby, mode })
                p.root.style.display = p == page ? DisplayStyle.Flex : DisplayStyle.None;
            current = page;
            if (page == mode)
            {
                PoolGameMode wanted = partyFromTitle ? PoolGameMode.Party : chosenMode;
                int index = modeOptions.FindIndex(m => m.mode == wanted);
                page.focused = Mathf.Max(0, index);
            }
            foreach (Option o in page.options) { o.focus = 0f; ResetVisual(o); }
            shooting = null;
        }

        private void Hide()
        {
            root.style.display = DisplayStyle.None;
            current = null;
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
            toast.Q<Label>().text = text;
            toastTime = 1.4f;
        }

        // ---------- Frame ----------

        private void Update()
        {
            PoolMatchRules matchRules = PoolMatchRules.Instance;
            if (current == null || (matchRules != null && matchRules.MatchStarted))
            {
                if (current != null) Hide();
                return;
            }

            if (toastTime > 0f) toastTime -= Time.unscaledDeltaTime;
            toast.style.opacity = Mathf.Clamp01(toastTime * 3f);
            blinkHint.style.opacity = 0.55f + 0.45f * Mathf.Round(Mathf.Repeat(Time.unscaledTime / 0.7f, 1f));

            if (current == lobby) UpdateSlots();
            if (current == mode) UpdateRules();

            if (shooting == null) ReadInput();
            Animate();
        }

        private void UpdateSlots()
        {
            int players = PlayerInput.all.Count;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4.5f);
            for (int i = 0; i < 2; i++)
                if (i >= players) slots[i].style.scale = new Scale(new Vector3(pulse, pulse, 1f));
            if (players == knownPlayers) return;
            knownPlayers = players;
            for (int i = 0; i < 2; i++)
            {
                slots[i].Clear();
                slots[i].style.scale = new Scale(Vector3.one);
                if (i < players)
                {
                    BallVisual(slots[i], 170, i == 0 ? P1 : P2, "J" + (i + 1), false);
                    slotLabels[i].text = DeviceName(PlayerInput.all[i]) + " - prêt";
                }
                else
                {
                    VisualElement ring = Box(slots[i], "empty");
                    ring.style.width = ring.style.height = 170;
                    Radius(ring, 85);
                    Border(ring, 6, new Color(1f, 1f, 1f, 0.55f));
                    ring.style.justifyContent = Justify.Center;
                    Label call = Text(ring, $"J{i + 1}\nappuie !", menuFont, 36, Cream, false);
                    call.style.unityTextAlign = TextAnchor.MiddleCenter;
                    slotLabels[i].text = "Une touche ou un bouton\npour rejoindre";
                }
            }
        }

        private static string DeviceName(PlayerInput player)
        {
            foreach (InputDevice device in player.devices)
                if (device is Gamepad) return "Manette";
            return "Clavier & souris";
        }

        private void UpdateRules()
        {
            if (current.options.Count == 0) return;
            var entry = modeOptions[Mathf.Clamp(current.focused, 0, modeOptions.Count - 1)];
            ruleTitle.text = entry.name;
            ruleText.text = entry.rules;
            ruleOption.text = entry.mode switch
            {
                PoolGameMode.FourteenOne => $"Premier à {targetScore}   < >  (LB / RB, molette, - / +)",
                PoolGameMode.Party => "Pouvoirs : Attaque · Défense · Effet",
                _ => "Classique",
            };
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
                confirm |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
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
                confirm |= pad.buttonSouth.wasPressedThisFrame;
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
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.1f) adjust = 1;
                else if (wheel < -0.1f) adjust = -1;
            }

            if (adjust != 0 && current == mode && modeOptions[current.focused].mode == PoolGameMode.FourteenOne)
                targetScore = Mathf.Clamp(targetScore + adjust * 5, 5, 200);
            if (nav != Vector2.zero) Step(nav);
            if (back && current.back != null) Show(current.back);
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

        private void Animate()
        {
            Vector2 size = current.root.layout.size;
            if (float.IsNaN(size.x) || size.x < 1f || current.options.Count == 0)
            {
                aimLine.style.display = cueStick.style.display = DisplayStyle.None;
                return;
            }
            aimLine.style.display = cueStick.style.display = DisplayStyle.Flex;

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < current.options.Count; i++)
            {
                Option o = current.options[i];
                if (o == shooting) continue;
                o.focus = Mathf.MoveTowards(o.focus, i == current.focused ? 1f : 0f, dt * 7f);
                float s = 1f + 0.18f * Ease(o.focus);
                o.visual.style.scale = new Scale(new Vector3(s, s, 1f));
                o.visual.style.translate = new Translate(0, -14f * Ease(o.focus));
                o.visual.style.opacity = 1f;
            }

            Option target = shooting ?? current.options[current.focused];
            Vector2 cue = Vector2.Scale(current.cuePos / 100f, size);
            Vector2 aimAt = Vector2.Scale(target.pos / 100f, size) + new Vector2(0f, -14f * Ease(target.focus));
            Vector2 dir = (aimAt - cue).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // Pull back, then thrust; then the ball flies off along the shot.
            float pull = 18f;
            if (shooting != null)
            {
                shotTime += dt;
                if (shotTime < 0.16f) pull = Mathf.Lerp(18f, 70f, shotTime / 0.16f);
                else if (shotTime < 0.24f) pull = Mathf.Lerp(70f, -6f, (shotTime - 0.16f) / 0.08f);
                else
                {
                    pull = -6f;
                    float k = Mathf.Clamp01((shotTime - 0.24f) / 0.32f);
                    Vector2 fly = dir * (420f * k);
                    shooting.visual.style.translate = new Translate(fly.x, fly.y - 14f);
                    float s = Mathf.Lerp(1.18f, 0.35f, k);
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

            Vector2 start = cue + dir * (current.cueRadius + 6f);
            float length = Mathf.Max(0f, Vector2.Distance(start, aimAt) - target.radius * 1.18f);
            aimLine.style.left = start.x;
            aimLine.style.top = start.y - 2.5f;
            aimLine.style.width = length;
            aimLine.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            aimLine.style.opacity = shooting != null && shotTime > 0.24f ? 0f : 1f;

            Vector2 tip = cue - dir * (current.cueRadius + pull);
            cueStick.style.left = tip.x;
            cueStick.style.top = tip.y - 8f;
            cueStick.style.rotate = new Rotate(new Angle(angle + 180f, AngleUnit.Degree));
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t);

        private static void ResetVisual(Option o)
        {
            o.visual.style.translate = new Translate(0, 0);
            o.visual.style.scale = new Scale(Vector3.one);
            o.visual.style.opacity = 1f;
        }

        // ---------- Element helpers ----------

        private static VisualElement Box(VisualElement parent, string name)
        {
            var e = new VisualElement { name = name };
            parent.Add(e);
            return e;
        }

        private static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0;
        }

        private static void Rect(VisualElement e, float left, float top, float right, float bottom)
        {
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(left);
            e.style.top = Length.Percent(top);
            e.style.right = Length.Percent(right);
            e.style.bottom = Length.Percent(bottom);
        }

        // Centered on (x %, y %) of the parent.
        private static void Place(VisualElement e, float x, float y)
        {
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(x);
            e.style.top = Length.Percent(y);
            e.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        }

        private static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        private static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = color;
        }

        private static void Stretch(VisualElement e) =>
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));

        private static VisualElement Disc(VisualElement parent, float x, float y, float size, Color color)
        {
            VisualElement d = Box(parent, "disc");
            d.style.width = d.style.height = size;
            Radius(d, size / 2f);
            d.style.backgroundColor = color;
            d.pickingMode = PickingMode.Ignore;
            Place(d, x, y);
            return d;
        }

        // A label; centered = wrapped in a holder to be placed with Place().
        private Label Text(VisualElement parent, string text, Font font, float size, Color color, bool centered = true)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            if (font != null) label.style.unityFontDefinition = FontDefinition.FromFont(font);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.pickingMode = PickingMode.Ignore;
            if (!centered)
            {
                parent.Add(label);
                return label;
            }
            VisualElement holder = Box(parent, "text");
            holder.pickingMode = PickingMode.Ignore;
            holder.Add(label);
            return label;
        }

        private static Label Shadowed(Label label, float drop = 5f)
        {
            label.style.textShadow = new TextShadow { offset = new Vector2(0f, drop), blurRadius = 0f, color = ChalkShadow };
            return label;
        }

        // A pool ball centered on (x %, y %): a holder with the ball (named
        // "ball") inside, which is what scales and flies.
        private VisualElement Ball(VisualElement parent, float x, float y, float size, Color color, string number, bool stripe)
        {
            VisualElement holder = Box(parent, "ball-holder");
            holder.style.width = holder.style.height = size;
            holder.pickingMode = PickingMode.Ignore;
            Place(holder, x, y);
            VisualElement shadow = Disc(holder, 50f, 62f, size * 0.9f, new Color(0f, 0f, 0f, 0.35f));
            shadow.name = "shadow";
            BallVisual(holder, size, color, number, stripe).name = "ball";
            return holder;
        }

        private VisualElement BallVisual(VisualElement parent, float size, Color color, string number, bool stripe)
        {
            VisualElement ball = Box(parent, "ball");
            ball.style.width = ball.style.height = size;
            ball.style.flexShrink = 0;
            Radius(ball, size / 2f);
            ball.style.overflow = Overflow.Hidden;
            ball.style.backgroundColor = stripe ? BallCream : color;
            if (stripe)
            {
                VisualElement band = Box(ball, "band");
                band.style.position = Position.Absolute;
                band.style.left = band.style.right = 0;
                band.style.top = Length.Percent(26);
                band.style.bottom = Length.Percent(26);
                band.style.backgroundColor = color;
            }
            VisualElement shade = Box(ball, "shade");
            Fill(shade);
            shade.style.backgroundImage = Radial(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.38f), 0.36f, 0.68f, 1.6f);
            Stretch(shade);
            if (!string.IsNullOrEmpty(number))
            {
                VisualElement disc = Disc(ball, 50f, 50f, size * 0.44f, BallCream);
                disc.style.justifyContent = Justify.Center;
                Label n = Text(disc, number, menuFont, size * (number.Length > 1 ? 0.19f : 0.23f), Ink, false);
                n.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            Disc(ball, 31f, 25f, size * 0.15f, new Color(1f, 1f, 1f, 0.9f));
            return ball;
        }

        // ---------- Textures ----------

        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        // A radial gradient (CSS radial-gradient stand-in): inner at (cx, cy)
        // (texture space, y up), outer at the corners; power shapes the falloff.
        private static StyleBackground Radial(Color inner, Color outer, float cx, float cy, float power)
        {
            string key = $"{inner}{outer}{cx}{cy}{power}";
            if (!textures.TryGetValue(key, out Texture2D tex) || tex == null)
            {
                const int n = 128;
                tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2((x + 0.5f) / n, (y + 0.5f) / n), new Vector2(cx, cy)) / 0.7071f;
                        pixels[y * n + x] = Color.Lerp(inner, outer, Mathf.Pow(Mathf.Clamp01(d), power));
                    }
                tex.SetPixels(pixels);
                tex.Apply();
                textures[key] = tex;
            }
            return new StyleBackground(tex);
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
