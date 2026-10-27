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
        // What the Inspector offers, rather than PoolPartyMode: Speed Pool
        // with powers is a Party sub-mode internally, but it's picked here as
        // Mode SpeedPool + Pouvoirs. Classic keeps 0, so scenes keep their value.
        public enum Variant { Classic, Pouvoirs }

        [Tooltip("Règles de la partie lancée directement. Party = 8-ball avec les pouvoirs (caisses, bille à pouvoir). SpeedPool = les deux joueurs tirent en même temps. CatchTheHole = 8-ball avec des trous vivants sur le tapis (réglages : CatchTheHoleSettings). HungryHole = la blanche est un trou qui grandit, en même temps (réglages : HungryHoleSettings). SnakePool = les billes ramassées suivent la blanche en file, en même temps (réglages : SnakePoolSettings). PowerWashPool = les billes lavent le tapis crasseux, en même temps, au chrono (réglages : PowerWashPoolSettings).")]
        [SerializeField] private PoolGameMode mode = PoolGameMode.Party;
        [Tooltip("Speed Pool : Classic = sans pouvoirs, Pouvoirs = avec les caisses de pouvoirs. Party : toujours avec les pouvoirs (8-ball). Sans effet sur les autres modes.")]
        [SerializeField] private Variant partyMode = Variant.Classic;
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
            if (rules.MatchStarted) return;
            if (mode == PoolGameMode.SpeedPool && partyMode == Variant.Pouvoirs)
                rules.RequestStart(PoolGameMode.Party, PoolPartyMode.SpeedPool, targetScore);
            else
                rules.RequestStart(mode, PoolPartyMode.Classic, targetScore);
        }
    }
}
