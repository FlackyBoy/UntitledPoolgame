using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // In a scene played straight from the editor (test scene, R&D arena):
    // no main menu, the match starts on its own with these choices. No
    // effect in a level launched from the menu.
    [AddComponentMenu("Pool/Lancer sans le menu")]
    public class SkipMainMenu : MonoBehaviour
    {
        // ModeDefault first: scenes set up before this field start as before.
        public enum TypeChoice { ModeDefault, TurnBased, SpeedPool }

        [Tooltip("ModeDefault = le type d'origine du mode (Speed Pool pour Hungry Hole, Snake Pool et Power Wash Pool, chacun son tour pour les autres). TurnBased = chacun son tour. SpeedPool = les deux joueurs tirent en même temps, chacun sa blanche. 9-ball et 14.1 se jouent toujours chacun son tour.")]
        [SerializeField] private TypeChoice type = TypeChoice.ModeDefault;
        [Tooltip("Règles de la partie lancée directement. EightBall = classique. CatchTheHole = 8-ball avec des trous vivants sur le tapis (réglages : CatchTheHoleSettings). HungryHole = la blanche est un trou qui grandit (réglages : HungryHoleSettings). SnakePool = les billes ramassées suivent la blanche en file (réglages : SnakePoolSettings). PowerWashPool = les billes lavent le tapis crasseux, au chrono (réglages : PowerWashPoolSettings). PlayAnywhere = golf dans le décor, un trou posé dans la scène (réglages : PlayAnywhereSettings ; trou : Tools > Pool > Play Anywhere > Créer un trou). Party et SpeedPool sont des anciens modes : Party = EightBall avec les pouvoirs, SpeedPool = EightBall en Speed Pool.")]
        [SerializeField] private PoolGameMode mode = PoolGameMode.EightBall;
        [Tooltip("Caisses de pouvoirs et bille à pouvoir (la bille seulement chacun son tour). Les pouvoirs limités à d'autres modes ne sortent pas (Only In Modes, sur chaque pouvoir).")]
        [SerializeField] private bool powers;
        [Tooltip("Score à atteindre, si 14.1.")]
        [Min(1)] [SerializeField] private int targetScore = 150;

        // Before the type and the powers were picked apart, Speed Pool's
        // powers were this enum (1 = with powers). Read so scenes keep them.
        [SerializeField, HideInInspector] private int partyMode;

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
            bool legacyPowers = mode == PoolGameMode.SpeedPool && partyMode == 1;
            PoolMatchType matchType = type == TypeChoice.ModeDefault ? PoolMatchSetup.NativeType(mode)
                : type == TypeChoice.SpeedPool ? PoolMatchType.SpeedPool : PoolMatchType.TurnBased;
            rules.RequestStart(new PoolMatchSetup(mode, matchType, powers || legacyPowers, targetScore));
        }
    }
}
