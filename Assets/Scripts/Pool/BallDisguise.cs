using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Gives every ball but the cue ball one shared look (IdenticalGroupsPower)
    // and puts the real ones back afterwards. Works on whatever renderers a
    // ball has (a generated sphere or a custom model), by swapping their
    // sharedMaterials and remembering the originals — not a property block,
    // which only works when every ball material uses the same shader
    // properties.
    //
    // PowerBall swaps a ball's material too (category material while it
    // carries a power): it goes through SetBaseMaterial so a change made
    // while disguised lands in the remembered originals instead of revealing
    // the ball.
    public static class BallDisguise
    {
        private static readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private static readonly List<Renderer> scratch = new List<Renderer>();

        public static bool Active { get; private set; }

        // look: the materials of the ball to copy, one array per renderer of
        // that ball (a renderer of a disguised ball takes the look of the
        // renderer at the same index, or the first one). Every ball,
        // pocketed (inactive) ones included, so a ball coming back on the
        // table can't give itself away either.
        public static void Apply(IReadOnlyList<Material[]> look)
        {
            if (Active) Restore();
            if (look == null || look.Count == 0) return;

            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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

        // The look of one ball as it really is (its remembered originals if
        // it's currently disguised; a power ball's own number material
        // rather than its category material).
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
