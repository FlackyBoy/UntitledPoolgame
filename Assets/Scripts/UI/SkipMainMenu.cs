using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Put on any GameObject of a scene played straight from the editor (a
    // test scene, the R&D arena): the main menu (SyntheseMenu), which
    // otherwise shows up in any scene with a pool match, isn't created, and
    // the match starts on its own in the mode chosen here — without it no
    // match would ever start (no turns, no powers). Does nothing in a level
    // launched from the menu (GameSession), whose start stays the menu's.
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
