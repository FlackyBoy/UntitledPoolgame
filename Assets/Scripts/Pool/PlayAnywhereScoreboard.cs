using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: the score card shown between two courses and at the end
    // of the match (PlayAnywhereRuleSet). One row per course played (par,
    // each player's strokes), the totals, and a line under it. A plain
    // prototype panel in the middle of the screen, over both halves.
    public class PlayAnywhereScoreboard : MonoBehaviour
    {
        public struct Row
        {
            public int course, par, player1, player2;
        }

        private static PlayAnywhereScoreboard instance;

        private string title;
        private string footer;
        // The table's text, made once in Show (OnGUI runs several times a
        // frame): one entry per row, the last one the totals.
        private readonly List<string[]> lines = new List<string[]>();

        private GUIStyle panelStyle, titleStyle, cellStyle, headStyle, footerStyle;

        public static void Show(string title, IReadOnlyList<Row> rows, string footer)
        {
            if (instance == null)
            {
                var go = new GameObject("Play Anywhere - Tableau des scores");
                instance = go.AddComponent<PlayAnywhereScoreboard>();
            }
            instance.title = title;
            instance.footer = footer;
            instance.lines.Clear();
            int total1 = 0, total2 = 0, totalPar = 0;
            foreach (Row r in rows)
            {
                instance.lines.Add(new[] { $"{r.course}", $"{r.par}", Score(r.player1, r.par), Score(r.player2, r.par) });
                total1 += r.player1;
                total2 += r.player2;
                totalPar += r.par;
            }
            instance.lines.Add(new[] { "Total", $"{totalPar}", Score(total1, totalPar), Score(total2, totalPar) });
            instance.enabled = true;
        }

        public static void Hide()
        {
            if (instance != null) instance.enabled = false;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnGUI()
        {
            EnsureStyles();
            const float rowHeight = 34f, width = 520f;
            float height = 110f + rowHeight * (lines.Count + 1) + (string.IsNullOrEmpty(footer) ? 0f : 50f);
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(area, GUIContent.none, panelStyle);

            GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 16f, width - 48f, height - 32f));
            GUILayout.Label(title, titleStyle);
            GUILayout.Space(8f);
            Line("Parcours", "Par", "J1 (bleu)", "J2 (rouge)", headStyle, rowHeight);
            for (int i = 0; i < lines.Count; i++)
            {
                string[] l = lines[i];
                Line(l[0], l[1], l[2], l[3], i == lines.Count - 1 ? headStyle : cellStyle, rowHeight);
            }
            if (!string.IsNullOrEmpty(footer))
            {
                GUILayout.Space(10f);
                GUILayout.Label(footer, footerStyle);
            }
            GUILayout.EndArea();
        }

        // Strokes, and how far from par (golf style: -1, +2...).
        private static string Score(int strokes, int par)
        {
            int diff = strokes - par;
            return diff == 0 ? $"{strokes}  (par)" : $"{strokes}  ({(diff > 0 ? "+" : "")}{diff})";
        }

        private static void Line(string a, string b, string c, string d, GUIStyle style, float height)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(height));
            GUILayout.Label(a, style, GUILayout.Width(110f));
            GUILayout.Label(b, style, GUILayout.Width(70f));
            GUILayout.Label(c, style, GUILayout.Width(140f));
            GUILayout.Label(d, style, GUILayout.Width(140f));
            GUILayout.EndHorizontal();
        }

        // Made once (OnGUI runs several times a frame).
        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.08f, 0.06f, 0.12f, 0.92f));
            background.Apply();
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = background } };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
            headStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            cellStyle = new GUIStyle(headStyle) { fontStyle = FontStyle.Normal, normal = { textColor = new Color(0.85f, 0.85f, 0.9f) } };
            footerStyle = new GUIStyle(titleStyle) { fontSize = 22 };
        }
    }
}
