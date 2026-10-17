using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Groupe identique": for the opponent's next turn every ball but the cue
    // ball looks the same. The rules don't change. Turn-bound like Vision
    // Impair. The HUD plate is hidden meanwhile, or counting balls would
    // give it away.
    [CreateAssetMenu(fileName = "IdenticalGroupsPower", menuName = "Pool/Powers/Identical Groups (Attack)")]
    public class IdenticalGroupsPower : PoolPower
    {
        public enum Look
        {
            Colorless,
            LikeBall,
        }

        [Tooltip("Incolore : toutes les billes prennent un aspect uni (Colorless Material, ou à défaut Colorless Color).\nComme une bille : toutes prennent l'aspect de la bille choisie (Look Ball Number).")]
        [SerializeField] private Look look = Look.Colorless;
        [Tooltip("Comme une bille : numéro de la bille dont toutes les autres prennent l'aspect (1 à 15 ; 8 = toutes ressemblent à la 8).")]
        [Range(1, 15)]
        [SerializeField] private int lookBallNumber = 8;
        [Tooltip("Incolore : matériau à appliquer à toutes les billes. Vide = un matériau uni de la couleur ci-dessous.")]
        [SerializeField] private Material colorlessMaterial;
        [Tooltip("Incolore, sans matériau : couleur unie des billes.")]
        [SerializeField] private Color colorlessColor = new Color(0.82f, 0.82f, 0.8f);

        public override PowerType Type => PowerType.Attack;

        // Built once per run, only when no colorless material is assigned.
        private Material generatedColorless;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            match.QueueIdenticalGroups(1 - activatingPlayer, this);
        }

        // The materials every disguised ball takes (see BallDisguise.Apply);
        // null if the chosen ball can't be found (falls back to colorless).
        public IReadOnlyList<Material[]> BuildLook()
        {
            if (look == Look.LikeBall)
            {
                foreach (PoolBall ball in FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
                    if (!ball.IsCueBall && ball.Number == lookBallNumber) return BallDisguise.LookOf(ball);
                Debug.LogWarning($"[IdenticalGroups] Bille {lookBallNumber} introuvable : aspect incolore à la place.");
            }
            return new List<Material[]> { new[] { ColorlessMaterial() } };
        }

        private Material ColorlessMaterial()
        {
            if (colorlessMaterial != null) return colorlessMaterial;
            if (generatedColorless == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                generatedColorless = new Material(shader) { name = "IdenticalGroups (colorless)" };
                generatedColorless.SetFloat("_Smoothness", 0.85f);
            }
            // Every time: a colour changed in the Inspector applies to the next use.
            generatedColorless.SetColor("_BaseColor", colorlessColor);
            return generatedColorless;
        }
    }
}
