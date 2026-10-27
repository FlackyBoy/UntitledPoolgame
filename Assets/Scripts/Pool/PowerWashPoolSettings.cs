using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Power Wash Pool (PowerWashPoolRuleSet, PowerWashDirt): the grime on the
    // felt, how the balls wash it, and how the match ends.
    [CreateAssetMenu(fileName = "PowerWashPoolSettings", menuName = "Pool/Power Wash Pool Settings")]
    public class PowerWashPoolSettings : ScriptableObject
    {
        [Header("Match")]
        [Tooltip("Durée de la partie (secondes). À la fin, celui qui a lavé le plus de tapis gagne ; à égalité, la partie continue jusqu'à ce que l'un passe devant.")]
        [Min(10f)] public float matchDuration = 180f;

        [Header("Grime")]
        [Tooltip("Nombre de cases de crasse sur la longueur du tapis (la largeur suit). Plus = bords des traces plus nets, un peu plus de calcul et d'envoi à la carte graphique.")]
        [Range(64, 1024)] public int resolution = 512;
        [Tooltip("Bords nets entre le sale et le propre (chaque case est un carré franc). Décoché : bords adoucis, plus flous.")]
        public bool sharpEdges = true;
        [Tooltip("Couleur de la crasse (l'alpha règle à quel point elle cache le tapis).")]
        public Color dirtColor = new Color(0.32f, 0.24f, 0.13f, 0.92f);
        [Tooltip("Variation de teinte de la crasse (taches plus claires et plus foncées), 0 = unie.")]
        [Range(0f, 1f)] public float dirtNoise = 0.35f;
        [Tooltip("Taille des taches de crasse, en mètres.")]
        [Min(0.01f)] public float dirtNoiseScale = 0.12f;
        [Tooltip("Opacité de la teinte du joueur sur le tapis qu'il a lavé (0 = tapis propre sans couleur).")]
        [Range(0f, 1f)] public float washedTint = 0.22f;
        [Tooltip("Pas de crasse autour des poches (marge en plus du rayon de la poche, en mètres) : on voit les trous, et ces cases ne comptent pas.")]
        [Min(0f)] public float pocketClearance = 0.02f;

        [Header("Washing")]
        [Tooltip("Largeur de la trace laissée par une bille lente, en rayons de bille (1 = la largeur de la bille).")]
        [Min(0f)] public float slowTrailWidth = 0.8f;
        [Tooltip("Largeur de la trace d'une bille lancée à Full Speed ou plus, en rayons de bille.")]
        [Min(0f)] public float fastTrailWidth = 1.6f;
        [Tooltip("Vitesse (m/s) à partir de laquelle la trace est la plus large.")]
        [Min(0.1f)] public float fullSpeed = 3f;
        [Tooltip("Repasser sur le tapis lavé par l'autre le reprend pour soi. Décoché : une case lavée reste à celui qui l'a lavée en premier.")]
        public bool stealWashed = true;

        [Header("Pocket splash")]
        [Tooltip("Une de ses billes rentrée éclabousse le tapis autour de la poche pour son propriétaire, puis ressort à sa place : portée des gouttes les plus lointaines (mètres).")]
        [Min(0f)] public float pocketSplashRadius = 0.4f;
        [Tooltip("Taille de la flaque au centre de l'éclaboussure, en part de la portée.")]
        [Range(0.1f, 1f)] public float splashPuddleShare = 0.45f;
        [Tooltip("Nombre de gouttes projetées autour de la flaque (chacune au bout d'une traînée).")]
        [Range(0, 40)] public int splashDrops = 14;
        [Tooltip("Ouverture de l'éventail des gouttes (degrés), tourné vers le milieu du tapis (360 = tout autour).")]
        [Range(30f, 360f)] public float splashSpread = 160f;
        [Tooltip("Temps (secondes) que mettent les gouttes à retomber, les plus lointaines en dernier. La flaque, elle, est là dès que la bille rentre.")]
        [Min(0f)] public float splashDuration = 0.15f;

        [Header("Splash juice")]
        [Tooltip("La peinture fraîche de l'éclaboussure brille (couleur du joueur éclaircie, presque opaque) puis se fond dans la teinte du tapis lavé en ce temps (secondes). 0 = pas d'éclat.")]
        [Min(0f)] public float freshPaintTime = 0.6f;
        [Tooltip("De combien la peinture fraîche est éclaircie vers le blanc (0 = couleur du joueur pure).")]
        [Range(0f, 1f)] public float freshPaintWhiten = 0.35f;
        [Tooltip("Effet joué à la poche (gerbe d'eau), teinté à la couleur du joueur. Vide = aucun. Rempli par Ensure Config Assets Exist (CFXR Water Splash).")]
        public GameObject splashVfx;
        [Tooltip("Taille de l'effet pour l'éclaboussure d'une bille (celle de la 8 est plus grosse, en proportion de sa portée).")]
        [Min(0f)] public float splashVfxScale = 1.5f;
        [Tooltip("Teinter les particules de l'effet à la couleur du joueur (décoché : les couleurs de l'effet).")]
        public bool tintSplashVfx = false;
        [Tooltip("Parties de l'effet multipliées pour une explosion d'eau (début de leur nom dans le prefab ; CFXR Water Splash : « Drops » = les gouttes qui jaillissent, chacune avec ses éclaboussures et ses ronds, « Foam » = l'écume).")]
        public string[] splashVfxBoosted = { "Drops", "Foam" };
        [Tooltip("Combien de fois plus de particules pour ces parties (1 = l'effet tel quel).")]
        [Range(1, 30)] public int splashVfxDropCount = 10;
        [Tooltip("Ouverture du cône dans lequel ces parties jaillissent (degrés ; 0 = celle de l'effet, CFXR Water Splash : tout droit).")]
        [Range(0f, 90f)] public float splashVfxSpread = 40f;
        [Tooltip("Vitesse de ces parties (multiplicateur) : plus haut = elles montent et retombent plus loin.")]
        [Range(0.2f, 3f)] public float splashVfxSpeed = 0.8f;
        [Tooltip("Secousse de l'écran du joueur qui gagne l'éclaboussure (force, durée en secondes ; plus forte pour la 8).")]
        [Min(0f)] public float shakeMagnitude = 0.12f;
        [Min(0f)] public float shakeDuration = 0.25f;
        [Tooltip("Flash de son écran à sa couleur (opacité, durée en secondes).")]
        [Range(0f, 1f)] public float screenFlashAlpha = 0.2f;
        [Min(0f)] public float screenFlashDuration = 0.25f;

        [Header("The 8 (mud ball)")]
        [Tooltip("La 8 resalit le tapis là où elle roule (largeur en rayons de bille, 0 = elle ne salit pas).")]
        [Min(0f)] public float mudTrailWidth = 1.2f;
        [Tooltip("Rentrer la 8 arrête la partie tout de suite. Le dernier joueur à l'avoir touchée (sa blanche ou une de ses billes) gagne d'abord cette éclaboussure (portée, mètres, même forme que celle d'une bille), puis le plus de tapis lavé gagne.")]
        [Min(0f)] public float eightSplashRadius = 0.6f;

        [Header("Debug")]
        [Tooltip("Lignes [PowerWash] dans la console : billes rentrées, 8, fin de partie.")]
        public bool debugLogs = true;
    }
}
