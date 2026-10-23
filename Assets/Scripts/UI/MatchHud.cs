using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
    public class MatchHud : MonoBehaviour
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
        private static PanelSettings CreatePanel()
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/MenuTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 90;
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
        private Label endTitle, endSubtitle, endWho;
        private VisualElement endWhoBox;
        private readonly List<VisualElement> confetti = new List<VisualElement>();
        private readonly Half[] halves = new Half[2];
        private readonly List<Fx> effects = new List<Fx>();
        private readonly List<(float at, Action run)> delayed = new List<(float, Action)>();
        private Font bodyFont, titleFont;
        private PoolPowerSpawnSettings powerSettings;
        private float endShownAt = -1f;

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
        }

        private void OnDisable()
        {
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

        // ---------- Building ----------

        private void Build()
        {
            root = document.rootVisualElement;
            Fill(root);
            root.pickingMode = PickingMode.Ignore;

            halvesLayer = Box(root, "halves");
            Fill(halvesLayer);
            halvesLayer.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < halves.Length; i++) halves[i] = BuildHalf(i);

            BuildEnd();
        }

        private Half BuildHalf(int index)
        {
            var h = new Half();
            h.frame = Box(halvesLayer, "half" + index);
            h.frame.style.position = Position.Absolute;
            h.frame.style.overflow = Overflow.Hidden;
            h.frame.style.display = DisplayStyle.None;

            h.content = Box(h.frame, "content");
            h.content.style.position = Position.Absolute;
            h.content.style.left = h.content.style.top = 0;
            h.content.style.transformOrigin = new TransformOrigin(0, 0);

            // The player waiting for their turn: their half is dimmed.
            h.dim = Box(h.content, "dim");
            Fill(h.dim);
            h.dim.style.backgroundColor = cfg.waitingDimColor;

            // Plate: player colour + what they're playing for.
            VisualElement plate = PopCard(h.content, "plate");
            plate.style.position = Position.Absolute;
            plate.style.left = Length.Percent(3);
            plate.style.top = Length.Percent(4);
            plate.style.flexDirection = FlexDirection.Row;
            plate.style.alignItems = Align.Center;
            plate.style.paddingLeft = plate.style.paddingTop = plate.style.paddingBottom = 6;
            plate.style.paddingRight = 14;
            h.who = Box(plate, "who");
            h.who.style.width = h.who.style.height = 54;
            h.who.style.justifyContent = Justify.Center;
            h.who.style.marginRight = 10;
            Radius(h.who, 11);
            Border(h.who, 3, Ink);
            h.whoText = Text(h.who, "J1", titleFont, 24, Color.white);
            h.plateBalls = Box(plate, "balls");
            h.plateBalls.style.flexDirection = FlexDirection.Row;
            h.plateBalls.style.alignItems = Align.Center;

            // "À toi !" tag.
            h.tag = Box(h.content, "turn-tag");
            h.tag.style.position = Position.Absolute;
            h.tag.style.left = Length.Percent(4);
            h.tag.style.top = Length.Percent(17);
            h.tag.style.paddingLeft = h.tag.style.paddingRight = 16;
            h.tag.style.paddingTop = h.tag.style.paddingBottom = 4;
            Radius(h.tag, 9);
            h.tagText = Text(h.tag, cfg.yourTurnText, titleFont, cfg.turnTagFontSize, Ink);

            // Power in stock: a ring with the type's colour and icon.
            VisualElement slot = h.slot = Box(h.content, "power-slot");
            slot.style.position = Position.Absolute;
            slot.style.right = Length.Percent(3);
            slot.style.top = Length.Percent(4);
            slot.style.alignItems = Align.Center;
            // The whole block grows from its top-right corner (it stays
            // anchored there): rings, icons, name, key, second power.
            slot.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(0), 0f);
            slot.style.scale = new Scale(Vector3.one * Mathf.Max(0.1f, cfg.powerSlotScale));
            h.ring = Box(slot, "ring");
            h.ring.style.width = h.ring.style.height = 84;
            h.ring.style.justifyContent = Justify.Center;
            h.ring.style.alignItems = Align.Center;
            Radius(h.ring, 42);
            h.ringIcon = Box(h.ring, "icon");
            h.ringIcon.style.width = h.ringIcon.style.height = 46;
            h.ringIcon.generateVisualContent += ctx => { if (h.iconTexture == null) DrawPowerIcon(ctx, h.iconType); };
            h.powerName = Text(slot, "—", bodyFont, 20, Color.white);
            h.powerName.style.marginTop = 4;
            InkShadow(h.powerName, 3);
            h.powerKey = Text(slot, cfg.powerKeyText, bodyFont, 16, Ink);
            h.powerKey.style.backgroundColor = Color.white;
            h.powerKey.style.paddingLeft = h.powerKey.style.paddingRight = 8;
            h.powerKey.style.marginTop = 3;
            Radius(h.powerKey, 6);
            Border(h.powerKey, 3, Ink);
            h.switchKey = Text(slot, cfg.powerSwitchKeyText, bodyFont, 14, Color.white);
            h.switchKey.style.marginTop = 2;
            InkShadow(h.switchKey, 2);

            // The other power in stock: a smaller ring to the left of the
            // selected one.
            h.secondRing = Box(slot, "second-ring");
            h.secondRing.style.position = Position.Absolute;
            h.secondRing.style.left = -58;
            h.secondRing.style.top = 22;
            h.secondRing.style.width = h.secondRing.style.height = 48;
            h.secondRing.style.justifyContent = Justify.Center;
            h.secondRing.style.alignItems = Align.Center;
            Radius(h.secondRing, 24);
            h.secondRing.style.opacity = 0.85f;
            h.secondIcon = Box(h.secondRing, "icon");
            h.secondIcon.style.width = h.secondIcon.style.height = 26;
            h.secondIcon.generateVisualContent += ctx => { if (h.secondIconTexture == null) DrawPowerIcon(ctx, h.secondIconType); };
            h.levelBadge = LevelBadge(h.ring, 22);
            h.secondLevelBadge = LevelBadge(h.secondRing, 15);

            // Power gauge with the strike point (while aiming).
            h.power = Box(h.content, "power");
            h.power.style.position = Position.Absolute;
            h.power.style.left = Length.Percent(21);
            h.power.style.width = Length.Percent(58);
            h.power.style.bottom = Length.Percent(5);
            h.power.style.flexDirection = FlexDirection.Row;
            h.power.style.alignItems = Align.Center;
            h.spin = Box(h.power, "spin");
            h.spin.style.width = h.spin.style.height = 60;
            h.spin.style.flexShrink = 0;
            h.spin.style.marginRight = 12;
            h.spin.style.backgroundColor = Hex("#e6e6e6");
            Radius(h.spin, 30);
            Border(h.spin, 3, Ink);
            h.spinDot = Box(h.spin, "dot");
            h.spinDot.style.position = Position.Absolute;
            h.spinDot.style.width = h.spinDot.style.height = 14;
            h.spinDot.style.backgroundColor = Red;
            h.spinDot.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            Radius(h.spinDot, 7);
            VisualElement bar = Box(h.power, "bar");
            bar.style.flexGrow = 1;
            bar.style.height = 38;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.backgroundColor = Cream;
            bar.style.paddingLeft = bar.style.paddingRight = bar.style.paddingTop = bar.style.paddingBottom = 4;
            Radius(bar, 20);
            Border(bar, 4, Ink);
            bar.style.borderBottomWidth = 8;
            for (int i = 0; i < Segments; i++)
            {
                VisualElement seg = Box(bar, "seg");
                seg.style.flexGrow = 1;
                seg.style.marginLeft = seg.style.marginRight = 1.5f;
                Radius(seg, 6);
                h.segments[i] = seg;
            }
            Label powerLabel = Text(h.power, cfg.powerGaugeText, titleFont, 22, Color.white);
            powerLabel.style.marginLeft = 12;
            InkShadow(powerLabel, 3);

            // Hint / instruction banner (out of reach, ball in hand, call the 8).
            h.banner = PopCard(h.content, "banner");
            h.banner.style.position = Position.Absolute;
            h.banner.style.left = Length.Percent(50);
            h.banner.style.bottom = Length.Percent(15);
            h.banner.style.translate = new Translate(Length.Percent(-50), 0);
            h.banner.style.paddingLeft = h.banner.style.paddingRight = 20;
            h.banner.style.paddingTop = h.banner.style.paddingBottom = 8;
            h.banner.style.display = DisplayStyle.None;
            h.bannerText = Text(h.banner, "", bodyFont, cfg.bannerFontSize, Ink);

            // Interjections and cards go on top.
            h.fx = Box(h.content, "fx");
            Fill(h.fx);
            return h;
        }

        private void BuildEnd()
        {
            endLayer = Box(root, "end");
            Fill(endLayer);
            endLayer.style.backgroundColor = new Color(0.03f, 0.02f, 0.01f, 0.6f);
            endLayer.style.display = DisplayStyle.None;

            endRays = Box(endLayer, "rays");
            endRays.style.position = Position.Absolute;
            endRays.style.width = endRays.style.height = 1500;
            endRays.style.left = Length.Percent(50);
            endRays.style.top = Length.Percent(30);
            endRays.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            endRays.generateVisualContent += ctx => DrawRays(ctx, new Color(1f, 0.82f, 0.25f, 0.16f));

            for (int i = 0; cfg.showConfetti && i < 50; i++)
            {
                VisualElement c = Box(endLayer, "confetti");
                c.style.position = Position.Absolute;
                c.style.width = 18;
                c.style.height = 9;
                Radius(c, 2);
                Color[] colors = { Yellow, P1, Cream, Yellow, P2 };
                c.style.backgroundColor = colors[i % colors.Length];
                c.style.left = Length.Percent((i * 23) % 100);
                confetti.Add(c);
            }

            endCard = PopCard(endLayer, "winner");
            endCard.style.position = Position.Absolute;
            endCard.style.left = Length.Percent(50);
            endCard.style.top = Length.Percent(30);
            endCard.style.flexDirection = FlexDirection.Row;
            endCard.style.alignItems = Align.Center;
            endCard.style.paddingLeft = 14;
            endCard.style.paddingRight = 40;
            endCard.style.paddingTop = endCard.style.paddingBottom = 14;
            endWhoBox = Box(endCard, "who");
            endWhoBox.style.width = endWhoBox.style.height = 110;
            endWhoBox.style.justifyContent = Justify.Center;
            endWhoBox.style.marginRight = 24;
            Radius(endWhoBox, 20);
            Border(endWhoBox, 4, Ink);
            endWho = Text(endWhoBox, "J1", titleFont, 50, Color.white);
            VisualElement texts = Box(endCard, "texts");
            endTitle = Text(texts, Format(cfg.winnerFormat, 1), titleFont, 76, Ink);
            endTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            endSubtitle = Text(texts, "", bodyFont, 28, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            endSubtitle.style.unityTextAlign = TextAnchor.MiddleLeft;

            endButton = PopCard(endLayer, "revanche");
            endButton.style.position = Position.Absolute;
            endButton.style.left = Length.Percent(50);
            endButton.style.top = Length.Percent(62);
            endButton.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            endButton.style.flexDirection = FlexDirection.Row;
            endButton.style.alignItems = Align.Center;
            endButton.style.backgroundColor = Yellow;
            endButton.style.paddingLeft = 12;
            endButton.style.paddingRight = 34;
            endButton.style.paddingTop = endButton.style.paddingBottom = 10;
            endButton.pickingMode = PickingMode.Position;
            VisualElement buttonBall = MiniBall(endButton, 56, 1);
            buttonBall.style.marginRight = 14;
            Text(endButton, cfg.rematchText, titleFont, 44, Ink);
            Label hint = Text(endLayer, cfg.rematchHint, bodyFont, 24, new Color(1f, 1f, 1f, 0.7f));
            hint.style.position = Position.Absolute;
            hint.style.left = Length.Percent(50);
            hint.style.top = Length.Percent(71);
            hint.style.translate = new Translate(Length.Percent(-50), 0);
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
            }
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
                h.player = Mathf.Clamp(rules.GetEffectivePlayerIndex(h.input.playerIndex), 0, 1);
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
                float k = Mathf.Clamp(Mathf.Min(width / 960f, height / 1080f), 0.6f, 1.4f) * Mathf.Max(0.1f, cfg.scale);
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
            if (h.shownWho != p)
            {
                h.shownWho = p;
                h.who.style.backgroundColor = p == 0 ? P1 : P2;
                TextFx.Set(h.whoText, Format(cfg.playerTagFormat, p + 1));
            }
            UpdatePlate(h, rules);

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

        // Rebuilt only when what it shows changes.
        private void UpdatePlate(Half h, PoolMatchRules rules)
        {
            int p = h.player;
            BallGroup group = rules.GetGroup(p);
            bool hasScore = rules.TryGetScore(p, out int score, out int target);
            int nextNine = rules.Mode == PoolGameMode.NineBall ? LowestBallOnTable(9) : 0;
            // Bit n set = ball n still on the table.
            int onTable = 0;
            foreach (PoolBall ball in PoolBall.Active)
                if (!ball.IsCueBall && ball.Number > 0 && ball.Number < 32) onTable |= 1 << ball.Number;

            // Groupe identique: the balls left would tell the groups apart.
            (int kind, int a, int b) key =
                rules.AreGroupsDisguised ? (1, 0, 0)
                : hasScore ? (2, score, target)
                : rules.Mode == PoolGameMode.NineBall ? (3, nextNine, 0)
                : group == BallGroup.Solid || group == BallGroup.Stripe ? (4, (int)group, onTable)
                : (5, 0, 0);
            if (key == h.plateKey) return;
            h.plateKey = key;

            h.plateBalls.Clear();
            if (rules.AreGroupsDisguised)
                Text(h.plateBalls, cfg.groupsDisguisedText, bodyFont, 22, Ink);
            else if (hasScore)
            {
                Text(h.plateBalls, $"{score}", titleFont, 34, Ink);
                Text(h.plateBalls, $" / {target}", bodyFont, 24, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            }
            else if (rules.Mode == PoolGameMode.NineBall)
            {
                Text(h.plateBalls, cfg.nextBallText, bodyFont, 22, Ink);
                if (nextNine > 0) MiniBall(h.plateBalls, 34, nextNine);
            }
            else if (group == BallGroup.Solid || group == BallGroup.Stripe)
            {
                int first = group == BallGroup.Solid ? 1 : 9;
                bool cleared = true;
                for (int n = first; n < first + 7; n++)
                {
                    VisualElement b = MiniBall(h.plateBalls, 28, n);
                    b.style.marginRight = 3;
                    if ((onTable & (1 << n)) == 0)
                    {
                        b.style.opacity = 0.25f;
                        b.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
                    }
                    else cleared = false;
                }
                if (cleared)
                {
                    Text(h.plateBalls, " → ", bodyFont, 22, Ink);
                    MiniBall(h.plateBalls, 34, 8);
                }
            }
            else Text(h.plateBalls, cfg.groupUndecidedText, bodyFont, 22, Ink);
        }

        private static int LowestBallOnTable(int max)
        {
            int lowest = 0;
            foreach (PoolBall ball in PoolBall.Active)
                if (!ball.IsCueBall && ball.Number <= max && (lowest == 0 || ball.Number < lowest)) lowest = ball.Number;
            return lowest;
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
            else if (myTurn && rules.IsShootingForEightBall(h.player))
                key = rules.CalledEightBallPocket != null ? (Hint.EightCalled, null, rules.CalledEightBallPocket) : (Hint.EightToCall, null, null);
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

        // ---------- Events ----------

        private void OnPocketed(PoolBall ball, PoolPocket pocket)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.MatchStarted || rules.GameOver) return;
            // Destroyed by Ball Blast: "BOUM !" already said it.
            if (ball == lastBlasted) { lastBlasted = null; return; }
            // Speed Pool: the owner of the cue ball or of the group, not the turn.
            int player = rules.PotOwner(ball);
            if (ball.IsCueBall) { Shout(player, cfg.cueBallPottedShout, Red, false); return; }
            if (!cfg.showPotShouts) return;
            // Cheered only when it's one of the shooter's balls: an opponent's
            // ball gets its own (rueful) shout, the early 8 none.
            switch (rules.MeaningOfPot(ball))
            {
                case PoolMatchRules.PotMeaning.Good:
                    ShoutOneOf(player, cfg.potShouts, Yellow, true);
                    break;
                case PoolMatchRules.PotMeaning.Opponent:
                    ShoutOneOf(player, cfg.opponentBallPotShouts, Red, false);
                    break;
            }
        }

        // One of the texts at random; nothing when the list is empty.
        private void ShoutOneOf(int player, string[] texts, Color color, bool burst)
        {
            if (texts == null || texts.Length == 0) return;
            Shout(player, texts[UnityEngine.Random.Range(0, texts.Length)], color, burst);
        }

        private PoolBall lastBlasted;

        private void OnBallBlasted(PoolBall ball, int shooter)
        {
            lastBlasted = ball;
            Half h = HalfOf(shooter);
            if (h != null) h.shake = cfg.shakeDuration;
            Shout(shooter, cfg.blastShout, cfg.blastColor, true);
        }

        // Fired before the turn passes: CurrentPlayer is the one who fouled.
        private void OnFouled()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null) return;
            Half h = HalfOf(rules.CurrentPlayer);
            if (h != null) h.shake = cfg.shakeDuration;
            Shout(rules.CurrentPlayer, cfg.foulShout, Red, false);
            delayed.Add((Time.unscaledTime + cfg.foulToBallInHandDelay, () =>
            {
                PoolMatchRules r = PoolMatchRules.Instance;
                if (r != null && !r.GameOver) Shout(r.CurrentPlayer, cfg.ballInHandShout, Color.white, false);
            }));
        }

        private void OnTurnChanged(int player)
        {
            Half h = HalfOf(player);
            if (h != null) h.tagBump = 1f;
        }

        // A new power: the sponsor card. The same one again: its level goes
        // up. Lost (stock full, or already at the top level): a word.
        private void OnPowerPickedUp(int player, PoolPower power, PoolMatchRules.PowerPickup outcome, int level)
        {
            if (power == null) return;
            switch (outcome)
            {
                case PoolMatchRules.PowerPickup.Added:
                    if (cfg.showSponsorCard) Sponsor(player, power);
                    break;
                case PoolMatchRules.PowerPickup.LevelUp:
                    Shout(player, Format(cfg.powerLevelUpShoutFormat, level, power.PowerName), Yellow, true);
                    break;
                case PoolMatchRules.PowerPickup.Lost:
                    Shout(player, cfg.powerLostShout, Red, false);
                    break;
            }
        }

        // The launcher hears it went, the other player gets a card saying
        // what's coming and when.
        private void OnPowerActivated(int player, PoolPower power, bool waitsForOpponentTurn)
        {
            if (power == null) return;
            Shout(player, power.TargetsOpponent ? cfg.powerUsedShout : cfg.powerActivatedShout, Yellow, false);
            if (!cfg.showIncomingCard || (!power.TargetsOpponent && !cfg.showOpponentBuffCard)) return;
            string line = !power.TargetsOpponent ? cfg.incomingBuffLine
                : waitsForOpponentTurn ? cfg.incomingNextTurnLine : cfg.incomingNowLine;
            string header = Format(power.TargetsOpponent ? cfg.incomingAttackHeaderFormat : cfg.incomingBuffHeaderFormat, player + 1);
            Incoming(1 - player, power, header, line);
        }

        // The slot shakes, with a word on why (an emptied text = no word).
        private void OnPowerRefused(int player, PowerRefusal reason)
        {
            Half h = HalfOf(player);
            if (h == null) return;
            Wiggle(h.ring, 0.35f);
            string text = reason switch
            {
                PowerRefusal.OwnTurnOnly => cfg.refusedOwnTurnOnlyText,
                PowerRefusal.OpponentTurnOnly => cfg.refusedOpponentTurnOnlyText,
                PowerRefusal.BallsMoving => cfg.refusedBallsMovingText,
                PowerRefusal.PointerBusy => cfg.refusedPointerBusyText,
                _ => cfg.refusedNoPowerText,
            };
            if (string.IsNullOrEmpty(text)) return;
            // One at a time: a mashed button doesn't stack the hints.
            h.refusedHint?.RemoveFromHierarchy();
            Label hint = Text(h.fx, text, bodyFont, 18, Ink);
            h.refusedHint = hint;
            hint.style.position = Position.Absolute;
            hint.style.right = Length.Percent(3);
            hint.style.backgroundColor = Color.white;
            hint.style.paddingLeft = hint.style.paddingRight = 10;
            hint.style.paddingTop = hint.style.paddingBottom = 4;
            Radius(hint, 8);
            Border(hint, 3, Red);
            StackUnderSlot(h);
            AddFx(hint, Mathf.Max(0.3f, cfg.refusedDuration), (fx, u) =>
            {
                float k = Mathf.Clamp01(u / 0.12f);
                fx.element.style.scale = new Scale(new Vector3(Mathf.Lerp(0.6f, 1f, BackOut(k)), Mathf.Lerp(0.6f, 1f, BackOut(k)), 1f));
                fx.element.style.opacity = u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f;
            });
        }

        private void OnShotTaken(int inputIndex, float charge)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || charge < cfg.fullPowerThreshold) return;
            int player = rules.GetEffectivePlayerIndex(inputIndex);
            Half h = HalfOf(player);
            if (h != null) h.shake = cfg.shakeDuration;
            Shout(player, cfg.fullPowerShout, Yellow, true);
        }

        // The half showing that player; in solo, the only one.
        private Half HalfOf(int player)
        {
            Half only = null;
            int active = 0;
            foreach (Half h in halves)
            {
                if (h.input == null) continue;
                active++;
                only = h;
                if (h.player == player) return h;
            }
            return active == 1 ? only : null;
        }

        // ---------- Effects ----------

        // From a Feel sequence (MMF_PoolHudText): a big word on that player's
        // half (-1 = both). color null = yellow.
        public static void ShowShout(int player, string markup, Color? color = null, bool burst = false)
        {
            MatchHud hud = FindAnyObjectByType<MatchHud>();
            if (hud == null) return;
            Color c = color ?? hud.Yellow;
            if (player >= 0) { hud.Shout(player, markup, c, burst); return; }
            foreach (Half h in hud.halves)
                if (h != null && h.input != null) hud.Shout(h.player, markup, c, burst);
        }

        public static float ShoutDuration =>
            PoolSettingsLoader.LoadOrDefault<HudSettings>("HudSettings").shoutDuration;

        private void Shout(int player, string text, Color color, bool burst)
        {
            Half h = HalfOf(player);
            // An emptied text in the settings turns that interjection off.
            if (h == null || string.IsNullOrEmpty(text)) return;
            Enqueue(h, h.shouts, () => ShowShoutNow(h, text, color, burst));
        }

        private Fx ShowShoutNow(Half h, string text, Color color, bool burst)
        {
            // Big outlined word that pops in tilted, then floats away.
            VisualElement holder = Box(h.fx, "shout");
            if (burst)
            {
                VisualElement rays = Box(h.fx, "burst");
                rays.PlaceBehind(holder);
                rays.style.position = Position.Absolute;
                rays.style.width = rays.style.height = 460;
                rays.style.left = Length.Percent(50);
                rays.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                rays.generateVisualContent += ctx => DrawRays(ctx, new Color(1f, 0.82f, 0.25f, 0.55f));
                AddFx(rays, Mathf.Max(0.05f, cfg.burstDuration), (fx, u) =>
                {
                    float s = Mathf.Lerp(0.2f, 1.3f, u);
                    fx.element.style.scale = new Scale(new Vector3(s, s, 1f));
                    fx.element.style.rotate = new Rotate(new Angle(60f * u, AngleUnit.Degree));
                    fx.element.style.opacity = 1f - u;
                    fx.element.style.top = ShoutCenter(h, holder);
                });
            }

            holder.style.position = Position.Absolute;
            holder.style.left = Length.Percent(50);
            holder.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            Label label = Text(holder, text, titleFont, cfg.shoutFontSize, color);
            TextFx.Reveal(label, text);   // letters pop in one after the other
            label.style.unityTextOutlineWidth = 3f;
            label.style.unityTextOutlineColor = Ink;
            InkShadow(label, 7);
            float life = Mathf.Max(0.05f, cfg.shoutDuration);
            Fx shout = AddFx(holder, life, (fx, u) =>
            {
                float s, rot = -6f, op = 1f, rise = 0f;
                if (u < 0.22f) { float k = u / 0.22f; s = Mathf.Lerp(0.2f, 1.12f, k); rot = Mathf.Lerp(-18f, -6f, k); op = k; }
                else if (u < 0.35f) s = Mathf.Lerp(1.12f, 1f, (u - 0.22f) / 0.13f);
                else s = 1f;
                if (u > 0.8f) { float k = (u - 0.8f) / 0.2f; op = 1f - k; rise = 0.07f * k; }
                fx.element.style.scale = new Scale(new Vector3(s, s, 1f));
                fx.element.style.rotate = new Rotate(new Angle(rot, AngleUnit.Degree));
                fx.element.style.opacity = op;
                fx.element.style.top = ShoutCenter(h, holder) - rise * ContentHeight(h);
            });
            shout.exitAt = 0.8f * life;
            return shout;
        }

        // 40 % down the half, or just under the incoming card when there is
        // one (stacked under the power slot, it reaches that low).
        private float ShoutCenter(Half h, VisualElement shout)
        {
            float center = 0.4f * ContentHeight(h);
            VisualElement card = h.incoming.current?.element;
            if (card == null || card.panel == null) return center;
            float cardBottom = card.layout.yMax, half = shout.layout.height * 0.5f;
            if (float.IsNaN(cardBottom) || float.IsNaN(half)) return center;
            return Mathf.Max(center, cardBottom + 8f + half);
        }

        private static float ContentHeight(Half h)
        {
            float height = h.content.layout.height;
            return float.IsNaN(height) || height < 1f ? 1080f : height;
        }

        // Top right, one under the other: power slot, refusal hint, incoming
        // card (the slot's size varies, so no fixed offsets).
        private void StackUnderSlot(Half h)
        {
            Rect slot = h.slot.layout;
            if (float.IsNaN(slot.height)) return;
            float y = slot.y + slot.height * Mathf.Max(0.1f, cfg.powerSlotScale) + 6f;
            if (h.refusedHint != null && h.refusedHint.panel != null)
            {
                h.refusedHint.style.top = y;
                float hintHeight = h.refusedHint.layout.height;
                if (!float.IsNaN(hintHeight)) y += hintHeight + 6f;
            }
            VisualElement card = h.incoming.current?.element;
            if (card != null && card.panel != null) card.style.top = Mathf.Max(y + 4f, 0.24f * ContentHeight(h));
        }

        private void Enqueue(Half h, Lane lane, Func<Fx> show)
        {
            while (lane.waiting.Count >= MaxWaiting) lane.waiting.Dequeue();
            lane.waiting.Enqueue(show);
            PumpLanes(h);
        }

        private void PumpLanes(Half h)
        {
            Pump(h.shouts);
            Pump(h.sponsors);
            Pump(h.incoming);
            if (h.input != null) StackUnderSlot(h);
        }

        private void Pump(Lane lane)
        {
            if (lane.current != null && lane.current.done) lane.current = null;
            if (lane.waiting.Count == 0) return;
            if (lane.current == null)
            {
                lane.current = lane.waiting.Dequeue()();
                return;
            }
            // Readable long enough: its exit starts now, the next one follows.
            Fx fx = lane.current;
            float minShown = lane.card ? cfg.minCardTime : cfg.minShoutTime;
            if (fx.time >= minShown && fx.time < fx.exitAt) fx.time = fx.exitAt;
        }

        // "CE TOUR VOUS EST OFFERT PAR …": the power picked up, as a sponsor.
        private void Sponsor(int player, PoolPower power)
        {
            Half h = HalfOf(player);
            if (h == null) return;
            Enqueue(h, h.sponsors, () =>
            {
                string kind = power.Type switch { PowerType.Attack => cfg.attackLabel, PowerType.Defense => cfg.defenseLabel, _ => cfg.effectLabel };
                VisualElement card = PowerCard(h, "sponsor", power, cfg.sponsorHeader, power.PowerName + cfg.sponsorNameSuffix, 40, false, Format(cfg.sponsorLineFormat, kind));
                card.style.left = Length.Percent(4);
                // Clear of the hint banner (bottom 15 %) when its text is long.
                card.style.bottom = Length.Percent(23);
                card.style.width = Length.Percent(60);
                return SlideCard(card, Mathf.Max(1f, cfg.sponsorDuration), -1f, 0.45f);
            });
        }

        // "J1 T'ENVOIE ...": the target's card when a power is launched, from
        // the right (the sponsor card comes from the left), under the slot.
        private void Incoming(int player, PoolPower power, string header, string line)
        {
            Half h = HalfOf(player);
            if (h == null) return;
            Enqueue(h, h.incoming, () =>
            {
                VisualElement card = PowerCard(h, "incoming", power, header, power.PowerName, 36, true, line);
                card.style.right = Length.Percent(4);
                card.style.top = Length.Percent(24);
                card.style.width = Length.Percent(52);
                return SlideCard(card, Mathf.Max(1f, cfg.incomingDuration), 1f, 0.4f);
            });
        }

        // A power card: a strip in the power's colour with the header, the
        // big name, a smaller line under it.
        private VisualElement PowerCard(Half h, string name, PoolPower power, string header, string title, float titleSize, bool revealTitle, string line)
        {
            VisualElement card = PopCard(h.fx, name);
            card.style.position = Position.Absolute;
            card.style.overflow = Overflow.Hidden;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = 0;
            card.style.paddingBottom = 10;
            VisualElement head = Box(card, "head");
            head.style.backgroundColor = powerSettings != null ? powerSettings.GetColor(power.Type) : Red;
            head.style.paddingLeft = head.style.paddingRight = 16;
            head.style.paddingTop = head.style.paddingBottom = 5;
            head.style.borderBottomWidth = 3;
            head.style.borderBottomColor = Ink;
            Text(head, header, bodyFont, 16, Color.white).style.unityTextAlign = TextAnchor.MiddleLeft;
            Label titleLabel = Text(card, title, titleFont, titleSize, Ink);
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.marginLeft = 16;
            titleLabel.style.marginTop = 6;
            if (revealTitle) TextFx.Reveal(titleLabel, title);
            if (!string.IsNullOrEmpty(line))
            {
                Label small = Text(card, line, bodyFont, 20, new Color(Ink.r, Ink.g, Ink.b, 0.75f));
                small.style.unityTextAlign = TextAnchor.MiddleLeft;
                small.style.marginLeft = 16;
            }
            return card;
        }

        // Slides in tilted from its side (-1 left, 1 right), stays, slides
        // back out over the last 0.3 s (where a Lane cuts it short).
        private Fx SlideCard(VisualElement card, float life, float side, float inTime)
        {
            Fx fx = AddFx(card, life, (f, u) =>
            {
                float t = u * life, x;
                if (t < inTime) x = 120f * side * (1f - BackOut(t / inTime));
                else if (t > life - 0.3f) x = 130f * side * ((t - (life - 0.3f)) / 0.3f);
                else x = 0f;
                f.element.style.translate = new Translate(Length.Percent(x), 0);
                f.element.style.rotate = new Rotate(new Angle(t < inTime ? 6f * side * (1f - t / inTime) : 0f, AngleUnit.Degree));
            });
            fx.exitAt = life - 0.3f;
            return fx;
        }

        // A quick "no" shake, sideways: leaves scale/rotate to Pop.
        private void Wiggle(VisualElement e, float duration)
        {
            AddFx(e, duration, (fx, u) =>
            {
                float x = u < 1f ? Mathf.Sin(u * Mathf.PI * 6f) * 10f * (1f - u) : 0f;
                fx.element.style.translate = new Translate(x, 0);
            }, removeAtEnd: false);
        }

        private void Pop(VisualElement e, float duration)
        {
            AddFx(e, duration, (fx, u) =>
            {
                float s = u < 0.7f ? Mathf.Lerp(0.2f, 1.25f, u / 0.7f) : Mathf.Lerp(1.25f, 1f, (u - 0.7f) / 0.3f);
                fx.element.style.scale = new Scale(new Vector3(s, s, 1f));
                fx.element.style.rotate = new Rotate(new Angle(u < 0.7f ? Mathf.Lerp(-180f, 10f, u / 0.7f) : Mathf.Lerp(10f, 0f, (u - 0.7f) / 0.3f), AngleUnit.Degree));
            }, removeAtEnd: false);
        }

        private Fx AddFx(VisualElement e, float life, Action<Fx, float> animate, bool removeAtEnd = true)
        {
            var fx = new Fx { element = e, life = life, exitAt = life, animate = animate, keep = !removeAtEnd };
            effects.Add(fx);
            animate(fx, 0f);
            return fx;
        }

        private void UpdateEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Fx fx = effects[i];
                fx.time += dt;
                float u = Mathf.Clamp01(fx.time / fx.life);
                fx.animate(fx, u);
                if (u < 1f) continue;
                effects.RemoveAt(i);
                fx.done = true;
                if (fx.keep)
                {
                    fx.element.style.scale = new Scale(Vector3.one);
                    fx.element.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
                }
                else fx.element.RemoveFromHierarchy();
            }
        }

        private static float BackOut(float k)
        {
            const float c = 1.70158f;
            float x = k - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        // ---------- End of the match ----------

        private void UpdateEnd(PoolMatchRules rules, float dt)
        {
            bool over = rules != null && rules.MatchStarted && rules.GameOver;
            endLayer.style.display = over ? DisplayStyle.Flex : DisplayStyle.None;
            if (!over)
            {
                endShownAt = -1f;
                return;
            }

            float now = Time.unscaledTime;
            if (endShownAt < 0f)
            {
                endShownAt = now;
                int w = Mathf.Max(0, rules.Winner);
                endWhoBox.style.backgroundColor = w == 0 ? P1 : P2;
                TextFx.Set(endWho, Format(cfg.playerTagFormat, w + 1));
                TextFx.Reveal(endTitle, Format(cfg.winnerFormat, w + 1));
                TextFx.Set(endSubtitle, rules.Mode == PoolGameMode.Party && rules.IsSimultaneous ? "Speed Pool + pouvoirs" : ModeName(rules.Mode));
                foreach (Half h in halves) h.plateKey = default;
            }
            float t = now - endShownAt;

            // Card pops in, rays turn, confetti falls once.
            float k = Mathf.Clamp01(t / 0.6f);
            float s = Mathf.Lerp(0.3f, 1f, BackOut(k));
            endCard.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            endCard.style.scale = new Scale(new Vector3(s, s, 1f));
            endCard.style.rotate = new Rotate(new Angle(-6f * (1f - k), AngleUnit.Degree));
            endCard.style.opacity = k;
            endRays.style.rotate = new Rotate(new Angle(t * 12f, AngleUnit.Degree));
            for (int i = 0; i < confetti.Count; i++)
            {
                float ct = Mathf.Clamp01((t - (i % 10) * 0.12f) / 3.2f);
                confetti[i].style.top = Length.Percent(-4f + 110f * ct);
                confetti[i].style.rotate = new Rotate(new Angle(720f * ct + i * 37f, AngleUnit.Degree));
                confetti[i].style.opacity = ct >= 1f ? 0f : 1f;
            }
            float pulse = 1f + 0.04f * Mathf.Sin(t * 5f);
            endButton.style.scale = new Scale(new Vector3(pulse, pulse, 1f));

            if (t > 0.6f && RevanchePressed()) rules.RequestRestart();
        }

        private bool RevanchePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                return true;
            // A pad nobody plays with is someone joining (PlayerDeviceRouter),
            // not a rematch.
            foreach (Gamepad pad in Gamepad.all)
                if ((pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame)
                    && UnityEngine.InputSystem.Users.InputUser.FindUserPairedToDevice(pad) != null) return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && root.panel != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                if (endButton.worldBound.Contains(panelPos)) return true;
            }
            return false;
        }

        private static string ModeName(PoolGameMode mode) => mode switch
        {
            PoolGameMode.NineBall => "9-ball",
            PoolGameMode.FourteenOne => "14.1",
            PoolGameMode.Party => "Party",
            PoolGameMode.SpeedPool => "Speed Pool",
            _ => "8-ball",
        };

        // ---------- Drawing ----------

        private static void DrawPowerIcon(MeshGenerationContext ctx, PowerType type)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 1f) return;
            Painter2D p = ctx.painter2D;
            p.fillColor = Color.white;
            p.BeginPath();
            Vector2 P(float x, float y) => new Vector2(r.x + x * r.width, r.y + y * r.height);
            switch (type)
            {
                case PowerType.Attack:
                    p.MoveTo(P(0.5f, 0.08f)); p.LineTo(P(0.95f, 0.9f)); p.LineTo(P(0.05f, 0.9f));
                    break;
                case PowerType.Defense:
                    p.MoveTo(P(0.5f, 0.04f)); p.LineTo(P(0.92f, 0.2f)); p.LineTo(P(0.85f, 0.6f));
                    p.LineTo(P(0.5f, 0.96f)); p.LineTo(P(0.15f, 0.6f)); p.LineTo(P(0.08f, 0.2f));
                    break;
                default:
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (-90f + i * 36f) * Mathf.Deg2Rad, rad = i % 2 == 0 ? 0.48f : 0.2f;
                        Vector2 v = P(0.5f + Mathf.Cos(a) * rad, 0.52f + Mathf.Sin(a) * rad);
                        if (i == 0) p.MoveTo(v); else p.LineTo(v);
                    }
                    break;
            }
            p.ClosePath();
            p.Fill();
        }

        // Sunburst (prototype repeating-conic-gradient): 18 wedges of 8°.
        private static void DrawRays(MeshGenerationContext ctx, Color color)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 1f) return;
            Painter2D p = ctx.painter2D;
            p.fillColor = color;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            for (int i = 0; i < 18; i++)
            {
                float a0 = i * 20f * Mathf.Deg2Rad, a1 = a0 + 8f * Mathf.Deg2Rad;
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius);
                p.LineTo(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius);
                p.ClosePath();
                p.Fill();
            }
        }

        // ---------- Element helpers ----------

        private static VisualElement Box(VisualElement parent, string name)
        {
            var e = new VisualElement { name = name };
            e.pickingMode = PickingMode.Ignore;
            parent.Add(e);
            return e;
        }

        private static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0;
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

        // The prototype's "pop card": cream, ink outline, a thicker ink edge
        // at the bottom standing in for its hard drop shadow.
        private VisualElement PopCard(VisualElement parent, string name)
        {
            VisualElement card = Box(parent, name);
            card.style.backgroundColor = Cream;
            Radius(card, 14);
            Border(card, 4, Ink);
            card.style.borderBottomWidth = 9;
            return card;
        }

        // The text may carry TextFx tags (<wave>, <shake>…).
        private Label Text(VisualElement parent, string text, Font font, float size, Color color)
        {
            var label = new Label();
            TextFx.Set(label, text);
            label.style.fontSize = size;
            label.style.color = color;
            if (font != null) label.style.unityFontDefinition = FontDefinition.FromFont(font);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);
            return label;
        }

        // A text from the settings with its number / name in place of {0};
        // a mistyped format shows as written rather than breaking the HUD.
        private static string Format(string format, params object[] values)
        {
            if (string.IsNullOrEmpty(format)) return "";
            try { return string.Format(format, values); }
            catch (FormatException) { return format; }
        }

        private void InkShadow(Label label, float drop) =>
            label.style.textShadow = new TextShadow { offset = new Vector2(drop * 0.75f, drop), blurRadius = 0f, color = Ink };

        // A small pool ball with its number (1–15; stripes from 9).
        private VisualElement MiniBall(VisualElement parent, float size, int number)
        {
            Color color = BallColors[Mathf.Clamp((number - 1) % 8, 0, 7)];
            bool stripe = number > 8;
            VisualElement ball = Box(parent, "ball");
            ball.style.width = ball.style.height = size;
            ball.style.flexShrink = 0;
            ball.style.overflow = Overflow.Hidden;
            ball.style.justifyContent = Justify.Center;
            ball.style.alignItems = Align.Center;
            Radius(ball, size / 2f);
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
            VisualElement disc = Box(ball, "disc");
            disc.style.width = disc.style.height = size * 0.5f;
            disc.style.justifyContent = Justify.Center;
            Radius(disc, size * 0.25f);
            disc.style.backgroundColor = BallCream;
            Text(disc, number.ToString(), bodyFont, size * (number > 9 ? 0.22f : 0.27f), Ink);
            return ball;
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
