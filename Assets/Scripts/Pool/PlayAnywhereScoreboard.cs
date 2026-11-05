using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: the score card shown between two courses and at the end
    // of the match (PlayAnywhereRuleSet), over both halves of the screen. Laid
    // out like a golf card, in the HUD's "billard pop" style: one column per
    // course, the ones to come left empty; a score under par is circled, over
    // par squared. The course just played and the totals stamp in one by one.
    public class PlayAnywhereScoreboard : MonoBehaviour
    {
        public struct Row
        {
            public int course, par, player1, player2;
        }

        private const float NameWidth = 170f, TotalWidth = 170f, MaxCell = 96f, MaxCoursesWidth = 1200f;
        private const float RowHeight = 92f;
        // Right of the totals, for the sticker: not over the number.
        private const float StickerRoom = 124f;
        private const float PopTime = 0.28f, StampTime = 0.18f;
        // Over the HUD (90), under the menus (100).
        private const int PanelOrder = 95;

        private static PlayAnywhereScoreboard instance;

        private HudSettings cfg;
        private Font bodyFont, titleFont;
        private VisualElement root, card, timerFill;
        // What's still to stamp in, and when (seconds after Show).
        private readonly List<(VisualElement element, float at)> stamps = new List<(VisualElement, float)>();
        private float shownAt, duration;

        // course: every course of the match, for the columns still to play.
        // winner: the player who won the match, -1 before its end. seconds:
        // how long it stays up, for the bar at the bottom.
        public static void Show(string title, IReadOnlyList<Row> rows, IReadOnlyList<PlayAnywhereHole> course, string footer, int winner, float seconds)
        {
            if (instance == null)
            {
                var go = new GameObject("Play Anywhere - Tableau des scores");
                go.SetActive(false);
                go.AddComponent<UIDocument>().panelSettings = MatchHud.CreatePanel(PanelOrder);
                instance = go.AddComponent<PlayAnywhereScoreboard>();
                go.SetActive(true);
            }
            instance.Build(title, rows, course, footer, winner);
            instance.shownAt = Time.unscaledTime;
            instance.duration = seconds;
            instance.enabled = true;
            instance.Update();
        }

        public static void Hide()
        {
            if (instance == null) return;
            instance.enabled = false;
            if (instance.root != null) instance.root.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---------- Building ----------

        private void Build(string title, IReadOnlyList<Row> rows, IReadOnlyList<PlayAnywhereHole> course, string footer, int winner)
        {
            cfg = PoolSettingsLoader.LoadOrDefault<HudSettings>("HudSettings");
            bodyFont = cfg.bodyFont != null ? cfg.bodyFont : Resources.Load<Font>("UI/Fonts/TitanOne-Regular");
            titleFont = cfg.titleFont != null ? cfg.titleFont : Resources.Load<Font>("UI/Fonts/BowlbyOne-Regular");

            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            stamps.Clear();
            root.style.display = DisplayStyle.Flex;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = new Color(0.03f, 0.02f, 0.01f, 0.6f);
            root.pickingMode = PickingMode.Ignore;

            card = Box(root);
            card.style.backgroundColor = cfg.cardColor;
            card.style.overflow = Overflow.Hidden;
            Radius(card, 22);
            Border(card, 5, cfg.inkColor);
            card.style.borderBottomWidth = 13;

            VisualElement head = Box(card);
            head.style.backgroundColor = cfg.highlightColor;
            head.style.paddingTop = 14;
            head.style.paddingBottom = 16;
            head.style.paddingLeft = head.style.paddingRight = 40;
            head.style.borderBottomWidth = 5;
            head.style.borderBottomColor = cfg.inkColor;
            Text(head, "Carte de score", bodyFont, 24, Faded(0.7f));
            Text(head, title, titleFont, 54, cfg.inkColor);

            int count = Mathf.Max(course.Count, rows.Count);
            float cell = Mathf.Min(MaxCell, MaxCoursesWidth / Mathf.Max(1, count));
            int total1 = 0, total2 = 0, totalPar = 0;
            foreach (Row r in rows)
            {
                total1 += r.player1;
                total2 += r.player2;
                totalPar += r.par;
            }
            int leader = winner >= 0 ? winner : total1 < total2 ? 0 : total2 < total1 ? 1 : -1;

            VisualElement table = Box(card);
            table.style.paddingLeft = table.style.paddingRight = 28;
            table.style.paddingTop = 14;
            table.style.paddingBottom = 6;

            // Course numbers, the one just played in a disc.
            VisualElement numbers = RowBox(table, Color.clear, 56f);
            Text(numbers, "Parcours", bodyFont, 24, Faded(0.65f)).style.width = NameWidth;
            for (int i = 0; i < count; i++)
            {
                VisualElement c = Cell(numbers, cell, 56f, i, rows.Count);
                bool justPlayed = i == rows.Count - 1;
                VisualElement disc = Box(c);
                disc.style.width = disc.style.height = 44;
                disc.style.justifyContent = Justify.Center;
                Radius(disc, 22);
                if (justPlayed) disc.style.backgroundColor = cfg.inkColor;
                Text(disc, $"{i + 1}", titleFont, 24, justPlayed ? cfg.highlightColor : i < rows.Count ? cfg.inkColor : Faded(0.35f));
            }
            Text(Total(numbers), "Total", bodyFont, 24, Faded(0.65f));

            // Par. Those to come are known too; the total counts those played.
            VisualElement pars = RowBox(table, Color.clear, 56f);
            Text(pars, "Par", bodyFont, 26, Faded(0.65f)).style.width = NameWidth;
            for (int i = 0; i < count; i++)
            {
                int par = i < rows.Count ? rows[i].par : i < course.Count ? course[i].Par : 0;
                Text(Cell(pars, cell, 56f, i, rows.Count), par > 0 ? $"{par}" : "", bodyFont, 28, i < rows.Count ? Faded(0.65f) : Faded(0.35f));
            }
            Text(Total(pars), $"{totalPar}", bodyFont, 28, Faded(0.65f));

            PlayerRow(table, 0, cfg.player1Color, rows, count, cell, total1, totalPar, leader, winner);
            PlayerRow(table, 1, cfg.player2Color, rows, count, cell, total2, totalPar, leader, winner);

            if (!string.IsNullOrEmpty(footer))
            {
                VisualElement pill = Box(card);
                pill.style.alignSelf = Align.Center;
                pill.style.marginTop = 12;
                pill.style.marginBottom = 18;
                pill.style.paddingLeft = pill.style.paddingRight = 28;
                pill.style.paddingTop = pill.style.paddingBottom = 9;
                pill.style.maxWidth = 1200;
                pill.style.backgroundColor = winner == 0 ? cfg.player1Color : winner == 1 ? cfg.player2Color : Color.clear;
                Radius(pill, 16);
                if (winner >= 0) Border(pill, 4, cfg.inkColor);
                Label text = Text(pill, footer, titleFont, winner >= 0 ? 34 : 28, winner >= 0 ? Color.white : cfg.inkColor);
                text.style.whiteSpace = WhiteSpace.Normal;
                stamps.Add((pill, 1.45f));
            }

            VisualElement timer = Box(card);
            timer.style.height = 10;
            timer.style.backgroundColor = Faded(0.15f);
            timerFill = Box(timer);
            timerFill.style.height = Length.Percent(100);
            timerFill.style.backgroundColor = cfg.inkColor;
            timerFill.style.transformOrigin = new TransformOrigin(0, 0);
        }

        private void PlayerRow(VisualElement table, int player, Color colour, IReadOnlyList<Row> rows, int count, float cell, int total, int totalPar, int leader, int winner)
        {
            bool won = winner == player;
            VisualElement row = RowBox(table, new Color(colour.r, colour.g, colour.b, won ? 0.32f : 0.2f), RowHeight);
            row.style.marginTop = 6;
            Radius(row, 14);

            VisualElement name = Box(row);
            name.style.width = NameWidth;
            name.style.alignItems = Align.Center;
            name.style.justifyContent = Justify.Center;
            VisualElement badge = Box(name);
            badge.style.width = 84;
            badge.style.height = 66;
            badge.style.justifyContent = Justify.Center;
            badge.style.backgroundColor = colour;
            Radius(badge, 16);
            Border(badge, 4, cfg.inkColor);
            Text(badge, $"J{player + 1}", titleFont, 32, Color.white);

            for (int i = 0; i < count; i++)
            {
                VisualElement c = Cell(row, cell, RowHeight, i, rows.Count);
                if (i >= rows.Count)
                {
                    Text(c, "·", titleFont, 32, Faded(0.3f));
                    continue;
                }
                int strokes = player == 0 ? rows[i].player1 : rows[i].player2;
                VisualElement mark = Mark(c, strokes, strokes - rows[i].par, cell);
                // The course just played comes in player after player.
                if (i == rows.Count - 1) stamps.Add((mark, 0.4f + 0.25f * player));
            }

            VisualElement totals = Total(row);
            totals.style.flexDirection = FlexDirection.Column;
            Text(totals, $"{total}", titleFont, 46, cfg.inkColor);
            int diff = total - totalPar;
            Text(totals, diff == 0 ? "par" : diff > 0 ? $"+{diff}" : $"{diff}", bodyFont, 22, Faded(0.65f));
            stamps.Add((totals, 0.9f + 0.15f * player));

            if (leader != player) return;
            VisualElement sticker = Box(row);
            sticker.style.position = Position.Absolute;
            sticker.style.right = 8;
            sticker.style.top = 26;
            sticker.style.rotate = new Rotate(new Angle(8f));
            sticker.style.paddingLeft = sticker.style.paddingRight = 12;
            sticker.style.paddingTop = sticker.style.paddingBottom = 3;
            sticker.style.backgroundColor = cfg.highlightColor;
            Radius(sticker, 10);
            Border(sticker, 3, cfg.inkColor);
            Text(sticker, won ? "Gagne !" : "En tête", bodyFont, 20, cfg.inkColor);
            stamps.Add((sticker, 1.25f));
        }

        // Golf's marks: circled under par, squared over it, red from two over.
        private VisualElement Mark(VisualElement parent, int strokes, int diff, float cell)
        {
            float size = Mathf.Min(66f, cell - 10f);
            VisualElement mark = Box(parent);
            mark.style.width = mark.style.height = size;
            mark.style.justifyContent = Justify.Center;
            if (diff < 0)
            {
                mark.style.backgroundColor = cfg.highlightColor;
                Radius(mark, size / 2f);
                Border(mark, 4, cfg.inkColor);
            }
            else if (diff > 0)
            {
                if (diff > 1) mark.style.backgroundColor = cfg.dangerColor;
                Radius(mark, 6);
                Border(mark, 4, cfg.inkColor);
            }
            Text(mark, $"{strokes}", titleFont, Mathf.Min(36f, size * 0.55f), diff > 1 ? Color.white : cfg.inkColor);
            return mark;
        }

        // ---------- Frame ----------

        // Unscaled: it plays through a slow motion or a pause.
        private void Update()
        {
            if (root == null || card == null) return;
            float t = Time.unscaledTime - shownAt;

            // Pops up, over its size and back.
            float pop = Mathf.Clamp01(t / PopTime);
            float scale = pop < 1f ? Mathf.Lerp(0.7f, 1f, pop) + Mathf.Sin(pop * Mathf.PI) * 0.12f : 1f;
            card.style.scale = new Scale(new Vector3(scale, scale, 1f));

            for (int i = stamps.Count - 1; i >= 0; i--)
            {
                (VisualElement element, float at) = stamps[i];
                float s = (t - at) / StampTime;
                element.style.opacity = s < 0f ? 0f : 1f;
                float size = Mathf.Lerp(1.8f, 1f, Mathf.Clamp01(s));
                element.style.scale = new Scale(new Vector3(size, size, 1f));
                if (s >= 1f) stamps.RemoveAt(i);
            }

            if (timerFill != null)
            {
                float left = duration > 0f ? Mathf.Clamp01(1f - t / duration) : 0f;
                timerFill.style.scale = new Scale(new Vector3(left, 1f, 1f));
            }
        }

        // ---------- Pieces ----------

        private Color Faded(float alpha) => new Color(cfg.inkColor.r, cfg.inkColor.g, cfg.inkColor.b, alpha);

        private static VisualElement RowBox(VisualElement parent, Color colour, float height)
        {
            VisualElement row = Box(parent);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            row.style.height = height;
            row.style.backgroundColor = colour;
            return row;
        }

        // A course's column: just played, it's lightly marked.
        private VisualElement Cell(VisualElement row, float width, float height, int index, int played)
        {
            VisualElement c = Box(row);
            c.style.width = width;
            c.style.height = height;
            c.style.alignItems = Align.Center;
            c.style.justifyContent = Justify.Center;
            if (index == played - 1) c.style.backgroundColor = new Color(cfg.highlightColor.r, cfg.highlightColor.g, cfg.highlightColor.b, 0.35f);
            return c;
        }

        private static VisualElement Total(VisualElement row)
        {
            VisualElement c = Box(row);
            c.style.width = TotalWidth;
            c.style.marginRight = StickerRoom;
            c.style.alignItems = Align.Center;
            c.style.justifyContent = Justify.Center;
            return c;
        }

        private static VisualElement Box(VisualElement parent)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            parent.Add(box);
            return box;
        }

        private static Label Text(VisualElement parent, string text, Font font, float size, Color colour)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = colour;
            if (font != null) label.style.unityFontDefinition = FontDefinition.FromFont(font);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            parent.Add(label);
            return label;
        }

        private static void Radius(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        private static void Border(VisualElement e, float width, Color colour)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = colour;
        }
    }
}
