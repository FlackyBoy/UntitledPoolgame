using UnityEngine;
using UnityEngine.UIElements;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.Core
{
    // A level's picture in the menu card and on the loading postcard: the
    // image set in GameFlowSettings (cropped to fill), or, until there is
    // one, a small drawing (bar, prison, R&D blueprint by position in the
    // list).
    public static class LevelPictures
    {
        public static void Fill(VisualElement pic, LevelEntry level, int index)
        {
            pic.Clear();
            if (level != null && level.picture != null)
            {
                pic.style.backgroundImage = new StyleBackground(level.picture);
                pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                return;
            }
            switch (index % 3)
            {
                case 1: Prison(pic); break;
                case 2: RnD(pic); break;
                default: Bar(pic); break;
            }
        }

        private static void Bar(VisualElement pic)
        {
            pic.style.backgroundImage = K.Radial(K.Hex("#5d3a26"), K.Hex("#1e130c"), 0.5f, 0.6f, 1.1f);
            K.Stretch(pic);
            VisualElement neon1 = K.Disc(pic, 25f, 28f, 70, Color.clear);
            neon1.style.backgroundImage = K.Radial(K.Hex("#ff5fc0"), new Color(1f, 0.37f, 0.75f, 0f), 0.5f, 0.5f, 0.7f); K.Stretch(neon1);
            VisualElement neon2 = K.Disc(pic, 75f, 24f, 60, Color.clear);
            neon2.style.backgroundImage = K.Radial(K.Hex("#ffd23f"), new Color(1f, 0.82f, 0.25f, 0f), 0.5f, 0.5f, 0.7f); K.Stretch(neon2);
            VisualElement table = K.Box(pic, "table");
            K.Rect(table, 22f, 62f, 22f, 12f);
            table.style.backgroundColor = K.Felt;
            K.Radius(table, 8);
            K.Border(table, 8, K.Wood);
        }

        private static void Prison(VisualElement pic)
        {
            pic.style.backgroundImage = K.Radial(K.Hex("#7a8288"), K.Hex("#2b2f33"), 0.5f, 0.9f, 1.2f);
            K.Stretch(pic);
            VisualElement light = K.Disc(pic, 50f, 8f, 180, Color.clear);
            light.style.backgroundImage = K.Radial(new Color(0.55f, 1f, 0.67f, 0.5f), new Color(0.55f, 1f, 0.67f, 0f), 0.5f, 0.5f, 0.8f); K.Stretch(light);
            for (int i = 0; i < 7; i++)
            {
                VisualElement bar = K.Box(pic, "bar");
                bar.style.position = Position.Absolute;
                bar.style.left = Length.Percent(6 + i * 14);
                bar.style.top = 0; bar.style.bottom = 0;
                bar.style.width = 10;
                bar.style.backgroundColor = K.Hex("#23272b");
            }
        }

        private static void RnD(VisualElement pic)
        {
            pic.style.backgroundImage = K.Radial(K.Hex("#2a6f97"), K.Hex("#0f2f4a"), 0.5f, 0.5f, 1.1f);
            K.Stretch(pic);
            Color line = new Color(1f, 1f, 1f, 0.2f);
            for (int i = 1; i < 8; i++)
            {
                VisualElement column = K.Box(pic, "grid");
                column.style.position = Position.Absolute;
                column.style.left = Length.Percent(i * 12.5f);
                column.style.top = 0; column.style.bottom = 0;
                column.style.width = 2;
                column.style.backgroundColor = line;
            }
            for (int i = 1; i < 5; i++)
            {
                VisualElement row = K.Box(pic, "grid");
                row.style.position = Position.Absolute;
                row.style.top = Length.Percent(i * 20f);
                row.style.left = 0; row.style.right = 0;
                row.style.height = 2;
                row.style.backgroundColor = line;
            }
            K.Disc(pic, 28f, 62f, 44, K.Hex("#f4c20d"));
            K.Disc(pic, 52f, 38f, 36, K.P1);
            K.Disc(pic, 74f, 66f, 30, K.P2);
        }
    }
}
