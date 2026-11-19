using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Speed Pool at three or four: solids and stripes aren't enough to tell
    // the groups apart, so each owner's balls get their own pattern, the way
    // stripes have a band: plain, band, cross, square or triangle, in the
    // ball's colour on white, around each number. The ball keeps its colour
    // and its numbers (copied from its own texture).
    // The ball textures are equirectangular (PoolBallTextureGenerator, on
    // Unity's sphere): u goes round the equator, v from pole to pole, the
    // numbers sit at u = 0.25 and 0.75 on the equator.
    // Taken off before every match (SpeedPoolRuleSet.FitToPlayers).
    public static class BallOwnerMarker
    {
        public enum Pattern { Solid, Stripe, Cross, Square, Triangle }

        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        // Covers PoolBallTextureGenerator's widest number disc (two digits:
        // 26 px on a 128 px high texture), kept from the original.
        private const float NumberDisc = 0.21f;

        // Repainted textures, made once per ball texture and pattern.
        private static readonly Dictionary<(Texture, Pattern), Texture2D> painted = new Dictionary<(Texture, Pattern), Texture2D>();
        // Each repainted ball's material and its own texture, to give back.
        private static readonly List<(Material material, Texture original)> marked = new List<(Material, Texture)>();

        public static void Mark(PoolBall ball, Pattern pattern)
        {
            Renderer meshRenderer = ball.GetComponentInChildren<Renderer>();
            if (meshRenderer == null) return;
            Material material = meshRenderer.material;
            Texture original = material.mainTexture;
            if (original == null) return;
            var key = (original, pattern);
            if (!painted.TryGetValue(key, out Texture2D texture) || texture == null)
            {
                texture = Paint(original, pattern);
                painted[key] = texture;
            }
            material.mainTexture = texture;
            marked.Add((material, original));
        }

        // Every ball back to its own texture, at once: the next match's
        // copies (second rack) are made from them this same frame.
        public static void RemoveAll()
        {
            foreach ((Material material, Texture original) in marked)
                if (material != null) material.mainTexture = original;
            marked.Clear();
        }

        private static Texture2D Paint(Texture source, Pattern pattern)
        {
            int width = source.width, height = source.height;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"{source.name} ({pattern})",
                wrapMode = TextureWrapMode.Repeat,
            };
            // Through the GPU: the ball textures aren't readable.
            RenderTexture copy = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, copy);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = copy;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            Color32[] pixels = texture.GetPixels32();

            // On the equator between the numbers: the ball's colour, solid or striped.
            Color32 colour = pixels[height / 2 * width];
            float disc = height * NumberDisc;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Pixels from the nearest number's centre (u = 0.25 or 0.75).
                    float dx = Mathf.Repeat(x, width * 0.5f) - width * 0.25f;
                    float dy = y - height * 0.5f;
                    if (dx * dx + dy * dy <= disc * disc) continue;
                    pixels[y * width + x] = Inside(pattern, dx / height, dy / height, y / (float)(height - 1)) ? colour : White;
                }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // In units of the texture's height, from the number's centre (v up);
        // v01 = 0 at one pole, 1 at the other.
        private static bool Inside(Pattern pattern, float u, float v, float v01)
        {
            switch (pattern)
            {
                case Pattern.Solid:
                    return true;
                case Pattern.Stripe:
                    // PoolBallTextureGenerator's band.
                    return v01 > 0.28f && v01 < 0.72f;
                case Pattern.Cross:
                    // An X: two diagonal bars, the number in the middle.
                    return Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) < 0.4f
                        && Mathf.Min(Mathf.Abs(u - v), Mathf.Abs(u + v)) * 0.70710678f < 0.12f;
                case Pattern.Square:
                    return Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) < 0.33f;
                case Pattern.Triangle:
                {
                    // Pointing up, centred a little low so its tip stays off
                    // the pole, where the texture is squeezed.
                    float tv = v + 0.06f;
                    return Mathf.Max(Mathf.Abs(u) * 0.8660254f + tv * 0.5f, -tv) < 0.25f;
                }
                default:
                    return false;
            }
        }
    }
}
