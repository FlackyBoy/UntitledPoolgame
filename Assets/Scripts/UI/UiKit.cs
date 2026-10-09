using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UntitledPoolGame.Core
{
    // Shared building blocks of the "Synthèse" UI (menus, pause, key
    // bindings, loading screen), built in code with UI Toolkit: palette,
    // fonts, panel settings and small element helpers. Palette and fonts come
    // from MenuSettings (Resources), so the designer can change them without
    // code. MatchHud keeps its own copies (its palette comes from HudSettings).
    public static class UiKit
    {
        // ---------- Palette (MenuSettings) ----------
        private static MenuSettings S => MenuSettings.Instance;
        public static Color Felt => S.felt;
        public static Color FeltDark => S.feltDark;
        public static Color Wood => S.wood;
        public static Color WoodDark => S.woodDark;
        public static Color Chalk => S.chalk;
        public static Color ChalkShadow => S.chalkShadow;
        public static Color BallCream => S.ballCream;
        public static Color Cube => S.cube;
        public static Color CubeDark => S.cubeDark;
        public static Color P1 => S.player1;
        public static Color P2 => S.player2;
        public static Color Ink => S.ink;
        public static Color Card => S.card;
        public static Color Yellow => S.yellow;
        public static Color Red => S.red;

        private static Font bodyFont, titleFont;
        public static Font BodyFont => S.bodyFont != null ? S.bodyFont
            : bodyFont != null ? bodyFont : bodyFont = Resources.Load<Font>("UI/Fonts/TitanOne-Regular");
        public static Font TitleFont => S.titleFont != null ? S.titleFont
            : titleFont != null ? titleFont : titleFont = Resources.Load<Font>("UI/Fonts/BowlbyOne-Regular");

        // Full screen, scaled from 1920 × 1080.
        public static PanelSettings CreatePanel(int sortingOrder)
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

        public static VisualElement Box(VisualElement parent, string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            parent.Add(e);
            return e;
        }

        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0;
        }

        public static void Rect(VisualElement e, float left, float top, float right, float bottom)
        {
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(left);
            e.style.top = Length.Percent(top);
            e.style.right = Length.Percent(right);
            e.style.bottom = Length.Percent(bottom);
        }

        // Centered on (x %, y %) of the parent.
        public static void Place(VisualElement e, float x, float y)
        {
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(x);
            e.style.top = Length.Percent(y);
            e.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        }

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = color;
        }

        public static void Pad(VisualElement e, float horizontal, float vertical)
        {
            e.style.paddingLeft = e.style.paddingRight = horizontal;
            e.style.paddingTop = e.style.paddingBottom = vertical;
        }

        public static void Stretch(VisualElement e) =>
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));

        public static VisualElement Disc(VisualElement parent, float x, float y, float size, Color color)
        {
            VisualElement d = Box(parent, "disc");
            d.style.width = d.style.height = size;
            Radius(d, size / 2f);
            d.style.backgroundColor = color;
            Place(d, x, y);
            return d;
        }

        // A label; centered = wrapped in a holder to be placed with Place().
        // The text may carry TextFx tags (<wave>, <shake>…).
        public static Label Text(VisualElement parent, string text, Font font, float size, Color color, bool centered = true)
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
            if (!centered)
            {
                parent.Add(label);
                return label;
            }
            VisualElement holder = Box(parent, "text");
            holder.Add(label);
            return label;
        }

        public static Label Shadowed(Label label, float drop = 5f)
        {
            label.style.textShadow = new TextShadow { offset = new Vector2(0f, drop), blurRadius = 0f, color = ChalkShadow };
            return label;
        }

        // The prototype's "pop card": cream, ink outline, a thicker ink edge
        // at the bottom standing in for its hard drop shadow.
        public static VisualElement PopCard(VisualElement parent, string name)
        {
            VisualElement card = Box(parent, name);
            card.style.backgroundColor = Card;
            Radius(card, 16);
            Border(card, 4, Ink);
            card.style.borderBottomWidth = 10;
            return card;
        }

        // A pool ball (number on a cream disc, stripe band for 9–15).
        public static VisualElement BallVisual(VisualElement parent, float size, Color color, string number, bool stripe)
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
                Label n = Text(disc, number, BodyFont, size * (number.Length > 1 ? 0.19f : 0.23f), Ink, false);
                n.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            Disc(ball, 31f, 25f, size * 0.15f, new Color(1f, 1f, 1f, 0.9f));
            return ball;
        }

        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        // A radial gradient (CSS radial-gradient stand-in): inner at (cx, cy)
        // (texture space, y up), outer at the corners; power shapes the falloff.
        public static StyleBackground Radial(Color inner, Color outer, float cx, float cy, float power)
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

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
