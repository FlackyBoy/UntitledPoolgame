using System;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // A kind of thing to eat on the felt (HungryHoleProp).
    [Serializable]
    public class HungryHolePropEntry
    {
        [Tooltip("N'importe quel prefab (ses colliders sont retirés).")]
        public GameObject prefab;
        [Tooltip("Largeur de l'objet sur la table : rayon de son emprise, en rayons de bille. Le trou le mange quand il est plus grand que ça.")]
        [Min(0.1f)] public float size = 1f;
        [Tooltip("Part de sa largeur qui doit passer dans le trou, et largeur de sa collision (1 = tout l'objet ; 0,4 = le pied d'un arbre dont le feuillage déborde).")]
        [Range(0.1f, 1f)] public float fit = 1f;
        [Tooltip("Chance relative de le tirer.")]
        [Min(0f)] public float weight = 1f;
    }

    // Hungry Hole: the cue ball is a hole that swallows the balls smaller than
    // itself and grows (HungryHoleCue, HungryHoleRuleSet). Ball sizes follow
    // their number. Loaded from Assets/Resources/HungryHoleSettings.
    [CreateAssetMenu(fileName = "HungryHoleSettings", menuName = "Pool/Hungry Hole Settings")]
    public class HungryHoleSettings : ScriptableObject
    {
        [Header("Taille des billes")]
        [Tooltip("Taille de la plus petite bille de chaque groupe (la 1 et la 9), en part de la taille normale.")]
        [Min(0.3f)] public float smallestBall = 0.7f;
        [Tooltip("Taille de la plus grosse bille de chaque groupe (la 7 et la 15). Les autres sont entre les deux, selon leur numéro.")]
        [Min(0.3f)] public float largestBall = 1.3f;
        [Tooltip("Taille de la 8, plus grosse que toutes : il faut avoir grandi pour la prendre.")]
        [Min(0.3f)] public float eightBall = 1.6f;
        [Tooltip("Une bille plus grosse est plus lourde : masse × taille^N (0 = même masse pour toutes, 3 = selon le volume).")]
        [Range(0f, 3f)] public float massExponent = 2f;

        [Header("Le trou (tailles en multiples du rayon d'une bille normale)")]
        [Tooltip("Taille du trou au début. Une bille n'est avalée que si elle est plus petite que le trou : au-dessus de Smallest Ball, les plus petites passent dès le départ.")]
        [Min(0.1f)] public float startSize = 0.8f;
        [Tooltip("Ce que le trou gagne à chaque bille de son groupe avalée.")]
        [Min(0f)] public float growPerBall = 0.2f;
        [Tooltip("Ce que le trou perd à chaque bille de l'adversaire avalée.")]
        [Min(0f)] public float shrinkPerBall = 0.2f;
        [Tooltip("Taille minimale et maximale du trou.")]
        public Vector2 sizeLimits = new Vector2(0.6f, 3.2f);
        [Tooltip("Changement de taille en ressort : raideur (plus haut = plus rapide).")]
        [Min(1f)] public float growStiffness = 260f;
        [Tooltip("Rebond du ressort (0 = aucun, il s'arrête net ; 0,3 = il dépasse nettement puis rebondit ; 1 = pas de dépassement).")]
        [Range(0.05f, 1f)] public float growBounce = 0.3f;
        [Tooltip("Anneau de croissance : il saute tout de suite à la nouvelle taille (un peu au-delà), épais et lumineux, puis s'estompe pendant que le trou le rattrape. Durée (s), épaisseur (m), dépassement (part du rayon).")]
        [Min(0f)] public float growRingTime = 0.5f;
        [Min(0f)] public float growRingWidth = 0.02f;
        [Range(0f, 1f)] public float growRingOvershoot = 0.15f;
        [Tooltip("Un trou mange l'autre s'il est au moins ce nombre de fois plus gros et passe sur son centre. L'autre revient après le délai de Speed Pool, réduit (Eaten Size Factor).")]
        [Min(1f)] public float eatHoleRatio = 1.2f;
        [Tooltip("Ce que le trou gagne en mangeant l'autre.")]
        [Min(0f)] public float growPerHoleEaten = 0.5f;
        [Tooltip("Taille du trou mangé quand il revient, en part de sa taille d'avant (0,6 = il garde 60 %, sans descendre sous le minimum).")]
        [Range(0.1f, 1f)] public float eatenSizeFactor = 0.6f;
        [Tooltip("Durée (secondes) de la chute d'une bille avalée.")]
        [Min(0.01f)] public float sinkTime = 0.35f;
        [Tooltip("Ralentissement du trou qui glisse (m/s²). Une bille qui roule ralentit d'environ 0,5.")]
        [Min(0.01f)] public float deceleration = 0.6f;
        [Tooltip("Couleur du cercle autour du trou de chaque joueur (J1, J2).")]
        public Color player1Ring = new Color(0.2f, 0.55f, 1f, 1f);
        public Color player2Ring = new Color(1f, 0.25f, 0.2f, 1f);
        [Tooltip("Épaisseur du cercle (mètres). 0 = pas de cercle, juste le disque noir.")]
        [Min(0f)] public float ringWidth = 0.006f;

        [Header("Juice (quand le trou avale quelque chose)")]
        [Tooltip("Le trou gonfle d'un coup puis oscille en revenant : de combien (0,5 = +50 % pour un gros morceau, moins pour un petit). Le son, la vibration, le texte : moments Feel « Hungry Hole Swallow » et « Hungry Hole Ate Hole » dans GameFeelSettings.")]
        [Range(0f, 2f)] public float gulpAmount = 1f;
        [Tooltip("Le disque noir bombe plus fort que son contour : × ce nombre.")]
        [Min(0f)] public float discBump = 1.5f;
        [Tooltip("Durée du « gloups » (secondes).")]
        [Min(0.05f)] public float gulpTime = 0.45f;
        [Tooltip("Le cercle du joueur s'épaissit pendant le « gloups » : ×(1 + ce nombre) au plus fort.")]
        [Min(0f)] public float ringPulse = 3f;
        [Tooltip("Onde de choc : un anneau aux couleurs du joueur part du trou et s'élargit en s'effaçant.")]
        public bool shockwave = true;
        [Tooltip("Jusqu'où va l'onde, en rayons du trou (pour un gros morceau ; moins pour un petit).")]
        [Min(1f)] public float shockwaveReach = 3f;
        [Min(0.05f)] public float shockwaveTime = 0.4f;
        [Tooltip("Épaisseur de l'onde au départ (mètres).")]
        [Min(0f)] public float shockwaveWidth = 0.015f;
        [Tooltip("Faisceau de lumière : une colonne qui jaillit du trou, aux couleurs du joueur, avec une vraie lumière qui éclaire la table.")]
        public bool beam = true;
        [Tooltip("Effet à jouer à la place du faisceau généré (vide = le faisceau généré et sa lumière). Il est créé au centre du trou, à l'échelle du trou (Beam Prefab Scale × son diamètre), le suit, prend la couleur de la bouchée (systèmes de particules), et s'arrête au bout de Beam Time.")]
        public GameObject beamPrefab;
        [Tooltip("Taille de l'effet, en diamètres du trou.")]
        [Min(0f)] public float beamPrefabScale = 1f;
        [Tooltip("Hauteur du faisceau (mètres) pour un gros morceau ; moins pour un petit.")]
        [Min(0f)] public float beamHeight = 4f;
        [Tooltip("Largeur du faisceau, en rayons du trou.")]
        [Min(0.1f)] public float beamWidth = 0.9f;
        [Tooltip("Durée totale du faisceau (secondes) : il jaillit dans le premier quart, puis s'éteint.")]
        [Min(0.05f)] public float beamTime = 0.3f;
        [Tooltip("Couleur du faisceau, du flash du cercle et de la lumière pour un bon morceau (une de ses billes, un objet, la 8 au bon moment, l'autre trou)…")]
        public Color goodBiteColor = Color.white;
        [Tooltip("… et pour un mauvais (une bille de l'adversaire, la 8 trop tôt).")]
        public Color badBiteColor = new Color(1f, 0.12f, 0.08f, 1f);
        [Tooltip("Opacité du faisceau.")]
        [Range(0f, 1f)] public float beamAlpha = 0.75f;
        [Tooltip("Intensité et portée (mètres) d'une lumière qui éclaire la table autour du trou (0 = pas de lumière ; elle déborde sur les côtés).")]
        [Min(0f)] public float beamLightIntensity = 0f;
        [Min(0f)] public float beamLightRange = 0.8f;
        [Tooltip("Secousse de l'écran du joueur pour un gros morceau (la plus forte, pour Shake Full Size et plus).")]
        [Min(0f)] public float shakeMagnitude = 0.12f;
        [Min(0f)] public float shakeDuration = 0.25f;
        [Tooltip("Taille (en rayons de bille) à partir de laquelle l'écran tremble, et taille qui donne la secousse entière.")]
        [Min(0f)] public float shakeMinSize = 0.6f;
        [Min(0.1f)] public float shakeFullSize = 2f;
        [Tooltip("Gros morceau (à partir de cette taille, en rayons de bille) : flash de l'écran du joueur à sa couleur, et arrêt sur image.")]
        [Min(0f)] public float bigBiteSize = 1.2f;
        [Tooltip("Opacité du flash d'un gros morceau (0 = pas de flash).")]
        [Range(0f, 1f)] public float flashAlpha = 0.25f;
        [Min(0f)] public float flashDuration = 0.25f;
        [Tooltip("Arrêt sur image d'un gros morceau (secondes, 0 = aucun). Le temps tourne à 5 % pendant ce temps.")]
        [Min(0f)] public float hitstopTime = 0.07f;
        [Tooltip("Vitesse de rotation d'un objet mangé pendant sa chute (degrés par seconde, elle accélère).")]
        [Min(0f)] public float propSpin = 540f;

        [Header("Objets à manger")]
        [Tooltip("Ce qui peut apparaître sur le tapis. Chaque objet est mis à l'échelle à son apparition d'après sa vraie taille : Size pour sa largeur, Prop Max Height pour sa hauteur.")]
        public HungryHolePropEntry[] props = new HungryHolePropEntry[0];
        [Tooltip("Nombre d'objets posés sur le tapis au début de la partie.")]
        [Min(0)] public int propCount = 10;
        [Tooltip("Hauteur maximale d'un objet, en rayons de bille (un arbre est réduit pour ne pas dépasser).")]
        [Min(0.5f)] public float propMaxHeight = 8f;
        [Tooltip("Ce que le trou gagne en mangeant un objet : sa taille × ce facteur (un objet de taille 2 → +0,3 avec 0,15).")]
        [Min(0f)] public float growPerPropSize = 0.15f;
        [Tooltip("Un objet mangé réapparaît ailleurs, au hasard, après ce délai (secondes, tiré entre les deux valeurs). 0 / 0 = il ne revient pas.")]
        public Vector2 propRespawnDelay = new Vector2(2f, 6f);
        [Tooltip("Durée de l'apparition d'un objet (il surgit du tapis).")]
        [Min(0f)] public float propPopTime = 0.3f;
        [Tooltip("Espace gardé autour d'un objet posé (billes, poches, trous, autres objets), en rayons de bille.")]
        [Min(0f)] public float propClearance = 1.5f;

        [Header("Diagnostic")]
        [Tooltip("Console : chaque bille avalée ou percutée, chaque changement de taille ([HungryHole]).")]
        public bool debugLogs = true;

        // Size of a ball (multiplier of the normal size) from its number.
        public float BallScale(PoolBall ball)
        {
            if (ball.IsCueBall) return 1f;
            if (ball.Group == BallGroup.Eight) return eightBall;
            int rank = ball.Number <= 7 ? ball.Number - 1 : ball.Number - 9;   // 0..6
            return Mathf.Lerp(smallestBall, largestBall, Mathf.Clamp01(rank / 6f));
        }
    }
}
