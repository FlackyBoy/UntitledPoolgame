using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // In-game UI (prototype docs/ui/synthese.html), UI Toolkit, built in code.
    // One HUD per player over their camera viewport: plate with colour and
    // goal, "À toi !" tag (the waiting half is dimmed), powers in stock,
    // power gauge and strike point while aiming, interjections, sponsor card,
    // winner card with "Revanche". Created in any scene with a PoolMatchRules;
    // texts, colours and timings in HudSettings.
    public partial class MatchHud : MonoBehaviour
    {
        // Pool balls keep their standard colours (not a HUD choice).
        private static readonly Color BallCream = Hex("#f6f1e4");
        private static readonly Color[] BallColors =
        {
            Hex("#f4c20d"), Hex("#1f4fd1"), Hex("#d8261c"), Hex("#6a2c91"), Hex("#f07b12"), Hex("#138a3a"), Hex("#7a1f1f"), Hex("#111111"),
        };
        private const int Segments = 12;

        // Palette, from HudSettings (OnEnable).
        private HudSettings cfg;
        private Color Ink, Cream, Yellow, Red, P1, P2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        private static void TryCreate()
        {
            if (FindAnyObjectByType<PoolMatchRules>() == null || FindAnyObjectByType<MatchHud>() != null) return;
            var go = new GameObject("HUD (Synthèse)");
            go.SetActive(false);
            go.AddComponent<UIDocument>().panelSettings = CreatePanel();
            go.AddComponent<MatchHud>();
            go.SetActive(true);
        }

        // Full screen, scaled from 1920 × 1080; under the menu (100).
        public static PanelSettings CreatePanel(int sortingOrder = 90)
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/MenuTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = sortingOrder;
            return panel;
        }

        // ---------- State ----------

        // One player's part of the screen. Content is laid out for a
        // 960 × 1080 half and scaled to the real viewport (see Layout).
        private class Half
        {
            public PlayerInput input;
            public LocalPoolAimController aim;
            public LocalPoolPowerController powerCtl;
            public Camera cam;
            public int player = -1;          // PoolMatchRules index shown on this half
            public VisualElement frame, content, dim, fx;
            public VisualElement who, plateBalls, tag, ring, ringIcon, power, spin, spinDot, banner;
            public Label whoText, tagText, powerName, powerKey, bannerText, refusedHint;
            // The other power in stock (small, beside the selected one) and
            // the level badges.
            public VisualElement secondRing, secondIcon;
            public Label levelBadge, secondLevelBadge, switchKey;
            public PowerType secondIconType;
            // The power's own icon (PoolPower.HudIcon); null = the category's drawing.
            public Texture2D iconTexture, secondIconTexture;
            public PoolPower shownSecond;
            public int shownLevel = 1, shownSecondLevel = 1;
            public readonly VisualElement[] segments = new VisualElement[Segments];
            public VisualElement slot;
            // What the plate, the tag and the banner show, so they're only
            // rebuilt (and their strings only made) when it changes.
            public (int kind, int a, int b) plateKey;
            public int shownWho = -1, shownTag = int.MinValue;
            public (Hint hint, PoolPower power, PoolPocket pocket) bannerKey = (Hint.Unset, null, null);
            public PoolPower shownPower;
            public float shake, tagBump;
            // Power Wash Pool's clock, top centre: seconds shown (MinValue =
            // hidden, -1 = overtime) and the bump of the last tick (1 → 0).
            public Label clock;
            public int shownClock = int.MinValue;
            public float clockBump;
            public PowerType iconType;
            public readonly Lane shouts = new Lane(false), sponsors = new Lane(true), incoming = new Lane(true);
        }

        private class Fx
        {
            public VisualElement element;
            public float time, life;
            public Action<Fx, float> animate;   // (fx, 0..1)
            // Left on screen at the end (scale and rotation reset) instead of
            // removed. Per effect: an element can run several (pop and wiggle).
            public bool keep;
            // Where its exit starts (seconds): a Lane cuts it short by
            // jumping there. Defaults to the end (no exit phase).
            public float exitAt;
            public bool done;
        }

        // Interjections, sponsor cards, incoming cards: one of each at a time
        // per half, since each kind always sits at the same spot. The next
        // waits until the current one has been readable a moment
        // (minShoutTime / minCardTime), then cuts it short.
        private class Lane
        {
            public readonly bool card;
            public Fx current;
            public readonly Queue<Func<Fx>> waiting = new Queue<Func<Fx>>();
            public Lane(bool card) => this.card = card;
        }

        // Beyond this, the oldest waiting one is dropped: a backlog would
        // only show stale news.
        private const int MaxWaiting = 2;

        private enum Hint { Unset = -1, None, BallInHand, PickTable, PickPocket, PickPocketNone, CallPocket, CallPocketNone, OutOfReach, EightCalled, EightToCall, BlastArmed, MagnetArmed }

        private UIDocument document;
        private VisualElement root, halvesLayer, endLayer, endRays, endCard, endButton;
        private Label endTitle, endSubtitle, endWho, endButtonText;
        private VisualElement endWhoBox;
        private readonly List<VisualElement> confetti = new List<VisualElement>();
        // One per player who can join (split screen up to four).
        private readonly Half[] halves = new Half[PoolMatchRules.MaxPlayers];
        private readonly List<Fx> effects = new List<Fx>();
        private readonly List<(float at, Action run)> delayed = new List<(float, Action)>();
        private Font bodyFont, titleFont;
        private PoolPowerSpawnSettings powerSettings;
        private float endShownAt = -1f;
        // Boogie Night: the match whose winner was counted (MatchGeneration).
        private int boogieRecorded = -1;

        private void OnEnable()
        {
            cfg = PoolSettingsLoader.LoadOrDefault<HudSettings>("HudSettings");
            Ink = cfg.inkColor;
            Cream = cfg.cardColor;
            Yellow = cfg.highlightColor;
            Red = cfg.dangerColor;
            P1 = cfg.player1Color;
            P2 = cfg.player2Color;
            bodyFont = cfg.bodyFont != null ? cfg.bodyFont : Resources.Load<Font>("UI/Fonts/TitanOne-Regular");
            titleFont = cfg.titleFont != null ? cfg.titleFont : Resources.Load<Font>("UI/Fonts/BowlbyOne-Regular");
            powerSettings = PoolSettingsLoader.LoadOrDefault<PoolPowerSpawnSettings>("PoolPowerSpawnSettings");

            document = GetComponent<UIDocument>();
            if (document == null)
            {
                document = gameObject.AddComponent<UIDocument>();
                document.panelSettings = CreatePanel();
            }
            if (document.rootVisualElement == null)
            {
                Debug.LogWarning("[HUD] Pas de panneau UI Toolkit : ancien affichage conservé.");
                return;
            }

            Build();
            PoolMatchRules.ExternalHud = true;
            PoolBall.Pocketed += OnPocketed;
            PoolMatchRules.Fouled += OnFouled;
            PoolMatchRules.PowerPickedUp += OnPowerPickedUp;
            PoolMatchRules.PowerActivated += OnPowerActivated;
            PoolMatchRules.PowerRefused += OnPowerRefused;
            PoolMatchRules.TurnChanged += OnTurnChanged;
            PoolMatchRules.BallBlasted += OnBallBlasted;
            LocalPoolAimController.ShotTaken += OnShotTaken;
            // The free quarter's dimmed lamp (MatchHud.FreeQuarter).
            RenderPipelineManager.beginCameraRendering += DimForFreeCamera;
            RenderPipelineManager.endCameraRendering += UndimAfterFreeCamera;
        }

        private void OnDisable()
        {
            HideFree();
            RenderPipelineManager.beginCameraRendering -= DimForFreeCamera;
            RenderPipelineManager.endCameraRendering -= UndimAfterFreeCamera;
            PoolMatchRules.ExternalHud = false;
            PoolBall.Pocketed -= OnPocketed;
            PoolMatchRules.Fouled -= OnFouled;
            PoolMatchRules.PowerPickedUp -= OnPowerPickedUp;
            PoolMatchRules.PowerActivated -= OnPowerActivated;
            PoolMatchRules.PowerRefused -= OnPowerRefused;
            PoolMatchRules.TurnChanged -= OnTurnChanged;
            PoolMatchRules.BallBlasted -= OnBallBlasted;
            LocalPoolAimController.ShotTaken -= OnShotTaken;
        }

        // ---------- Frame ----------

        private void Update()
        {
            if (root == null) return;
            PoolMatchRules rules = PoolMatchRules.Instance;
            bool playing = rules != null && rules.MatchStarted;
            halvesLayer.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
            float dt = Time.unscaledDeltaTime;

            for (int i = delayed.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= delayed[i].at)
                {
                    Action run = delayed[i].run;
                    delayed.RemoveAt(i);
                    run();
                }

            UpdateEffects(dt);
            foreach (Half h in halves) PumpLanes(h);
            if (playing)
            {
                Layout(rules);
                foreach (Half h in halves)
                    if (h.input != null) UpdateHalf(h, rules, dt);
                UpdateFree(rules, root.layout.size, dt);
            }
            else HideFree();
            UpdateEnd(rules, dt);
        }

        // Each half over its player's camera viewport.
        private void Layout(PoolMatchRules rules)
        {
            Vector2 size = root.layout.size;
            if (float.IsNaN(size.x) || size.x < 1f) return;
            var inputs = PlayerInput.all;
            for (int i = 0; i < halves.Length; i++)
            {
                Half h = halves[i];
                if (i >= inputs.Count)
                {
                    h.input = null;
                    h.frame.style.display = DisplayStyle.None;
                    continue;
                }
                if (h.input != inputs[i])
                {
                    h.input = inputs[i];
                    h.aim = h.input.GetComponent<LocalPoolAimController>();
                    h.powerCtl = h.input.GetComponent<LocalPoolPowerController>();
                    h.cam = null;
                    h.plateKey = default;
                    h.shownWho = -1;
                    h.shownTag = int.MinValue;
                    h.bannerKey = (Hint.Unset, null, null);
                }
                h.player = Mathf.Clamp(rules.GetEffectivePlayerIndex(h.input.playerIndex), 0, rules.PlayerCount - 1);
                h.frame.style.display = DisplayStyle.Flex;

                if (h.cam == null) h.cam = h.input.camera != null ? h.input.camera : h.input.GetComponentInChildren<Camera>();
                Rect r = h.cam != null ? h.cam.rect : new Rect(0f, 0f, 1f, 1f);
                float x = Mathf.Clamp01(r.x), w = Mathf.Clamp01(r.xMax) - x;
                float y = Mathf.Clamp01(r.y), hgt = Mathf.Clamp01(r.yMax) - y;
                float left = x * size.x, top = (1f - y - hgt) * size.y, width = w * size.x, height = hgt * size.y;
                h.frame.style.left = left;
                h.frame.style.top = top;
                h.frame.style.width = width;
                h.frame.style.height = height;

                // Designed for a 960 × 1080 half (side by side on 1920 × 1080).
                // A quarter (3 or 4 players: 960 × 540) shrinks it by half, so
                // the whole height still fits.
                float k = Mathf.Clamp(Mathf.Min(width / 960f, height / 1080f), 0.45f, 1.4f) * Mathf.Max(0.1f, cfg.scale);
                h.content.style.width = width / k;
                h.content.style.height = height / k;
                h.content.style.scale = new Scale(new Vector3(k, k, 1f));
            }
        }

        private void UpdateHalf(Half h, PoolMatchRules rules, float dt)
        {
            int p = h.player;
            bool solo = PlayerInput.all.Count <= 1;
            bool myTurn = solo || rules.IsSimultaneous || rules.CurrentPlayer == p;
            float t = Time.unscaledTime;

            h.dim.style.display = cfg.dimWaitingPlayer && !myTurn && !rules.GameOver ? DisplayStyle.Flex : DisplayStyle.None;

            // Plate.
            // In 2 v 2, the team's colour (J1's or J2's): teammates match.
            int whoKey = p + 8 * PlayerBallGroups.ColourOf(p);
            if (h.shownWho != whoKey)
            {
                h.shownWho = whoKey;
                h.who.style.backgroundColor = cfg.PlayerColor(PlayerBallGroups.ColourOf(p));
                TextFx.Set(h.whoText, Format(cfg.playerTagFormat, p + 1));
            }
            UpdatePlate(h, rules);
            UpdateWashClock(h, rules, dt);

            // Turn tag: bobs on the player who plays, muted on the other.
            int tagState = myTurn ? -1 : rules.CurrentPlayer;
            if (h.shownTag != tagState)
            {
                h.shownTag = tagState;
                if (myTurn)
                {
                    TextFx.Set(h.tagText, cfg.yourTurnText);
                    h.tagText.style.fontSize = cfg.turnTagFontSize;
                    h.tagText.style.color = Ink;
                    h.tag.style.backgroundColor = Yellow;
                    Border(h.tag, 3, Ink);
                    h.tag.style.borderBottomWidth = 7;
                }
                else
                {
                    TextFx.Set(h.tagText, Format(cfg.otherTurnFormat, rules.CurrentPlayer + 1));
                    h.tagText.style.fontSize = cfg.turnTagFontSize * 0.7f;
                    h.tagText.style.color = Color.white;
                    h.tag.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
                    Border(h.tag, 0, Color.clear);
                    h.tag.style.translate = new Translate(0, 0);
                }
            }
            if (myTurn)
            {
                float period = Mathf.Max(0.05f, cfg.turnTagBobPeriod);
                h.tag.style.translate = new Translate(0, -cfg.turnTagBob * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / period)));
            }
            h.tagBump = Mathf.MoveTowards(h.tagBump, 0f, dt * 3f);
            float tagScale = 1f + 0.3f * h.tagBump;
            h.tag.style.scale = new Scale(new Vector3(tagScale, tagScale, 1f));
            h.tag.style.rotate = new Rotate(new Angle(-4f, AngleUnit.Degree));

            UpdatePowerSlot(h, rules);

            // Gauge and strike point, while aiming.
            LocalPoolAimController aim = h.aim;
            bool aiming = aim != null && aim.IsAiming;
            h.power.style.display = aiming ? DisplayStyle.Flex : DisplayStyle.None;
            if (aiming)
            {
                float charge = aim.ChargeFraction;
                int on = Mathf.RoundToInt(charge * Segments);
                for (int i = 0; i < Segments; i++)
                {
                    Color segColor = i < 7 ? cfg.gaugeLowColor : i < 10 ? cfg.gaugeMidColor : cfg.gaugeHighColor;
                    h.segments[i].style.backgroundColor = i < on ? segColor : new Color(0f, 0f, 0f, 0.1f);
                }
                Vector2 jitter = charge >= 0.999f ? new Vector2(Mathf.Sin(t * 80f), Mathf.Cos(t * 95f)) * 2f : Vector2.zero;
                h.power.style.translate = new Translate(jitter.x, jitter.y);
                Vector2 spin = aim.ContactOffset;
                h.spinDot.style.left = Length.Percent(50f + spin.x * 34f);
                h.spinDot.style.top = Length.Percent(50f - spin.y * 34f);
            }

            UpdateBanner(h, rules, myTurn);
            StackUnderSlot(h);

            // Shake (fouls, full-power shots).
            if (h.shake > 0f)
            {
                h.shake = Mathf.Max(0f, h.shake - dt);
                float a = cfg.shakeAmplitude * (h.shake / Mathf.Max(0.01f, cfg.shakeDuration));
                h.content.style.translate = new Translate(Mathf.Sin(t * 70f) * a, Mathf.Cos(t * 60f) * a * 0.6f);
            }
            else h.content.style.translate = new Translate(0, 0);
        }

        // The power's own icon as the element's background, or none (the
        // category drawing, generateVisualContent, takes over).
        private static void SetIcon(VisualElement icon, ref Texture2D shown, PoolPower power)
        {
            shown = power != null ? power.HudIcon : null;
            icon.style.backgroundImage = shown != null ? new StyleBackground(shown) : new StyleBackground(StyleKeyword.None);
            icon.MarkDirtyRepaint();
        }

        // "N2" in a small white pill at the top right of a ring.
        private Label LevelBadge(VisualElement ring, float size)
        {
            Label badge = Text(ring, "", titleFont, size, Ink);
            badge.style.position = Position.Absolute;
            badge.style.right = -size * 0.4f;
            badge.style.top = -size * 0.35f;
            badge.style.backgroundColor = Color.white;
            badge.style.paddingLeft = badge.style.paddingRight = size * 0.25f;
            Radius(badge, size * 0.4f);
            Border(badge, 2, Ink);
            badge.style.display = DisplayStyle.None;
            return badge;
        }

        private void ShowLevel(Label badge, int level)
        {
            badge.style.display = level > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (level > 1) badge.text = Format(cfg.powerLevelBadgeFormat, level);
        }

        // Selected power (big ring, name, key) and the other one in stock
        // (small ring). Rebuilt only when what's in stock changes.
        private void UpdatePowerSlot(Half h, PoolMatchRules rules)
        {
            int selected = rules.SelectedPowerSlot(h.player);
            int other = (selected + 1) % PoolMatchRules.PowerSlots;
            PoolPower held = rules.GetHeldPower(h.player);
            PoolPower second = rules.GetPowerInSlot(h.player, other);
            int level = rules.GetHeldPowerLevel(h.player), secondLevel = rules.GetPowerLevelInSlot(h.player, other);
            if (held == h.shownPower && level == h.shownLevel && second == h.shownSecond && secondLevel == h.shownSecondLevel) return;
            bool levelledUp = held != null && held == h.shownPower && level > h.shownLevel;
            h.shownLevel = level;
            h.shownSecond = second;
            h.shownSecondLevel = secondLevel;

            h.secondRing.style.display = second != null ? DisplayStyle.Flex : DisplayStyle.None;
            h.switchKey.style.display = second != null && held != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (second != null)
            {
                h.secondIconType = second.Type;
                SetIcon(h.secondIcon, ref h.secondIconTexture, second);
                h.secondRing.style.backgroundColor = powerSettings != null ? powerSettings.GetColor(second.Type) : Red;
                Border(h.secondRing, 3, Ink);
                h.secondIcon.MarkDirtyRepaint();
                ShowLevel(h.secondLevelBadge, secondLevel);
            }
            ShowLevel(h.levelBadge, held != null ? level : 1);
            if (levelledUp) Pop(h.ring, 0.6f);
            if (held == h.shownPower) return;
            h.shownPower = held;

            if (held == null)
            {
                h.ring.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
                Border(h.ring, 4, new Color(1f, 1f, 1f, 0.45f));
                h.ringIcon.style.display = DisplayStyle.None;
                TextFx.Set(h.powerName, "—");
                h.powerKey.style.display = DisplayStyle.None;
                return;
            }
            h.iconType = held.Type;
            SetIcon(h.ringIcon, ref h.iconTexture, held);
            h.ring.style.backgroundColor = powerSettings != null ? powerSettings.GetColor(held.Type) : Red;
            Border(h.ring, 4, Ink);
            h.ring.style.borderBottomWidth = 8;
            h.ringIcon.style.display = DisplayStyle.Flex;
            h.ringIcon.MarkDirtyRepaint();
            TextFx.Reveal(h.powerName, held.PowerName);
            h.powerKey.style.display = DisplayStyle.Flex;
            Pop(h.ring, 0.6f);
        }

        private void UpdateBanner(Half h, PoolMatchRules rules, bool myTurn)
        {
            LocalPoolAimController aim = h.aim;
            LocalPoolPowerController powerCtl = h.powerCtl;
            PoolPower power = powerCtl != null ? powerCtl.ChoosingPower : null;
            PoolPocket magnet = myTurn ? rules.GetArmedMagnetPocket(h.player) : null;
            // None at the end of the match.
            (Hint hint, PoolPower power, PoolPocket pocket) key = default;
            if (rules.GameOver) { }
            else if (aim != null && aim.IsBallPlacementActive)
                key = (Hint.BallInHand, null, null);
            else if (powerCtl != null && powerCtl.IsChoosingTablePoint)
                key = (Hint.PickTable, power, null);
            else if (power != null)
                key = powerCtl.TargetedPocket != null ? (Hint.PickPocket, power, powerCtl.TargetedPocket) : (Hint.PickPocketNone, power, null);
            else if (aim != null && aim.IsCallPocketActive)
                key = aim.HighlightedPocket != null ? (Hint.CallPocket, null, aim.HighlightedPocket) : (Hint.CallPocketNone, null, null);
            else if (aim != null && aim.IsOutOfReach)
                key = (Hint.OutOfReach, null, null);
            else if (myTurn && rules.IsGoingForPocket(h.player))
            {
                PoolPocket called = rules.CalledPocketOf(h.player);
                key = called != null ? (Hint.EightCalled, null, called) : (Hint.EightToCall, null, null);
            }
            else if (myTurn && rules.IsBallBlastArmed(h.player))
                key = (Hint.BlastArmed, null, null);
            else if (magnet != null)
                key = (Hint.MagnetArmed, null, magnet);
            if (key == h.bannerKey) return;
            h.bannerKey = key;

            string where = key.pocket != null ? key.pocket.DescribeLocation() : null;
            string name = key.power != null ? key.power.PowerName : null;
            string text = key.hint switch
            {
                Hint.BallInHand => cfg.ballInHandBanner,
                Hint.PickTable => Format(cfg.pickTableBannerFormat, name),
                Hint.PickPocket => Format(cfg.pickPocketBannerFormat, name, where),
                Hint.PickPocketNone => Format(cfg.pickPocketNoneFormat, name),
                Hint.CallPocket => Format(cfg.callPocketBannerFormat, where),
                Hint.CallPocketNone => cfg.callPocketNoneBanner,
                Hint.OutOfReach => cfg.outOfReachBanner,
                Hint.EightCalled => Format(cfg.eightCalledBannerFormat, where),
                Hint.EightToCall => cfg.eightToCallBanner,
                Hint.BlastArmed => cfg.blastArmedBanner,
                Hint.MagnetArmed => Format(cfg.magnetArmedBannerFormat, where),
                _ => null,
            };
            // An emptied text in the settings hides that banner.
            bool show = !string.IsNullOrEmpty(text);
            h.banner.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) TextFx.Set(h.bannerText, text);
        }
    }
}
