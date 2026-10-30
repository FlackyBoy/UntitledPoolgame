using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Which types each mode can be played in (turn based, Speed Pool) and
    // which one it starts on in the menus. Read through PoolMatchSetup.
    [CreateAssetMenu(fileName = "PoolModeSettings", menuName = "Pool/Mode Settings")]
    public class PoolModeSettings : ScriptableObject
    {
        [System.Serializable]
        public class ModeEntry
        {
            public PoolGameMode mode;
            [Tooltip("Ce mode peut se jouer chacun son tour.")]
            public bool turnBased = true;
            [Tooltip("Ce mode peut se jouer en Speed Pool (tout le monde en même temps, chacun sa blanche). Le mode doit avoir des règles pour ça : 9-ball et 14.1 n'en ont pas encore, ils restent chacun son tour même coché.")]
            public bool speedPool = true;
            [Tooltip("Type sur lequel le mode démarre dans le menu (Y / Tab pour changer) et dans Lancer sans le menu (Type = ModeDefault). Ignoré s'il n'est pas coché au-dessus.")]
            public PoolMatchType defaultType = PoolMatchType.TurnBased;
            [Tooltip("Partie avec pouvoirs : les caisses et la bille à pouvoir ne donnent que les pouvoirs propres à ce mode (ceux qui le listent dans leur Only In Modes). Décoché : aussi les pouvoirs communs (Only In Modes vide).")]
            public bool ownPowersOnly;

            public ModeEntry(PoolGameMode mode, bool turnBased, bool speedPool, PoolMatchType defaultType, bool ownPowersOnly = false)
            {
                this.mode = mode;
                this.turnBased = turnBased;
                this.speedPool = speedPool;
                this.defaultType = defaultType;
                this.ownPowersOnly = ownPowersOnly;
            }

            public bool Allows(PoolMatchType type) => type == PoolMatchType.SpeedPool ? speedPool : turnBased;
        }

        [Tooltip("Une ligne par mode. Un mode absent de la liste prend les valeurs par défaut du code. Party et SpeedPool (anciens modes) n'ont pas besoin de ligne : ils sont lus comme Classique.")]
        public ModeEntry[] modes = Defaults();

        // Also what a mode missing from the list falls back to.
        public static ModeEntry[] Defaults() => new[]
        {
            new ModeEntry(PoolGameMode.EightBall, true, true, PoolMatchType.TurnBased),
            new ModeEntry(PoolGameMode.NineBall, true, false, PoolMatchType.TurnBased),
            new ModeEntry(PoolGameMode.FourteenOne, true, false, PoolMatchType.TurnBased),
            new ModeEntry(PoolGameMode.CatchTheHole, true, true, PoolMatchType.TurnBased),
            new ModeEntry(PoolGameMode.HungryHole, true, true, PoolMatchType.SpeedPool),
            new ModeEntry(PoolGameMode.SnakePool, true, true, PoolMatchType.SpeedPool),
            new ModeEntry(PoolGameMode.PowerWashPool, true, true, PoolMatchType.SpeedPool, ownPowersOnly: true),
        };

        private static PoolModeSettings instance;
        private static ModeEntry[] fallback;

        // Asked every frame by the menu: loaded once.
        public static PoolModeSettings Instance =>
            instance != null ? instance : instance = PoolSettingsLoader.LoadOrDefault<PoolModeSettings>("PoolModeSettings");

        public ModeEntry Find(PoolGameMode mode)
        {
            if (modes != null)
                foreach (ModeEntry entry in modes)
                    if (entry != null && entry.mode == mode) return entry;
            if (fallback == null) fallback = Defaults();
            foreach (ModeEntry entry in fallback)
                if (entry.mode == mode) return entry;
            return null;
        }
    }
}
