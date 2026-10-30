using System.Collections.Generic;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Boogie Night (BoogieNight, BoogieWheel): a run of matches whose mode,
    // type and powers a wheel draws before each one, the first player out of
    // reach winning the night. Texts of its menu page, wheel and end card.
    [CreateAssetMenu(fileName = "BoogieNightSettings", menuName = "Pool/Boogie Night Settings")]
    public class BoogieNightSettings : ScriptableObject
    {
        [Header("Soirée")]
        [Tooltip("Nombre de parties proposé en arrivant sur la page Boogie Night.")]
        [Min(1)] public int defaultRounds = 5;
        [Tooltip("Plus petit et plus grand nombre de parties qu'on peut choisir.")]
        [Min(1)] public int minRounds = 1;
        [Min(1)] public int maxRounds = 15;
        [Tooltip("Relances permises à chaque tour de roue (les deux joueurs confondus).")]
        [Min(0)] public int rerollsPerSpin = 1;

        [Header("Roue")]
        [Tooltip("Modes sur la roue, une part chacun. Un mode peut sortir plusieurs fois dans la soirée. Play Anywhere (golf) n'est tiré que si le niveau a un trou posé. 9-ball et 14.1 retirés le 30/10.")]
        public List<PoolGameMode> modes = new List<PoolGameMode>
        {
            PoolGameMode.EightBall, PoolGameMode.CatchTheHole, PoolGameMode.HungryHole, PoolGameMode.SnakePool,
            PoolGameMode.PowerWashPool, PoolGameMode.PlayAnywhere,
        };
        [Tooltip("Chance que la partie tirée soit avec pouvoirs (0 = jamais, 1 = toujours).")]
        [Range(0f, 1f)] public float powersChance = 0.5f;
        [Tooltip("Chance qu'un mode jouable des deux façons soit tiré en Speed Pool plutôt que chacun son tour.")]
        [Range(0f, 1f)] public float speedPoolChance = 0.5f;
        [Tooltip("Score à atteindre quand la roue tire le 14.1.")]
        [Min(1)] public int fourteenOneTarget = 25;
        [Tooltip("Durée du tour de roue, en secondes.")]
        [Min(0.5f)] public float spinDuration = 3.2f;
        [Tooltip("Tours complets de la roue avant de s'arrêter.")]
        [Min(1)] public int spinTurns = 5;

        [Header("Page du menu")]
        public string pageHeading = "Boogie Night";
        [TextArea(2, 4)] public string pageRules = "Une roue tire le mode avant chaque partie. Chaque partie gagnée rapporte un point : le premier qu'on ne peut plus rattraper remporte la soirée. Égalité au bout ? Ultime partie !";
        [Tooltip("{0} = nombre de parties.")]
        public string roundsFormat = "{0} parties";
        public string roundsHint = "< >  (flèches, LB / RB, molette, - / +)";
        public string goText = "Lancer la soirée";

        [Header("Roue (textes)")]
        public string wheelTitle = "Boogie Night";
        [Tooltip("{0} = numéro de la partie, {1} = nombre de parties.")]
        public string roundFormat = "Partie {0} / {1}";
        public string finalRoundText = "Ultime partie !";
        public string powersOnText = "Avec pouvoirs";
        public string powersOffText = "Sans pouvoirs";
        public string playHint = "A / Entrée : on joue !";
        [Tooltip("{0} = relances restantes.")]
        public string rerollHintFormat = "Y / R : relancer ({0})";

        [Header("Fin de partie")]
        public string nextButton = "Tourner la roue";
        public string menuButton = "Menu principal";
        [Tooltip("{0} = numéro du joueur.")]
        public string nightWinnerFormat = "J{0} remporte la soirée !";
        [Tooltip("{0} et {1} = points de J1 et J2.")]
        public string scoreFormat = "Boogie Night · J1 {0} – {1} J2";

        private static BoogieNightSettings instance;
        public static BoogieNightSettings Instance =>
            instance != null ? instance : instance = PoolSettingsLoader.LoadOrDefault<BoogieNightSettings>("BoogieNightSettings");
    }
}
