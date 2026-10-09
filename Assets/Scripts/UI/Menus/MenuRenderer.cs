using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UIElements;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.Core
{
    // One built element: holder (placed, centered on its position) → fx
    // (entrance and idle movement, rotation) → visual (what grows when aimed
    // at and flies when shot). text / sub: its labels, if any.
    public class MenuNode
    {
        public MenuElement data;
        public int index;
        public VisualElement holder, fx, visual;
        public Label text, sub;
        public float focus;                  // 0..1, eased by the menu

        public Vector2 Position => data.position;
        public float Radius => data.kind == MenuElementKind.Ball ? data.size.x / 2f : Mathf.Max(data.size.x, 60f) / 2f;
        public bool IsCard => data.kind != MenuElementKind.Ball;
    }

    // A screen built from its MenuScreen: the elements, the cue ball, and the
    // entrance / idle animation of every element (MenuFx below).
    public class MenuView
    {
        public MenuScreen screen;
        public VisualElement root, cueBall;
        public readonly List<MenuNode> items = new List<MenuNode>();
        public float openedAt;
        private IVisualElementScheduledItem tick;

        public MenuNode Item(string id) => items.Find(i => i.data.id == id);

        public IEnumerable<MenuNode> Selectable
        {
            get { foreach (MenuNode i in items) if (i.data.Selectable) yield return i; }
        }

        // Entrances from the start, letters revealed, Feel "on open".
        public void Replay()
        {
            openedAt = Time.realtimeSinceStartup;
            foreach (MenuNode i in items)
            {
                if (!i.data.revealText) continue;
                if (i.text != null && !string.IsNullOrEmpty(i.data.text)) TextFx.Reveal(i.text, i.data.text);
                if (i.sub != null && !string.IsNullOrEmpty(i.data.subText)) TextFx.Reveal(i.sub, i.data.subText);
            }
            MenuFx.Apply(this);
            if (tick == null) tick = root.schedule.Execute(() => MenuFx.Apply(this)).Every(16);
            else tick.Resume();
            MenuFeel.Play(screen.feelOnOpen);
        }

        public void Stop() => tick?.Pause();
    }

    public static class MenuRenderer
    {
        // editor: slots are outlined and named (Menu Studio).
        public static MenuView Build(VisualElement parent, MenuScreen screen, bool editor = false)
        {
            var view = new MenuView { screen = screen };
            view.root = K.Box(parent, screen != null ? screen.name : "screen");
            K.Fill(view.root);
            if (screen == null) return view;

            BuildBackdrop(view.root, screen);
            for (int i = 0; i < screen.elements.Count; i++)
            {
                MenuElement e = screen.elements[i];
                if (e == null || (!e.visible && !editor)) continue;
                MenuNode item = BuildElement(view.root, e, editor);
                item.index = i;
                if (!e.visible) item.holder.style.opacity = 0.35f;   // editor only
                view.items.Add(item);
            }
            if (screen.hasCue)
            {
                VisualElement holder = K.Box(view.root, "cue-ball");
                holder.style.width = holder.style.height = screen.cueSize;
                K.Place(holder, screen.cuePosition.x, screen.cuePosition.y);
                VisualElement shadow = K.Disc(holder, 50f, 62f, screen.cueSize * 0.9f, new Color(0f, 0f, 0f, 0.35f));
                shadow.name = "shadow";
                view.cueBall = K.BallVisual(holder, screen.cueSize, K.BallCream, null, false);
            }
            view.Replay();
            return view;
        }

        // ---------- Backdrop ----------

        private static void BuildBackdrop(VisualElement root, MenuScreen screen)
        {
            switch (screen.backdrop)
            {
                case MenuBackdrop.Color:
                case MenuBackdrop.Dim:
                    VisualElement fill = K.Box(root, "backdrop");
                    K.Fill(fill);
                    fill.style.backgroundColor = screen.backgroundColor;
                    break;
                case MenuBackdrop.Image:
                    VisualElement image = K.Box(root, "backdrop");
                    K.Fill(image);
                    image.style.backgroundColor = screen.backgroundColor;
                    if (screen.backgroundImage != null)
                    {
                        image.style.backgroundImage = new StyleBackground(screen.backgroundImage);
                        image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                    }
                    break;
                case MenuBackdrop.Bar:
                    BuildBar(root, screen.barLights, screen.table);
                    break;
            }
        }

        // The bar at night, its lights, and the pool table lit by its lamp.
        public static void BuildBar(VisualElement parent, bool lights, bool table)
        {
            VisualElement room = K.Box(parent, "room");
            K.Fill(room);
            room.style.backgroundImage = K.Radial(K.Hex("#4d3121"), K.Hex("#140d09"), 0.5f, 0.4f, 1.1f);
            K.Stretch(room);

            if (lights)
            {
                (float x, float y, float r, Color c)[] bokeh =
                {
                    (7, 20, 60, new Color(1f, 0.67f, 0.31f, 0.55f)), (15, 34, 42, new Color(1f, 0.47f, 0.35f, 0.45f)),
                    (24, 16, 48, new Color(0.47f, 0.78f, 1f, 0.35f)), (79, 18, 66, new Color(1f, 0.78f, 0.47f, 0.5f)),
                    (88, 32, 48, new Color(1f, 0.35f, 0.55f, 0.4f)), (94, 12, 42, new Color(0.55f, 1f, 0.78f, 0.3f)),
                    (70, 8, 36, new Color(1f, 0.9f, 0.63f, 0.35f)), (33, 6, 36, new Color(1f, 0.59f, 0.35f, 0.35f)),
                };
                foreach (var (x, y, r, c) in bokeh)
                {
                    VisualElement glow = K.Disc(parent, x, y, r * 2.4f, Color.clear);
                    glow.style.backgroundImage = K.Radial(c, new Color(c.r, c.g, c.b, 0f), 0.5f, 0.5f, 0.8f);
                    K.Stretch(glow);
                }
            }
            if (!table) return;

            VisualElement tableShadow = K.Box(parent, "table-shadow");
            K.Rect(tableShadow, 11f, 18f, 11f, 1f);
            tableShadow.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            K.Radius(tableShadow, 40);

            VisualElement felt = K.Box(parent, "felt");
            K.Rect(felt, 10f, 14f, 10f, 5f);
            felt.style.backgroundImage = K.Radial(K.Felt, K.FeltDark, 0.5f, 0.6f, 1.2f);
            K.Stretch(felt);
            K.Radius(felt, 22);
            K.Border(felt, 30, K.Wood);

            foreach (var (x, y) in new[] { (12.6f, 19f), (50f, 17f), (87.4f, 19f), (12.6f, 90.5f), (50f, 92.5f), (87.4f, 90.5f) })
            {
                VisualElement pocket = K.Disc(parent, x, y, 66, Color.black);
                K.Border(pocket, 6, K.WoodDark);
            }

            VisualElement lampLight = K.Box(parent, "lamplight");
            K.Fill(lampLight);
            lampLight.style.backgroundImage = K.Radial(new Color(1f, 0.89f, 0.63f, 0.2f), new Color(1f, 0.89f, 0.63f, 0f), 0.5f, 0.52f, 0.9f);
            K.Stretch(lampLight);

            VisualElement shade = K.Box(parent, "lampshade");
            shade.style.position = Position.Absolute;
            shade.style.left = Length.Percent(33);
            shade.style.width = Length.Percent(34);
            shade.style.top = 0;
            shade.style.height = Length.Percent(7.5f);
            shade.style.backgroundColor = K.Hex("#174a31");
            shade.style.borderBottomLeftRadius = shade.style.borderBottomRightRadius = 10;
            shade.style.borderBottomWidth = 8;
            shade.style.borderBottomColor = K.Hex("#f7d58a");

            VisualElement vignette = K.Box(parent, "vignette");
            K.Fill(vignette);
            vignette.style.backgroundImage = K.Radial(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.55f), 0.5f, 0.45f, 2.2f);
            K.Stretch(vignette);
        }

        // ---------- Elements ----------

        public static MenuNode BuildElement(VisualElement parent, MenuElement e, bool editor)
        {
            var item = new MenuNode { data = e };
            item.holder = K.Box(parent, e.id);
            K.Place(item.holder, e.position.x, e.position.y);
            bool sized = e.size.x > 0f && e.size.y > 0f;
            if (sized)
            {
                item.holder.style.width = e.size.x;
                item.holder.style.height = e.kind == MenuElementKind.Ball ? e.size.x : e.size.y;
            }
            item.fx = K.Box(item.holder, "fx");
            if (sized) K.Fill(item.fx);
            item.fx.style.alignItems = Align.Center;
            item.fx.style.justifyContent = Justify.Center;

            Font textFont = e.titleFont ? K.TitleFont : K.BodyFont;
            switch (e.kind)
            {
                case MenuElementKind.Ball:
                {
                    float d = Mathf.Max(10f, e.size.x);
                    VisualElement shadow = K.Disc(item.fx, 50f, 62f, d * 0.9f, new Color(0f, 0f, 0f, 0.35f));
                    shadow.name = "shadow";
                    item.visual = K.BallVisual(item.fx, d, e.color, e.number, e.stripe);
                    item.visual.name = "visual";
                    item.visual.style.position = Position.Absolute;
                    VisualElement labels = K.Box(item.holder, "labels");
                    labels.style.position = Position.Absolute;
                    labels.style.top = d + 16f;
                    labels.style.left = Length.Percent(50);
                    labels.style.translate = new Translate(Length.Percent(-50), 0);
                    labels.style.alignItems = Align.Center;
                    AddTexts(item, labels, e, textFont, false);
                    break;
                }
                case MenuElementKind.Card:
                {
                    item.visual = K.PopCard(item.fx, "visual");
                    K.Fill(item.visual);
                    item.visual.style.backgroundColor = e.color;
                    if (e.borderWidth > 0f) K.Border(item.visual, e.borderWidth, e.borderColor);
                    K.Pad(item.visual, 14, 14);
                    item.visual.style.alignItems = Align.Center;
                    item.visual.style.justifyContent = Justify.Center;
                    if (e.image != null)
                    {
                        VisualElement pic = K.Box(item.visual, "picture");
                        pic.style.alignSelf = Align.Stretch;
                        pic.style.height = Mathf.Max(20f, e.size.y * 0.57f);
                        pic.style.overflow = Overflow.Hidden;
                        K.Radius(pic, 10);
                        K.Border(pic, 3, K.Ink);
                        pic.style.backgroundImage = new StyleBackground(e.image);
                        pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                    }
                    AddTexts(item, item.visual, e, textFont, true);
                    break;
                }
                case MenuElementKind.Text:
                {
                    item.visual = K.Box(item.fx, "visual");
                    if (sized) K.Fill(item.visual);
                    item.visual.style.alignItems = Align.Center;
                    item.visual.style.justifyContent = Justify.Center;
                    AddTexts(item, item.visual, e, textFont, sized);
                    break;
                }
                case MenuElementKind.Image:
                {
                    item.visual = K.Box(item.fx, "visual");
                    K.Fill(item.visual);
                    if (e.image != null)
                    {
                        item.visual.style.backgroundImage = new StyleBackground(e.image);
                        item.visual.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                        item.visual.style.unityBackgroundImageTintColor = e.color;
                    }
                    else if (editor) Outline(item.visual, "image");
                    break;
                }
                case MenuElementKind.Panel:
                {
                    item.visual = K.Box(item.fx, "visual");
                    K.Fill(item.visual);
                    item.visual.style.backgroundColor = e.color;
                    K.Radius(item.visual, e.cornerRadius);
                    if (e.borderWidth > 0f) K.Border(item.visual, e.borderWidth, e.borderColor);
                    if (e.image != null)
                    {
                        item.visual.style.backgroundImage = new StyleBackground(e.image);
                        item.visual.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                    }
                    K.Pad(item.visual, 26, 16);
                    item.visual.style.justifyContent = Justify.Center;
                    AddTexts(item, item.visual, e, textFont, true);
                    break;
                }
                case MenuElementKind.Pill:
                {
                    item.visual = K.Box(item.fx, "visual");
                    K.Fill(item.visual);
                    item.visual.style.flexDirection = FlexDirection.Row;
                    item.visual.style.alignItems = Align.Center;
                    item.visual.style.paddingLeft = 10;
                    item.visual.style.paddingRight = 30;
                    K.Radius(item.visual, Mathf.Max(e.cornerRadius, e.size.y / 2f));
                    item.visual.style.backgroundColor = new Color(0f, 0f, 0f, 0.08f);
                    float b = Mathf.Max(20f, e.size.y * 0.75f);
                    VisualElement ball = K.BallVisual(item.visual, b, e.color, e.number, e.stripe);
                    ball.style.marginRight = 18;
                    item.text = K.Text(item.visual, e.text, textFont, e.textSize, e.textColor, false);
                    if (e.textShadow) K.Shadowed(item.text, e.textSize * 0.1f);
                    break;
                }
                default:   // Slot
                {
                    item.visual = K.Box(item.fx, "visual");
                    K.Fill(item.visual);
                    if (editor) Outline(item.visual, "zone remplie par le jeu : " + e.id);
                    break;
                }
            }
            item.fx.style.rotate = new Rotate(new Angle(e.rotation, AngleUnit.Degree));
            return item;
        }

        private static void AddTexts(MenuNode item, VisualElement parent, MenuElement e, Font font, bool wrap)
        {
            // Always there, even empty: the game may fill it (context, status, slot labels…).
            {
                item.text = K.Text(parent, e.text, font, e.textSize, e.textColor, false);
                item.text.name = "text";
                if (wrap) { item.text.style.whiteSpace = WhiteSpace.Normal; item.text.style.alignSelf = Align.Stretch; }
                if (e.textShadow) K.Shadowed(item.text, Mathf.Max(2f, e.textSize * 0.1f));
            }
            if (!string.IsNullOrEmpty(e.subText) || e.kind == MenuElementKind.Panel || e.kind == MenuElementKind.Card)
            {
                Color subColor = e.kind == MenuElementKind.Ball ? new Color(1f, 1f, 1f, 0.8f) : new Color(e.textColor.r, e.textColor.g, e.textColor.b, 0.75f);
                item.sub = K.Text(parent, e.subText, K.BodyFont, e.subTextSize, subColor, false);
                item.sub.name = "sub";
                item.sub.style.marginTop = 4;
                if (wrap) { item.sub.style.whiteSpace = WhiteSpace.Normal; item.sub.style.alignSelf = Align.Stretch; }
                if (e.textShadow && e.kind == MenuElementKind.Ball) K.Shadowed(item.sub, 3f);
            }
        }

        private static void Outline(VisualElement e, string label)
        {
            K.Border(e, 2, new Color(1f, 0.82f, 0.25f, 0.8f));
            e.style.backgroundColor = new Color(1f, 0.82f, 0.25f, 0.08f);
            e.style.justifyContent = Justify.Center;
            Label l = K.Text(e, label, K.BodyFont, 20, new Color(1f, 0.82f, 0.25f), false);
            l.style.whiteSpace = WhiteSpace.Normal;
        }
    }

    // Entrance and idle movement of every element of a view, on its fx
    // layer (the visual inside stays free for the menu's own aiming and
    // shooting animation).
    public static class MenuFx
    {
        public static void Apply(MenuView view)
        {
            float now = Time.realtimeSinceStartup;
            foreach (MenuNode i in view.items)
            {
                MenuElement e = i.data;
                float t = now - view.openedAt - e.entranceDelay;
                float rot = e.rotation, scale = 1f, alpha = 1f;
                Vector2 move = Vector2.zero;

                if (e.entrance != MenuEntrance.None)
                {
                    if (t < 0f) alpha = 0f;
                    else if (t < e.entranceDuration)
                    {
                        float k = t / e.entranceDuration;
                        switch (e.entrance)
                        {
                            case MenuEntrance.Pop: scale = BackOut(k); alpha = Mathf.Clamp01(k * 4f); break;
                            case MenuEntrance.Drop: move.y = -(1f - BounceOut(k)) * 320f; alpha = Mathf.Clamp01(k * 4f); break;
                            case MenuEntrance.Rise: move.y = (1f - CubicOut(k)) * 220f; alpha = k; break;
                            case MenuEntrance.SlideFromLeft: move.x = -(1f - CubicOut(k)) * 800f; break;
                            case MenuEntrance.SlideFromRight: move.x = (1f - CubicOut(k)) * 800f; break;
                            case MenuEntrance.Fade: alpha = k; break;
                            case MenuEntrance.Spin: rot -= (1f - CubicOut(k)) * 360f; scale = CubicOut(k); break;
                        }
                    }
                }

                float phase = i.index * 0.7f, sp = e.idleSpeed, amt = e.idleAmount;
                switch (e.idle)
                {
                    case MenuIdle.Float: move.y += Mathf.Sin(now * 2f * sp + phase) * 8f * amt; break;
                    case MenuIdle.Pulse: scale *= 1f + Mathf.Sin(now * 3f * sp + phase) * 0.04f * amt; break;
                    case MenuIdle.Swing: rot += Mathf.Sin(now * 2f * sp + phase) * 5f * amt; break;
                    case MenuIdle.Wobble:
                        rot += Mathf.Sin(now * 6f * sp + phase) * 2f * amt;
                        scale *= 1f + Mathf.Sin(now * 5f * sp + phase) * 0.02f * amt;
                        break;
                    case MenuIdle.Spin: rot += now * 90f * sp * amt; break;
                }

                i.fx.style.translate = new Translate(move.x, move.y);
                i.fx.style.scale = new Scale(new Vector3(scale, scale, 1f));
                i.fx.style.rotate = new Rotate(new Angle(rot, AngleUnit.Degree));
                i.fx.style.opacity = alpha;
            }
        }

        private static float CubicOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        private static float BackOut(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private static float BounceOut(float k)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (k < 1f / d1) return n1 * k * k;
            if (k < 2f / d1) { k -= 1.5f / d1; return n1 * k * k + 0.75f; }
            if (k < 2.5f / d1) { k -= 2.25f / d1; return n1 * k * k + 0.9375f; }
            k -= 2.625f / d1;
            return n1 * k * k + 0.984375f;
        }
    }

    // Feel sequences of the menus (on open, on focus, on press): each prefab
    // instantiated once, kept across scenes. Play mode only.
    public static class MenuFeel
    {
        private static readonly Dictionary<MMF_Player, MMF_Player> instances = new Dictionary<MMF_Player, MMF_Player>();
        private static GameObject holder;

        public static void Play(MMF_Player prefab)
        {
            if (prefab == null || !Application.isPlaying) return;
            if (holder == null)
            {
                holder = new GameObject("Menu feel (Feel)");
                Object.DontDestroyOnLoad(holder);
                instances.Clear();
            }
            if (!instances.TryGetValue(prefab, out MMF_Player p) || p == null)
            {
                p = Object.Instantiate(prefab, holder.transform);
                p.name = prefab.name;
                instances[prefab] = p;
            }
            p.PlayFeedbacks();
        }
    }

    // The screens: Resources/Menus/<id>, or the built-in default layout.
    public static class MenuLayouts
    {
        public const string Title = "Title", NewGame = "NewGame", Load = "Load", Level = "Level", Mode = "Mode",
            Multiplayer = "Multiplayer", Lobby = "Lobby", Settings = "Settings", Pause = "Pause",
            PauseConfirm = "PauseConfirm", Loading = "Loading";

        public static readonly string[] All =
            { Title, NewGame, Load, Level, Mode, Multiplayer, Lobby, Settings, Pause, PauseConfirm, Loading };

        private static readonly Dictionary<string, MenuScreen> fallbacks = new Dictionary<string, MenuScreen>();

        public static MenuScreen Get(string id)
        {
            MenuScreen s = Resources.Load<MenuScreen>("Menus/" + id);
            if (s != null) return s;
            if (!fallbacks.TryGetValue(id, out s) || s == null)
            {
                s = MenuDefaults.Create(id);
                s.hideFlags = HideFlags.DontSave;
                fallbacks[id] = s;
            }
            return s;
        }
    }
}
