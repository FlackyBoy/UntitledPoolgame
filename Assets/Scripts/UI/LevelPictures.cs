using UnityEngine;
using UnityEngine.UIElements;
using K = UntitledPoolGame.Core.UiKit;

namespace UntitledPoolGame.Core
{
    // A level's picture in the menu card and on the loading postcard: the
    // image set in GameFlowSettings (cropped to fill), or, until there is
    // one, a small drawing (bar, prison, space station by position in the
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
                case 2: Space(pic); break;
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

        private static void Space(VisualElement pic)
        {
            pic.style.backgroundImage = K.Radial(K.Hex("#281450"), K.Hex("#0b1640"), 0.3f, 0.8f, 1.1f);
            K.Stretch(pic);
            foreach (var (x, y) in new[] { (12f, 18f), (34f, 70f), (58f, 12f), (88f, 78f), (8f, 60f), (70f, 50f) })
                K.Disc(pic, x, y, 5, Color.white);
            K.Disc(pic, 74f, 34f, 90, K.Hex("#ffb36b"));
            K.Disc(pic, 22f, 52f, 34, K.Hex("#f4c20d"));
            K.Disc(pic, 38f, 40f, 28, K.P1);
            K.Disc(pic, 52f, 62f, 24, K.P2);
        }
    }
}
