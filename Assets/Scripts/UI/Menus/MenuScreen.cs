using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    public enum MenuElementKind
    {
        Ball,    // a pool ball with its label under it (shot with the cue)
        Card,    // a "pop" card: background, optional picture, title, line
        Text,    // just text
        Image,   // a picture
        Panel,   // a coloured box (frame, note, chip)
        Pill,    // a small ball + a word on a rounded bar (pause choices)
        Slot,    // an area the game fills itself (level cards, lobby, gutter…)
    }

    // What choosing an element does. The game knows these; the designer
    // picks one per element.
    public enum MenuAction
    {
        None,
        OpenScreen,      // goes to "Target Screen"
        Back,
        NewGameSlot,     // a save slot of "Nouvelle partie"
        LoadSlot,        // a save slot of "Charger"
        JustForFun,
        PlayLocal,
        PlayOnline,
        LobbyGo,         // "C'est parti" in the lobby
        StartMode,       // launches the level with "Mode"
        Quit,
        Resume,          // pause
        OpenSettings,    // key bindings
        AskMainMenu,     // pause → confirmation
        ConfirmStay,
        ConfirmQuit,
    }

    public enum MenuEntrance { None, Pop, Drop, Rise, SlideFromLeft, SlideFromRight, Fade, Spin }
    public enum MenuIdle { None, Float, Pulse, Swing, Wobble, Spin }
    public enum MenuBackdrop { Bar, Color, Image, Dim, None }

    // One element of a menu screen, placed freely (center in % of the
    // screen, size in px of a 1920 × 1080 screen).
    [Serializable]
    public class MenuElement
    {
        [Tooltip("Nom de l'élément. Les éléments que le jeu remplit lui-même (context, rules, levels, slot1…) sont retrouvés par ce nom : ne pas le changer pour ceux-là.")]
        public string id = "element";
        public MenuElementKind kind = MenuElementKind.Ball;
        [Tooltip("Décoché : l'élément n'est pas affiché.")]
        public bool visible = true;

        [Header("Place")]
        [Tooltip("Centre de l'élément, en % de l'écran (0,0 = en haut à gauche).")]
        public Vector2 position = new Vector2(50f, 50f);
        [Tooltip("Taille en pixels sur un écran 1080p. Bille : diamètre (x). Texte : 0 = taille du texte.")]
        public Vector2 size = new Vector2(110f, 110f);
        [Tooltip("Inclinaison, en degrés.")]
        public float rotation;

        [Header("Apparence")]
        [Tooltip("Bille, pilule : couleur de la bille. Carte, panneau : fond. Image : teinte.")]
        public Color color = Color.white;
        [Tooltip("Bille rayée.")]
        public bool stripe;
        [Tooltip("Numéro écrit sur la bille.")]
        public string number = "1";
        [Tooltip("Image (carte, image, panneau).")]
        public Texture2D image;
        public Color borderColor = new Color(0.11f, 0.08f, 0.06f);
        [Min(0f)] public float borderWidth;
        [Min(0f)] public float cornerRadius = 14f;

        [Header("Texte (balises TextFx acceptées)")]
        [TextArea(1, 3)] public string text = "";
        [TextArea(1, 3)] public string subText = "";
        [Min(1f)] public float textSize = 44f;
        [Min(1f)] public float subTextSize = 24f;
        public Color textColor = new Color(1f, 0.97f, 0.89f);
        [Tooltip("Police des gros titres (sinon police du texte).")]
        public bool titleFont;
        [Tooltip("Ombre sous le texte.")]
        public bool textShadow = true;
        [Tooltip("Les lettres arrivent une à une à l'ouverture de l'écran.")]
        public bool revealText;

        [Header("Action")]
        public MenuAction action = MenuAction.None;
        [Tooltip("Écran ouvert par l'action « Open Screen ».")]
        public string targetScreen = "";
        [Tooltip("Type de partie lancé par « Start Mode ».")]
        public PoolGameMode mode = PoolGameMode.EightBall;
        [Tooltip("Règles affichées sur la carte « rules » quand la bille est visée.")]
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Grossissement quand l'élément est visé (0,18 = +18 %).")]
        [Range(0f, 0.6f)] public float focusScale = 0.18f;

        [Header("Effets")]
        public MenuEntrance entrance = MenuEntrance.Pop;
        [Tooltip("Délai avant l'arrivée, en secondes (pour faire arriver les éléments les uns après les autres).")]
        [Min(0f)] public float entranceDelay;
        [Min(0.05f)] public float entranceDuration = 0.45f;
        [Tooltip("Mouvement permanent.")]
        public MenuIdle idle = MenuIdle.None;
        [Range(0f, 3f)] public float idleAmount = 1f;
        [Range(0f, 3f)] public float idleSpeed = 1f;

        [Header("Feel")]
        [Tooltip("Séquence Feel jouée quand l'élément devient visé.")]
        public MMF_Player feelOnFocus;
        [Tooltip("Séquence Feel jouée quand l'élément est choisi.")]
        public MMF_Player feelOnPress;

        public bool Selectable => action != MenuAction.None && visible;

        public MenuElement Clone() => (MenuElement)MemberwiseClone();
    }

    // One menu screen (title, level choice, pause…), edited in
    // Tools > Pool > Menu Studio. Assets/Resources/Menus/<Id>.asset, created
    // with the current layout by Tools > Pool > Ensure Config Assets Exist;
    // MenuLayouts falls back to that default when a file is missing.
    [CreateAssetMenu(fileName = "MenuScreen", menuName = "Pool/Menu Screen")]
    public class MenuScreen : ScriptableObject
    {
        [Header("Fond")]
        public MenuBackdrop backdrop = MenuBackdrop.Bar;
        [Tooltip("Fond « Color » ; voile du fond « Dim » (pause).")]
        public Color backgroundColor = new Color(0.05f, 0.03f, 0.02f, 0.62f);
        [Tooltip("Fond « Image » (recadrée pour remplir l'écran).")]
        public Texture2D backgroundImage;
        [Tooltip("Fond « Bar » : petites lumières floues.")]
        public bool barLights = true;
        [Tooltip("Fond « Bar » : la table de billard.")]
        public bool table = true;

        [Header("Queue (on vise les billes)")]
        public bool hasCue;
        [Tooltip("Bille blanche, en % de l'écran.")]
        public Vector2 cuePosition = new Vector2(84f, 84f);
        [Min(10f)] public float cueSize = 64f;

        [Header("Navigation")]
        [Tooltip("Écran ouvert par Retour (Échap / B). Vide = aucun.")]
        public string backScreen = "";
        [Tooltip("Séquence Feel jouée à l'ouverture de l'écran.")]
        public MMF_Player feelOnOpen;

        public List<MenuElement> elements = new List<MenuElement>();

        public MenuElement Find(string id) => elements.Find(e => e != null && e.id == id);
    }
}
