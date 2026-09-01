#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UntitledPoolGame.PoolEditor
{
    // Bakes a simple numbered billiard-ball texture (solid fill or a white
    // stripe band, plus a number badge on two opposite sides) so players can
    // tell balls apart — needed for 9-ball/14.1 order, not just 8-ball groups,
    // where the old flat-color-only material made e.g. ball 1 and ball 9
    // (which share the same base color, like real ball sets) indistinguishable.
    // Editor-only: written once to a PNG asset by PoolTableBuilder, not
    // regenerated at runtime.
    internal static class PoolBallTextureGenerator
    {
        private const int Width = 256;
        private const int Height = 128;

        // Classic 3x5 pixel digit font, rows top-to-bottom.
        private static readonly bool[][,] Digits =
        {
            Bitmap("111,101,101,101,111"), // 0
            Bitmap("010,110,010,010,111"), // 1
            Bitmap("111,001,111,100,111"), // 2
            Bitmap("111,001,111,001,111"), // 3
            Bitmap("101,101,111,001,001"), // 4
            Bitmap("111,100,111,001,111"), // 5
            Bitmap("111,100,111,101,111"), // 6
            Bitmap("111,001,001,001,001"), // 7
            Bitmap("111,101,111,101,111"), // 8
            Bitmap("111,101,111,001,111"), // 9
        };

        public static Texture2D GetOrCreate(string ballName, int number, Color baseColor, bool isCueBall)
        {
            string folder = "Assets/Textures/Balls";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Textures"))
                    AssetDatabase.CreateFolder("Assets", "Textures");
                AssetDatabase.CreateFolder("Assets/Textures", "Balls");
            }

            string path = $"{folder}/{ballName}.png";
            Texture2D tex = Generate(number, baseColor, isCueBall);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Generate(int number, Color baseColor, bool isCueBall)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            bool striped = !isCueBall && number > 8;

            for (int y = 0; y < Height; y++)
            {
                float v = y / (float)(Height - 1);
                bool inStripeBand = v > 0.28f && v < 0.72f;
                Color fill = isCueBall ? Color.white
                    : striped ? (inStripeBand ? baseColor : Color.white)
                    : baseColor;

                for (int x = 0; x < Width; x++)
                    tex.SetPixel(x, y, fill);
            }

            if (!isCueBall)
            {
                DrawBadge(tex, number, 0.25f);
                DrawBadge(tex, number, 0.75f);
            }

            tex.Apply();
            return tex;
        }

        private static void DrawBadge(Texture2D tex, int number, float uCenter)
        {
            int cx = Mathf.RoundToInt(uCenter * Width);
            int cy = Height / 2;

            // Glyph size is fixed relative to the texture, not to the circle —
            // the circle is then sized to comfortably fit whatever we're about
            // to draw (1 or 2 digits), so a two-digit number (10-15) never
            // risks poking outside its badge the way a shared fixed radius would.
            string digits = number.ToString();
            const int digitW = 3, digitH = 5;
            int scale = Mathf.Max(1, Height / 32);
            int glyphGap = scale;
            int totalW = digits.Length * digitW * scale + (digits.Length - 1) * glyphGap;
            int totalH = digitH * scale;
            int radius = Mathf.CeilToInt(Mathf.Sqrt(totalW * totalW + totalH * totalH) / 2f) + scale * 2;

            for (int y = -radius; y <= radius; y++)
            {
                int py = cy + y;
                if (py < 0 || py >= Height) continue;
                int span = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, radius * radius - y * y)));
                for (int x = -span; x <= span; x++)
                    tex.SetPixel(Wrap(cx + x), py, Color.white);
            }

            int startX = cx - totalW / 2;
            int startY = cy - totalH / 2;

            for (int i = 0; i < digits.Length; i++)
            {
                bool[,] bitmap = Digits[digits[i] - '0'];
                int glyphX = startX + i * (digitW * scale + glyphGap);

                for (int row = 0; row < digitH; row++)
                {
                    for (int col = 0; col < digitW; col++)
                    {
                        if (!bitmap[row, col]) continue;
                        int py = startY + (digitH - 1 - row) * scale;
                        int px = glyphX + col * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            int y = py + sy;
                            if (y < 0 || y >= Height) continue;
                            for (int sx = 0; sx < scale; sx++)
                                tex.SetPixel(Wrap(px + sx), y, Color.black);
                        }
                    }
                }
            }
        }

        private static int Wrap(int x) => ((x % Width) + Width) % Width;

        private static bool[,] Bitmap(string rows)
        {
            string[] rowStrings = rows.Split(',');
            var bitmap = new bool[rowStrings.Length, rowStrings[0].Length];
            for (int row = 0; row < rowStrings.Length; row++)
                for (int col = 0; col < rowStrings[row].Length; col++)
                    bitmap[row, col] = rowStrings[row][col] == '1';
            return bitmap;
        }
    }
}
#endif
