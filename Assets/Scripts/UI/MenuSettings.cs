using System;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // One choice drawn as a pool ball (main menu, game types, pause pills).
    [Serializable]
    public class MenuBall
    {
        [Tooltip("Texte sous la bille (ou dans la pilule de la pause).")]
        public string label;
        [Tooltip("Petite ligne sous le texte (vide = aucune).")]
        public string sub;
        [Tooltip("Numéro (ou texte court) écrit sur la bille.")]
        public string number;
        [Tooltip("Couleur de la bille.")]
        public Color color = Color.white;
        [Tooltip("Bille rayée (bande colorée sur fond crème) au lieu de pleine.")]
        public bool stripe;

        public MenuBall() { }
        public MenuBall(string label, string number, string hex, bool stripe = false, string sub = null)
        {
            this.label = label;
            this.number = number;
            color = MenuSettings.Hex(hex);
            this.stripe = stripe;
            this.sub = sub;
        }
    }

    // A game type on the "Quel type de partie ?" page: its ball and the rules
    // written on the card next to it.
    [Serializable]
    public class MenuModeBall
    {
        public MenuBall ball = new MenuBall();
        [Tooltip("Règles affichées sur la carte bleue quand la bille est visée.")]
        [TextArea(2, 4)] public string rules;
    }

    // A row of the key bindings screen, found by its id: Move, Look, Pause
    // (fixed rows), or the name of a player action (Interact, Attack, Punch,
    // Kick, Next, Sprint, Jump).
    [Serializable]
    public class KeyRowText
    {
        [Tooltip("Move, Look, Pause, ou le nom d'une action du joueur (Interact, Attack, Punch, Kick, Next, Sprint, Jump).")]
        public string id;
        [Tooltip("Nom de l'action affiché.")]
        public string label;
        [Tooltip("Précision en petit à côté (vide = aucune).")]
        public string sub;
    }

    // Shared look and texts of the menus: the palette, the fonts, the
    // messages and formats the game writes itself (toasts, "{0} - prêt",
    // pause by J{0}, key binding rows…) and a few timings. The screens
    // themselves (elements, positions, texts, balls, background) are
    // MenuScreens edited in Menu Studio; the screen fields here only seed
    // their default layout (MenuDefaults). Assets/Resources/MenuSettings,
    // created by Tools > Pool > Ensure Config Assets Exist; read when a menu
    // is built, so changes show on the next Play. Tooltips in French: read by
    // the designer in the Inspector.
    [CreateAssetMenu(fileName = "MenuSettings", menuName = "Pool/Menu Settings")]
    public class MenuSettings : ScriptableObject
    {
        // ---------- Look ----------

        [Header("Disposition, textes et billes des écrans : Tools > Pool > Menu Studio.")]
        [Header("Ici : couleurs, polices, messages et formats, et les valeurs de départ des écrans.")]
        [Header("Couleurs (menu, pause, touches, chargement)")]
        [Tooltip("Tapis de la table.")] public Color felt = Hex("#1d7f4b");
        [Tooltip("Bord sombre du tapis.")] public Color feltDark = Hex("#125c35");
        [Tooltip("Bois de la table.")] public Color wood = Hex("#6b3a1e");
        [Tooltip("Bois sombre (tour des poches).")] public Color woodDark = Hex("#3e200f");
        [Tooltip("Texte clair (titres et libellés sur la table).")] public Color chalk = Hex("#fff7e3");
        [Tooltip("Ombre portée sous le texte clair.")] public Color chalkShadow = Hex("#0d4527");
        [Tooltip("Bille blanche, fond des billes rayées.")] public Color ballCream = Hex("#f6f1e4");
        [Tooltip("Encre : texte et contours sur les cartes.")] public Color ink = Hex("#1c1510");
        [Tooltip("Fond des cartes (sauvegardes, niveaux, pause, touches).")] public Color card = Hex("#fff6e3");
        [Tooltip("Accent jaune (étiquette de contexte, onglet actif).")] public Color yellow = Hex("#ffd23f");
        [Tooltip("Accent rouge (tampon « Bientôt »).")] public Color red = Hex("#ff4d3d");
        [Tooltip("Cube de craie : carte des règles et notes.")] public Color cube = Hex("#3d8fe0");
        [Tooltip("Dessous du cube de craie.")] public Color cubeDark = Hex("#1f5c99");
        [Tooltip("Couleur du joueur 1.")] public Color player1 = Hex("#2f7cf6");
        [Tooltip("Couleur du joueur 2.")] public Color player2 = Hex("#ef4a3c");

        [Header("Polices (vide = Titan One / Bowlby One de Resources/UI/Fonts)")]
        [Tooltip("Police du texte courant et des libellés.")] public Font bodyFont;
        [Tooltip("Police des gros titres (POOL GAME, Pause…).")] public Font titleFont;

        [Header("Fond du menu")]
        [Tooltip("Image de fond à la place du bar dessiné (recadrée pour remplir l'écran). Vide = bar la nuit.")]
        public Texture2D background;
        [Tooltip("Petites lumières floues du bar autour de la table.")]
        public bool barLights = true;
        [Tooltip("Logo à la place des deux lignes de titre (vide = titre écrit).")]
        public Texture2D logo;
        [Tooltip("Hauteur du logo, en pixels sur un écran 1080p.")]
        public float logoHeight = 260f;

        // ---------- Main menu ----------

        [Header("Accueil")]
        public string titleSmall = "Untitled";
        public string titleBig = "POOL GAME";
        public MenuBall newGameBall = new MenuBall("Nouvelle partie", "1", "#f4c20d");
        public MenuBall loadBall = new MenuBall("Charger", "2", "#1f4fd1");
        public MenuBall justForFunBall = new MenuBall("Just for fun", "3", "#d8261c");
        public MenuBall multiplayerBall = new MenuBall("Multijoueur", "12", "#6a2c91", true);
        public MenuBall settingsBall = new MenuBall("Réglages", "6", "#138a3a");
        public MenuBall quitBall = new MenuBall("Quitter", "8", "#111111");
        [Tooltip("Texte qui clignote près de la bille blanche.")]
        public string aimHint = "vise une bille, tire !";
        [Tooltip("Aide des touches en bas de l'accueil.")]
        public string titleKeysHint = "Viser : souris · flèches · stick    Tirer : clic · Entrée · A";
        [Tooltip("Aide des touches en bas des autres pages.")]
        public string pageKeysHint = "Choisir : souris · flèches · stick    Valider : clic · Entrée · A    Retour : Échap · B";

        [Header("Nouvelle partie / Charger")]
        public string newGameHeading = "Nouvelle partie";
        public string loadHeading = "Charger une partie";
        [Tooltip("{0} = numéro de l'emplacement.")]
        public string slotFormat = "Emplacement {0}";
        public string slotFree = "Libre";
        public string slotEmpty = "Vide";
        [TextArea(2, 3)] public string newGameNote = "Histoire et tutoriel à venir : en attendant, une partie classique.";
        [TextArea(2, 3)] public string loadNote = "Les sauvegardes arriveront avec l'histoire.";
        [Tooltip("Message en choisissant un emplacement de Nouvelle partie.")]
        public string newGameToast = "Le tuto arrive bientôt !";
        [Tooltip("Message en choisissant un emplacement vide de Charger.")]
        public string emptySlotToast = "Emplacement vide";

        [Header("Choix du niveau (niveaux eux-mêmes : GameFlowSettings)")]
        public string levelHeading = "On joue où ?";
        [Tooltip("Tampon sur les niveaux pas encore jouables.")]
        public string comingSoonStamp = "Bientôt";
        public string comingSoonToast = "Bientôt !";
        [Tooltip("Étiquette jaune quand on vient de Just for fun.")]
        public string justForFunContext = "Just for fun";
        [Tooltip("Étiquette jaune quand on vient de Multijoueur > Local.")]
        public string localContext = "Multijoueur · Local";

        [Header("Type de partie")]
        public string modeHeading = "Quel type de partie ?";
        public MenuModeBall classicMode = new MenuModeBall { ball = new MenuBall("Classique", "8", "#111111"),
            rules = "8-ball : pleines contre rayées, la 8 en dernier dans la poche annoncée." };
        public MenuModeBall powersMode = new MenuModeBall { ball = new MenuBall("Pouvoirs", "12", "#6a2c91", true),
            rules = "Les règles du 8-ball, plus des caisses et une bille à pouvoir sur la table." };
        public MenuModeBall nineBallMode = new MenuModeBall { ball = new MenuBall("9-ball", "9", "#f4c20d", true),
            rules = "Touche toujours la plus petite bille. Qui rentre la 9 gagne." };
        public MenuModeBall fourteenOneMode = new MenuModeBall { ball = new MenuBall("14.1", "3", "#d8261c"),
            rules = "Chaque bille rentrée rapporte 1 point." };
        [Tooltip("Option du 14.1. {0} = score à atteindre.")]
        public string targetScoreFormat = "Premier à {0}   < >  (LB / RB, molette, - / +)";
        public string powersOption = "Pouvoirs : Attaque · Défense · Effet";
        public string noPowersOption = "Sans pouvoirs";

        [Header("Multijoueur")]
        public string multiplayerHeading = "Multijoueur";
        public MenuBall localBall = new MenuBall("Local", "J1", "#2f7cf6", false, "écran partagé · 2 manettes");
        public MenuBall onlineBall = new MenuBall("En ligne", "NET", "#ef4a3c", true, "héberger · rejoindre");
        public string onlineToast = "Le jeu en ligne arrive plus tard";

        [Header("Salon (qui joue ?)")]
        public string lobbyHeading = "Qui joue ce soir ?";
        public string goText = "C'est parti";
        [Tooltip("Dans le rond vide. {0} = numéro du joueur.")]
        public string joinCallFormat = "J{0}\nappuie !";
        public string joinHint = "Une touche ou un bouton\npour rejoindre";
        [Tooltip("Sous un joueur arrivé. {0} = son appareil.")]
        public string readyFormat = "{0} - prêt";
        public string gamepadName = "Manette";
        public string keyboardName = "Clavier & souris";
        [Tooltip("Message quand J2 arrive.")]
        public string player2Joined = "Salut J2 !";

        // ---------- Pause ----------

        [Header("Pause")]
        public string pauseTitle = "Pause";
        public MenuBall resumeItem = new MenuBall("Reprendre", "1", "#f4c20d");
        public MenuBall pauseSettingsItem = new MenuBall("Réglages", "6", "#138a3a");
        public MenuBall mainMenuItem = new MenuBall("Menu principal", "8", "#111111");
        [Tooltip("{0} = numéro du joueur qui a mis en pause.")]
        public string pausedByFormat = "J{0} a mis la partie en pause";
        public string pausedText = "Partie en pause";
        public string confirmTitle = "Retour au menu ?";
        public string confirmText = "La partie en cours sera perdue.";
        public MenuBall confirmStayItem = new MenuBall("Non, je reste", "1", "#f4c20d");
        public MenuBall confirmQuitItem = new MenuBall("Oui, quitter", "8", "#111111");

        // ---------- Key bindings ----------

        [Header("Réglages > Touches")]
        public string settingsTitle = "Réglages";
        [Tooltip("Onglets : le premier est celui des touches, les autres sont marqués « bientôt ».")]
        public List<string> settingsTabs = new List<string> { "Touches", "Audio", "Vidéo", "Jeu" };
        [Tooltip("Ajouté aux onglets pas encore faits.")]
        public string tabSoonSuffix = " · bientôt";
        public string actionColumn = "Action";
        public string keyboardColumn = "Clavier / souris";
        public string gamepadColumn = "Manette";
        public string resetButton = "Réinitialiser";
        public string backButton = "Retour";
        [Tooltip("Noms des actions. Une ligne absente garde son nom par défaut.")]
        public List<KeyRowText> keyRows = new List<KeyRowText>
        {
            new KeyRowText { id = "Move", label = "Se déplacer", sub = "en visée : point de frappe" },
            new KeyRowText { id = "Look", label = "Regarder", sub = "en visée : tourner autour de la bille" },
            new KeyRowText { id = "Interact", label = "Interagir", sub = "ramasser · viser · valider" },
            new KeyRowText { id = "Attack", label = "Tirer · lancer · coup de queue", sub = "maintenir puis relâcher" },
            new KeyRowText { id = "Punch", label = "Coup de poing", sub = "mains vides" },
            new KeyRowText { id = "Kick", label = "Coup de pied", sub = "maintenir : spartiate" },
            new KeyRowText { id = "Next", label = "Pouvoir", sub = "mode Pouvoirs" },
            new KeyRowText { id = "Sprint", label = "Courir", sub = "" },
            new KeyRowText { id = "Jump", label = "Sauter", sub = "prévu" },
            new KeyRowText { id = "Pause", label = "Pause", sub = "" },
        };

        // ---------- Timings ----------

        [Header("Durées")]
        [Tooltip("Durée d'affichage des petits messages (« Bientôt ! »…), en secondes.")]
        [Min(0.3f)] public float toastDuration = 1.4f;
        [Tooltip("Vitesse de l'animation du tir sur une bille (1 = normale, 2 = deux fois plus rapide).")]
        [Min(0.2f)] public float shotSpeed = 1f;
        [Tooltip("Période du clignotement de « vise une bille, tire ! », en secondes.")]
        [Min(0.1f)] public float blinkPeriod = 0.7f;

        private static MenuSettings instance;
        public static MenuSettings Instance =>
            instance != null ? instance : instance = Pool.PoolSettingsLoader.LoadOrDefault<MenuSettings>("MenuSettings");

        public KeyRowText KeyRow(string id) => keyRows.Find(r => r != null && r.id == id);

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
