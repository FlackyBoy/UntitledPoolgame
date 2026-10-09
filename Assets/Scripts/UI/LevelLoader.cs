using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.Core
{
    // From the main menu to a level (prototype docs/ui/chargement.html,
    // variant B « Le retour des billes »): the level's scene loads in the
    // background (LoadSceneAsync) behind a loading screen — the level's
    // postcard with its twist, balls dropping into the return gutter as it
    // loads, silly status lines and a tip. Once the scene is up, its players
    // are joined with the devices chosen in the menu (GameSession), each one
    // presses A / Enter to say they're ready, and the match starts with the
    // game type chosen in the menu (PoolMatchRules.RequestStart).
    // Texts, tips and timings: LoadingScreenSettings; level name, picture
    // and twist: GameFlowSettings. Kept across the scene change
    // (DontDestroyOnLoad), gone once the match has started.
    public class LevelLoader : MonoBehaviour
    {
        private static LevelLoader instance;
        public static bool Busy => instance != null;

        public static void Load(LevelEntry level, int levelIndex, PoolGameMode mode, PoolPartyMode party, int targetScore, IEnumerable<InputDevice> devices)
        {
            if (instance != null || level == null) return;
            GameSession.Begin(level, mode, party, targetScore, devices);
            var go = new GameObject("Loading screen");
            DontDestroyOnLoad(go);
            go.SetActive(false);
            go.AddComponent<UIDocument>().panelSettings = K.CreatePanel(120);
            instance = go.AddComponent<LevelLoader>();
            instance.levelIndex = levelIndex;
            go.SetActive(true);
        }

        private static readonly Color[] BallColors =
        {
            K.Hex("#f4c20d"), K.Hex("#1f4fd1"), K.Hex("#d8261c"), K.Hex("#6a2c91"), K.Hex("#f07b12"), K.Hex("#138a3a"), K.Hex("#7a1f1f"), K.Hex("#111111"),
        };
        private const int BallCount = 15;

        private class Gutter
        {
            public VisualElement element;
            public float x, target;            // percent of the gutter
        }

        private LoadingScreenSettings cfg;
        private int levelIndex;
        private VisualElement root, gutter, readyRow, goHolder, tipCard;
        private Label status, tipText, goLabel;
        private readonly List<Gutter> balls = new List<Gutter>();
        private readonly List<(InputDevice device, VisualElement pill, Label label, bool ready)> players =
            new List<(InputDevice, VisualElement, Label, bool)>();
        private float tipTimer, statusTimer, goTime = -1f, startedAt;
        private int tipIndex, statusIndex;
        private bool loaded;

        private void OnEnable()
        {
            cfg = LoadingScreenSettings.Instance;
            UIDocument document = GetComponent<UIDocument>();
            if (document != null && document.rootVisualElement != null) Build(document.rootVisualElement);
        }

        private void Start() => StartCoroutine(Run());

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---------- Building ----------

        // The "Loading" screen of Menu Studio gives the place, size and look of
        // each part (elements found by id: context, postcard, tip, status,
        // ready, gutter, go) and any decoration the designer added; this
        // fills the parts.
        private void Build(VisualElement r)
        {
            root = r;
            K.Fill(root);
            MenuView view = MenuRenderer.Build(root, MenuLayouts.Get(MenuLayouts.Loading));

            LevelEntry level = GameSession.Level;
            int count = Mathf.Max(1, GameSession.Devices.Count);

            // Context chip: game type and number of players.
            MenuNode chip = view.Item("context");
            if (chip?.text != null)
                TextFx.Set(chip.text, Format(cfg.contextFormat, GameSession.ModeName(GameSession.Mode),
                    count == 1 ? cfg.onePlayerText : Format(cfg.playersFormat, count)));

            // The level's postcard.
            VisualElement card = K.PopCard(Zone(view, "postcard", root), "postcard");
            card.style.alignSelf = Align.Stretch;
            K.Pad(card, 16, 16);
            VisualElement pic = K.Box(card, "picture");
            pic.style.height = 310;
            pic.style.overflow = Overflow.Hidden;
            K.Radius(pic, 10);
            K.Border(pic, 3, K.Ink);
            LevelPictures.Fill(pic, level, levelIndex);
            Label name = K.Text(card, level != null ? level.displayName : "", K.TitleFont, 70, K.Ink, false);
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.marginTop = 10;
            Label twist = K.Text(card, level != null ? level.twist : "", K.BodyFont, 26, new Color(K.Ink.r, K.Ink.g, K.Ink.b, 0.75f), false);
            twist.style.unityTextAlign = TextAnchor.MiddleLeft;
            twist.style.whiteSpace = WhiteSpace.Normal;
            if (level != null) TextFx.Type(twist, level.twist);   // written on the postcard while it loads
            VisualElement stamp = K.Box(card, "stamp");
            stamp.style.position = Position.Absolute;
            stamp.style.right = -30;
            stamp.style.top = 30;
            K.Pad(stamp, 16, 4);
            K.Radius(stamp, 8);
            K.Border(stamp, 4, K.Red);
            stamp.style.backgroundColor = new Color(K.Card.r, K.Card.g, K.Card.b, 0.9f);
            stamp.style.rotate = new Rotate(new Angle(12f, AngleUnit.Degree));
            K.Text(stamp, cfg.stampText, K.TitleFont, 40, K.Red, false);

            // Tip card.
            MenuElement tipData = view.Item("tip")?.data;
            tipCard = K.Box(Zone(view, "tip", root), "tip");
            tipCard.style.alignSelf = Align.Stretch;
            tipCard.style.backgroundColor = tipData != null ? tipData.color : K.Cube;
            K.Pad(tipCard, 24, 18);
            K.Radius(tipCard, 14);
            tipCard.style.borderBottomWidth = 12;
            tipCard.style.borderBottomColor = K.CubeDark;
            Label tipTitle = K.Text(tipCard, cfg.tipTitle, K.TitleFont, 30, Color.white, false);
            tipTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            tipText = K.Text(tipCard, "", K.BodyFont, 24, Color.white, false);
            tipText.style.unityTextAlign = TextAnchor.UpperLeft;
            tipText.style.whiteSpace = WhiteSpace.Normal;
            tipIndex = cfg.tips != null && cfg.tips.Length > 0 ? Random.Range(0, cfg.tips.Length) : 0;
            NextTip();

            // Status line, then the players' "ready" pills in its place.
            MenuNode statusItem = view.Item("status");
            if (statusItem?.text != null) status = statusItem.text;
            else
            {
                status = K.Shadowed(K.Text(root, "", K.BodyFont, 46, K.Chalk));
                K.Place(status.parent, 50f, 69f);
            }
            NextStatus();

            readyRow = K.Box(Zone(view, "ready", root), "ready");
            readyRow.style.flexDirection = FlexDirection.Row;
            readyRow.style.justifyContent = Justify.Center;
            readyRow.style.display = DisplayStyle.None;

            // Return gutter (the progress).
            MenuElement gutterData = view.Item("gutter")?.data;
            gutter = K.Box(Zone(view, "gutter", root), "gutter");
            gutter.style.alignSelf = Align.Stretch;
            gutter.style.flexGrow = 1;
            gutter.style.backgroundColor = gutterData != null ? gutterData.color : K.Hex("#4a2a16");
            K.Radius(gutter, 48);
            gutter.style.borderTopWidth = 10;
            gutter.style.borderTopColor = K.Hex("#2b1a0f");
            gutter.style.borderBottomWidth = 6;
            gutter.style.borderBottomColor = K.Hex("#1a0e07");

            MenuNode goItem = view.Item("go");
            if (goItem != null)
            {
                goHolder = goItem.holder;
                goLabel = goItem.text ?? K.Text(goItem.visual, "", K.TitleFont, 150, K.Yellow, false);
            }
            else
            {
                goHolder = K.Box(root, "go");
                K.Place(goHolder, 50f, 40f);
                goLabel = K.Text(goHolder, "", K.TitleFont, 150, K.Yellow, false);
            }
            TextFx.Set(goLabel, cfg.goText);
            goLabel.style.unityTextOutlineWidth = 4f;
            goLabel.style.unityTextOutlineColor = K.Ink;
            goLabel.style.textShadow = new TextShadow { offset = new Vector2(8f, 10f), blurRadius = 0f, color = K.Ink };
            goHolder.style.opacity = 0f;
        }

        // The inside of a zone of the Loading screen (its slot element),
        // or a plain box on the screen when the designer removed it.
        private static VisualElement Zone(MenuView view, string id, VisualElement fallback)
        {
            MenuNode item = view.Item(id);
            if (item == null) return K.Box(fallback, id + "-zone");
            item.visual.style.flexDirection = FlexDirection.Column;
            item.visual.style.alignItems = Align.Stretch;
            item.visual.style.justifyContent = Justify.Center;
            return item.visual;
        }

        private static string Format(string format, params object[] values)
        {
            if (string.IsNullOrEmpty(format)) return "";
            try { return string.Format(format, values); } catch (System.FormatException) { return format; }
        }

        private void NextTip()
        {
            if (tipText == null) return;
            bool any = cfg.tips != null && cfg.tips.Length > 0;
            tipCard.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (any) TextFx.Type(tipText, cfg.tips[tipIndex++ % cfg.tips.Length]);
            tipTimer = Mathf.Max(0.5f, cfg.tipInterval);
        }

        private void NextStatus()
        {
            if (status == null || loaded) return;
            TextFx.Reveal(status, cfg.statusMessages != null && cfg.statusMessages.Length > 0 ? cfg.statusMessages[statusIndex++ % cfg.statusMessages.Length] : "");
            statusTimer = Mathf.Max(0.3f, cfg.statusInterval);
        }

        // Balls already in the gutter for this progress (0..1).
        private void ShowProgress(float progress)
        {
            if (gutter == null) return;
            int want = Mathf.Clamp(Mathf.FloorToInt(progress * BallCount), 0, BallCount);
            while (balls.Count < want)
            {
                int i = balls.Count;
                VisualElement holder = K.Box(gutter, "ball");
                holder.style.position = Position.Absolute;
                holder.style.top = Length.Percent(50);
                holder.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                K.BallVisual(holder, 62, BallColors[i % 8], (i + 1).ToString(), i >= 8);
                balls.Add(new Gutter { element = holder, x = 104f, target = 4f + i * 6.6f });
                Debug.Log($"[Loading] ball {i + 1}/{BallCount} at {Time.realtimeSinceStartup - startedAt:0.00}s (shown {progress:0.00}, frame {Time.unscaledDeltaTime * 1000f:0}ms)");
            }
        }

        // ---------- Flow ----------

        private IEnumerator Run()
        {
            // Let the screen draw before the load starts: in the editor the
            // load can block the main thread for a while.
            yield return null;
            yield return null;

            LevelEntry level = GameSession.Level;
            startedAt = Time.realtimeSinceStartup;
            Debug.Log($"[Loading] loading scene '{level?.sceneName}' from '{SceneManager.GetActiveScene().name}', devices: {string.Join(", ", GameSession.Devices)}, minimum duration {cfg.minimumDuration}s");
            AsyncOperation op = string.IsNullOrEmpty(level?.sceneName) ? null : SceneManager.LoadSceneAsync(level.sceneName);
            if (op == null)
            {
                Debug.LogError($"[Loading] Impossible de charger la scène « {level?.sceneName} » : vérifie qu'elle est dans la liste des scènes du build (GameFlowSettings).");
                GameSession.Clear();
                Destroy(gameObject);
                yield break;
            }
            op.allowSceneActivation = false;

            // Shown progress: follows the real one but fills at a steady
            // pace — at most one full gutter per Minimum Duration, and with a
            // capped frame time, so a load that freezes the editor for a
            // second doesn't drop all the balls at once afterwards.
            float shown = 0f, fillRate = 1f / Mathf.Max(0.3f, cfg.minimumDuration);
            while (true)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                float real = Mathf.Clamp01(op.progress / 0.9f);
                shown = Mathf.MoveTowards(shown, real, dt * fillRate);
                ShowProgress(shown);
                if (shown >= 1f && op.progress >= 0.9f) break;
                yield return null;
            }
            ShowProgress(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;   // the level's Awake / Start have run
            TracePlayers($"scene active after {Time.realtimeSinceStartup - startedAt:0.00}s, before joining");

            JoinPlayers();
            loaded = true;
            if (status != null) TextFx.Reveal(status, cfg.readyText);

            if (cfg.waitForPlayers && players.Count > 0)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                if (status != null) status.parent.style.display = DisplayStyle.None;
                if (readyRow != null) readyRow.style.display = DisplayStyle.Flex;
                while (!AllReady())
                {
                    ReadReady();
                    yield return null;
                }
            }

            goTime = 0f;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, cfg.goDuration));

            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null) rules.RequestStart(GameSession.Mode, GameSession.Party, GameSession.TargetScore);
            else Debug.LogWarning("[Loading] Pas de PoolMatchRules dans le niveau : la partie ne peut pas démarrer.");
            yield return null;
            yield return null;   // the match has started (applied in PoolMatchRules.Update)
            TracePlayers("match start");

            // Fade out, then gone.
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                if (root != null) root.style.opacity = 1f - t / 0.35f;
                yield return null;
            }
            Destroy(gameObject);
        }

        // The level's players, with the devices chosen in the menu. Joining
        // by pressing a button is switched off: the players are already
        // decided, a stray press must not add a third one.
        private void JoinPlayers()
        {
            PlayerInputManager manager = PlayerInputManager.instance;
            List<InputDevice> devices = new List<InputDevice>(GameSession.Devices);
            if (devices.Count == 0)
            {
                if (Keyboard.current != null) devices.Add(Keyboard.current);
                else if (Gamepad.current != null) devices.Add(Gamepad.current);
            }

            // Players already in the level (a player prefab placed in the
            // scene, which grabs a device on its own when the scene starts)
            // are reused and switched to the menu's device; only the missing
            // ones are joined, under a free index. Joining everyone under a
            // fixed index clashed with that placed player: the character on
            // screen ended up with the wrong device, or none.
            var existing = new List<PlayerInput>(PlayerInput.all);
            existing.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
            if (manager != null) manager.DisableJoining();
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice device = devices[i];
                string scheme = device is Gamepad ? "Gamepad" : "Keyboard&Mouse";
                InputDevice[] paired = DevicesFor(device);
                if (i < existing.Count)
                {
                    existing[i].SwitchCurrentControlScheme(scheme, paired);
                    existing[i].ActivateInput();
                }
                else if (manager != null)
                    manager.JoinPlayer(-1, -1, scheme, paired);
                else
                    Debug.LogWarning("[Loading] Pas de PlayerInputManager dans le niveau : le joueur " + (i + 1) + " ne peut pas être ajouté.");
            }

            for (int i = 0; i < devices.Count; i++) AddReadyPill(i, devices[i]);
            TracePlayers("players joined");
        }

        // A gamepad alone; the keyboard with the mouse.
        private static InputDevice[] DevicesFor(InputDevice device)
        {
            if (device is Gamepad) return new[] { device };
            var kbm = new List<InputDevice>();
            if (Keyboard.current != null) kbm.Add(Keyboard.current);
            if (Mouse.current != null) kbm.Add(Mouse.current);
            return kbm.ToArray();
        }

        // Diagnostic: each player's index, devices, scheme and input state.
        private static void TracePlayers(string when)
        {
            var sb = new System.Text.StringBuilder($"[Loading] {when}: {PlayerInput.all.Count} player(s)");
            foreach (PlayerInput p in PlayerInput.all)
            {
                sb.Append($"\n  P{p.playerIndex} '{p.name}' scheme {p.currentControlScheme ?? "—"}, input active {p.inputIsActive}, map {p.currentActionMap?.name ?? "—"} (enabled {p.currentActionMap?.enabled}), devices: ");
                foreach (InputDevice d in p.devices) sb.Append(d.displayName).Append("; ");
            }
            PoolMatchRules rules = PoolMatchRules.Instance;
            sb.Append($"\n  match started {(rules != null && rules.MatchStarted)}, time scale {Time.timeScale}, paused {PauseMenu.IsPaused}, cursor {UnityEngine.Cursor.lockState}");
            Debug.Log(sb.ToString());
        }

        private void AddReadyPill(int index, InputDevice device)
        {
            if (readyRow == null) { players.Add((device, null, null, false)); return; }
            VisualElement pill = K.Box(readyRow, "pill");
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.marginLeft = pill.style.marginRight = 30;
            pill.style.paddingLeft = 10;
            pill.style.paddingRight = 30;
            pill.style.paddingTop = pill.style.paddingBottom = 8;
            pill.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            K.Radius(pill, 50);
            VisualElement ball = K.BallVisual(pill, 64, index == 0 ? K.P1 : K.P2, "J" + (index + 1), false);
            ball.style.marginRight = 16;
            Label label = K.Text(pill, Format(cfg.pressFormat, index + 1, device is Gamepad ? "A" : "Entrée"), K.BodyFont, 36, K.Chalk, false);
            players.Add((device, pill, label, false));
        }

        private bool AllReady()
        {
            foreach (var p in players) if (!p.ready) return false;
            return true;
        }

        private void ReadReady()
        {
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p.ready) continue;
                bool pressed = p.device is Gamepad pad
                    ? pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame
                    : (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                      || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
                if (!pressed) continue;
                p.ready = true;
                if (p.pill != null)
                {
                    p.pill.style.backgroundColor = K.Hex("#30b566");
                    p.pill.style.scale = new Scale(new Vector3(1.06f, 1.06f, 1f));
                    TextFx.Reveal(p.label, Format(cfg.readyFormat, i + 1));
                }
                players[i] = p;
            }
        }

        // ---------- Frame ----------

        private void Update()
        {
            // The level's provisional OnGUI mode select must stay hidden
            // while this is up (the menu that switched it off is gone).
            PoolMatchRules.ExternalMenu = true;
            if (root == null) return;
            float dt = Time.unscaledDeltaTime;

            tipTimer -= dt;
            if (tipTimer <= 0f) NextTip();
            statusTimer -= dt;
            if (statusTimer <= 0f) NextStatus();

            foreach (Gutter b in balls)
            {
                b.x = Mathf.Lerp(b.x, b.target, 1f - Mathf.Exp(-9f * dt));
                b.element.style.left = Length.Percent(b.x);
                b.element.style.rotate = new Rotate(new Angle((b.x - b.target) * 18f, AngleUnit.Degree));
            }

            // Blinking "press" pills.
            float blink = 0.55f + 0.45f * Mathf.Round(Mathf.Repeat(Time.unscaledTime / 0.5f, 1f));
            foreach (var p in players) if (p.label != null && !p.ready) p.label.style.opacity = blink;

            if (goTime >= 0f)
            {
                goTime += dt;
                float k = Mathf.Clamp01(goTime / 0.3f);
                float s = Mathf.Lerp(0.2f, 1f, 1f - (1f - k) * (1f - k));
                goHolder.style.opacity = k;
                goHolder.style.scale = new Scale(new Vector3(s, s, 1f));
                goHolder.style.rotate = new Rotate(new Angle(Mathf.Lerp(-14f, -5f, k), AngleUnit.Degree));
            }
        }
    }
}
