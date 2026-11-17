using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Drawn shapes and the element helpers the other parts build with.
    public partial class MatchHud
    {
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
