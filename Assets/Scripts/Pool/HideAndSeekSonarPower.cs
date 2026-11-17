using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // "Sonar", the seeker's power in Hide and seek: for a few seconds a disc on
    // the felt, with the hider's ball somewhere inside. Handed out each round by
    // the rules (HideAndSeekRuleSet.StartRound), one use. Both players see it:
    // the hider can run, and a ball that bolts gives itself away.
    [CreateAssetMenu(fileName = "HideAndSeekSonarPower", menuName = "Pool/Powers/Hide And Seek/Sonar")]
    public class HideAndSeekSonarPower : PoolPower
    {
        [Tooltip("Rayon de la zone, en mètres, pour une table à l'échelle 1 (une bille : 0,05 m). Plus grand = moins précis.")]
        [Min(0.05f)] [SerializeField] private float radius = 0.6f;
        [Tooltip("Durée d'affichage de la zone, en secondes.")]
        [Min(0.1f)] [SerializeField] private float duration = 4f;
        [Tooltip("Couleur de la zone, avec sa transparence.")]
        [SerializeField] private Color color = new Color(0.2f, 0.9f, 1f, 0.3f);

        public override PowerType Type => PowerType.Effect;

        public override void Activate(PoolMatchRules match, int activatingPlayer)
        {
            HideAndSeekRuleSet rules = HideAndSeekRuleSet.Current;
            if (rules == null)
            {
                Debug.Log($"[HideAndSeek] {PowerName} : sans effet hors du Cache-cache.", this);
                return;
            }
            rules.Ping(radius, duration, color);
        }
    }
}
