using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Everything the in-game HUD (MatchHud) shows and how: texts, colours,
    // sizes, timings, which interjections play. Loaded from
    // Assets/Resources/HudSettings (Tools > Pool > Ensure Config Assets
    // Exist), same pattern as the gameplay settings — so the HUD can be tuned
    // and reworded without touching code. Read when the HUD is built (texts
    // and colours) and every frame (timings): changes made while playing show
    // up at once for timings, at the next scene load for the rest.
    // Tooltips are in French: they are read by the designer in the Inspector.
    [CreateAssetMenu(fileName = "HudSettings", menuName = "Pool/HUD Settings")]
    public class HudSettings : ScriptableObject
    {
        [Header("Polices (vide = Titan One / Bowlby One de Resources/UI/Fonts)")]
        [Tooltip("Police des textes courants.")]
        public Font bodyFont;
        [Tooltip("Police des titres, étiquettes et interjections.")]
        public Font titleFont;

        [Header("Taille")]
        [Tooltip("Multiplie la taille de tout le HUD (1 = calé pour une moitié d'écran de 1920 × 1080).")]
        [Range(0.5f, 2f)] public float scale = 1f;
        [Tooltip("Taille du texte des interjections.")]
        public float shoutFontSize = 86f;
        [Tooltip("Taille du texte « À toi ! ».")]
        public float turnTagFontSize = 34f;
        [Tooltip("Taille du texte des bandeaux d'aide.")]
        public float bannerFontSize = 26f;

        [Header("Couleurs")]
        public Color player1Color = new Color32(0x2f, 0x7c, 0xf6, 0xff);
        public Color player2Color = new Color32(0xef, 0x4a, 0x3c, 0xff);
        [Tooltip("Contours et ombres « pop ».")]
        public Color inkColor = new Color32(0x1c, 0x15, 0x10, 0xff);
        [Tooltip("Fond des cartes (plaque, bandeau, sponsor, gagnant).")]
        public Color cardColor = new Color32(0xff, 0xf6, 0xe3, 0xff);
        [Tooltip("« À toi ! », interjections positives, bouton Revanche.")]
        public Color highlightColor = new Color32(0xff, 0xd2, 0x3f, 0xff);
        [Tooltip("Interjections négatives (« FAUTE ! », « Oups ! ») et point de frappe.")]
        public Color dangerColor = new Color32(0xff, 0x4d, 0x3d, 0xff);
        [Tooltip("Interjection de la bille destructrice (« BOUM ! »).")]
        public Color blastColor = new Color32(0xff, 0x9a, 0x1f, 0xff);
        [Tooltip("Voile sur la moitié du joueur qui attend son tour.")]
        public Color waitingDimColor = new Color(0.04f, 0.024f, 0.016f, 0.28f);
        [Tooltip("Jauge de puissance : début, milieu et fin.")]
        public Color gaugeLowColor = new Color32(0x30, 0xb5, 0x66, 0xff);
        public Color gaugeMidColor = new Color32(0xff, 0xb3, 0x00, 0xff);
        public Color gaugeHighColor = new Color32(0xff, 0x4d, 0x3d, 0xff);

        [Header("Textes : tour et plaque")]
        public string yourTurnText = "À toi !";
        [Tooltip("{0} = numéro du joueur dont c'est le tour.")]
        public string otherTurnFormat = "Tour de J{0}";
        [Tooltip("{0} = numéro du joueur.")]
        public string playerTagFormat = "J{0}";
        public string groupUndecidedText = "Groupe à décider";
        public string nextBallText = "Prochaine ";
        public string powerGaugeText = "Puissance";
        [Tooltip("Touche affichée sous le pouvoir en stock.")]
        public string powerKeyText = "F / B";

        [Header("Textes : bandeaux d'aide")]
        public string ballInHandBanner = "Main libre ! Place la blanche · E / Y pour valider";
        [Tooltip("{0} = poche visée.")]
        public string callPocketBannerFormat = "Bille 8 → {0} · E / Y pour valider";
        public string callPocketNoneBanner = "Bille 8 : vise une poche";
        public string outOfReachBanner = "Trop loin — contourne la table";
        [Tooltip("{0} = poche annoncée.")]
        public string eightCalledBannerFormat = "Poche annoncée : {0}";
        public string eightToCallBanner = "Bille 8 : annonce ta poche (vue du dessus)";
        public string blastArmedBanner = "Bille destructrice armée : la 1re bille touchée explose";

        [Header("Interjections")]
        [Tooltip("Une au hasard quand une bille rentre.")]
        public string[] potShouts = { "Dans le mille !", "Quel coup !", "Et hop !" };
        public string cueBallPottedShout = "Oups !";
        public string foulShout = "FAUTE !";
        public string ballInHandShout = "Main libre !";
        public string fullPowerShout = "OHHH !";
        public string powerUsedShout = "Envoyé !";
        public string blastShout = "BOUM !";
        [Tooltip("Afficher les interjections quand une bille rentre.")]
        public bool showPotShouts = true;
        [Tooltip("Charge (0 à 1) à partir de laquelle un tir déclenche l'interjection du tir plein.")]
        [Range(0f, 1f)] public float fullPowerThreshold = 0.95f;

        [Header("Carte sponsor (pouvoir ramassé)")]
        public bool showSponsorCard = true;
        public string sponsorHeader = "CE TOUR VOUS EST OFFERT PAR";
        [Tooltip("Ajouté après le nom du pouvoir.")]
        public string sponsorNameSuffix = "™";
        [Tooltip("{0} = catégorie du pouvoir.")]
        public string sponsorLineFormat = "Pouvoir {0} · F / B pour le lancer";
        public string attackLabel = "Attaque";
        public string defenseLabel = "Défense";
        public string effectLabel = "Effet";

        [Header("Fin de partie")]
        [Tooltip("{0} = numéro du gagnant.")]
        public string winnerFormat = "J{0} gagne !";
        public string rematchText = "Revanche";
        public string rematchHint = "Entrée · E · A / Y · clic";
        public bool showConfetti = true;

        [Header("Durées et mouvements (secondes, temps réel)")]
        public float shoutDuration = 1.3f;
        public float burstDuration = 1.1f;
        public float sponsorDuration = 3.1f;
        [Tooltip("Délai entre « FAUTE ! » et « Main libre ! ».")]
        public float foulToBallInHandDelay = 1.3f;
        public float shakeDuration = 0.35f;
        [Tooltip("Amplitude de la secousse (pixels du HUD).")]
        public float shakeAmplitude = 10f;
        [Tooltip("Hauteur (pixels) du balancement de « À toi ! ».")]
        public float turnTagBob = 5f;
        public float turnTagBobPeriod = 1.2f;
        [Tooltip("Assombrir la moitié du joueur qui attend son tour.")]
        public bool dimWaitingPlayer = true;
    }
}
