using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Gives every ball but the cue ball one shared look (Identical Groups) and
    // restores the real ones after, by swapping sharedMaterials (works for any
    // model; a property block needs matching shaders). PowerBall changes
    // materials through SetBaseMaterial, so it doesn't reveal a ball.
    public static class BallDisguise
    {
        private static readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private static readonly List<Renderer> scratch = new List<Renderer>();

        public static bool Active { get; private set; }

        // look: materials to copy, one array per renderer (a renderer takes the
        // look at its index, or the first). Potted balls too, so one coming
        // back doesn't give itself away.
        public static void Apply(IReadOnlyList<Material[]> look)
        {
            if (Active) Restore();
            if (look == null || look.Count == 0) return;

            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                if (ball.IsCueBall) continue;
                CollectRenderers(ball, scratch);
                for (int i = 0; i < scratch.Count; i++)
                {
                    Renderer renderer = scratch[i];
                    originals[renderer] = renderer.sharedMaterials;
                    Material[] source = look[i < look.Count ? i : 0];
                    renderer.sharedMaterials = FitTo(source, renderer.sharedMaterials.Length);
                }
            }
            Active = true;
        }

        public static void Restore()
        {
            foreach (KeyValuePair<Renderer, Material[]> entry in originals)
                if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
            originals.Clear();
            Active = false;
        }

        // A ball's real look (its originals if disguised, its number material
        // rather than a power material).
        public static List<Material[]> LookOf(PoolBall ball)
        {
            var look = new List<Material[]>();
            CollectRenderers(ball, scratch);
            foreach (Renderer renderer in scratch)
            {
                Material[] materials = originals.TryGetValue(renderer, out Material[] saved) ? saved : renderer.sharedMaterials;
                look.Add((Material[])materials.Clone());
            }
            if (look.Count > 0 && ball.TryGetComponent(out PowerBall powerBall) && powerBall.OriginalMaterial != null)
                look[0][0] = powerBall.OriginalMaterial;
            return look;
        }

        // PowerBall's material swap (slot 0 of the ball's own renderer).
        public static void SetBaseMaterial(Renderer renderer, Material material)
        {
            if (Active && originals.TryGetValue(renderer, out Material[] saved))
            {
                saved[0] = material;
                return;
            }
            renderer.sharedMaterial = material;
        }

        // Mesh renderers only: lights, lines and particles parented to a
        // ball (power glow, trails) keep their own look.
        private static void CollectRenderers(PoolBall ball, List<Renderer> into)
        {
            into.Clear();
            foreach (Renderer renderer in ball.GetComponentsInChildren<Renderer>(true))
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) into.Add(renderer);
        }

        private static Material[] FitTo(Material[] source, int count)
        {
            var fitted = new Material[Mathf.Max(1, count)];
            for (int i = 0; i < fitted.Length; i++)
                fitted[i] = source[Mathf.Min(i, source.Length - 1)];
            return fitted;
        }
    }
}
