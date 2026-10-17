using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // In a scene played straight from the editor (test scene, R&D arena):
    // no main menu, the match starts on its own in this mode. No effect in a
    // level launched from the menu.
    [AddComponentMenu("Pool/Lancer sans le menu")]
    public class SkipMainMenu : MonoBehaviour
    {
        [Tooltip("Règles de la partie lancée directement. Party = 8-ball avec les pouvoirs (caisses, bille à pouvoir).")]
        [SerializeField] private PoolGameMode mode = PoolGameMode.Party;
        [Tooltip("Sous-mode, si Party.")]
        [SerializeField] private PoolPartyMode partyMode = PoolPartyMode.Classic;
        [Tooltip("Score à atteindre, si 14.1.")]
        [Min(1)] [SerializeField] private int targetScore = 150;

        // Read by SyntheseMenu before creating itself.
        public static bool InScene => FindAnyObjectByType<SkipMainMenu>() != null;

        private void Start()
        {
            if (GameSession.Active) return;
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null)
            {
                Debug.LogWarning("[SkipMainMenu] Pas de PoolMatchRules dans la scène : rien à lancer.", this);
                return;
            }
            if (!rules.MatchStarted) rules.RequestStart(mode, partyMode, targetScore);
        }
    }
}
