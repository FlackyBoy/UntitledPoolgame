using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Hide and seek: one player hides as a ball, the other looks for it with
    // the cue ball (HideAndSeekRuleSet). Distances and speeds are for a table at
    // scale 1 (felt 1.97 m wide, ball radius 5 cm), they follow the table's, as
    // the balls do. Loaded from Resources. The small body and how it moves are
    // Pocket Size's (PocketSizeSettings).
    [CreateAssetMenu(fileName = "HideAndSeekSettings", menuName = "Pool/Hide And Seek Settings")]
    public class HideAndSeekSettings : ScriptableObject
    {
        [Header("Partie")]
        [Tooltip("Points pour gagner la partie. Une manche donne un point au chercheur s'il trouve la bille, au cacheur sinon. Les rôles s'inversent à chaque manche.")]
        [Min(1)] public int pointsToWin = 3;
        [Tooltip("Joueur qui cherche à la première manche (0 = J1, 1 = J2).")]
        [Range(0, 1)] public int firstSeeker = 0;
        [Tooltip("Temps pour se cacher, en secondes : le chercheur ferme les yeux. Arrivé au bout, le cacheur prend la bille la plus proche.")]
        [Min(1f)] public float hideTime = 15f;
        [Tooltip("Temps de la recherche, en secondes. Arrivé au bout, le cacheur gagne la manche.")]
        [Min(5f)] public float searchTime = 90f;
        [Tooltip("Tirs du chercheur par manche. Le dernier tir joué sans trouver la bille, le cacheur gagne la manche.")]
        [Min(1)] public int shotsPerRound = 5;
        [Tooltip("Temps pendant lequel le résultat d'une manche reste affiché, en secondes.")]
        [Min(0f)] public float roundOverTime = 3f;

        [Header("Table")]
        [Tooltip("Billes éparpillées sur la table (toutes les billes de couleur = 15).")]
        [Range(2, 15)] public int ballsOnTable = 15;
        [Tooltip("Distance minimale entre une bille éparpillée et la blanche ou le départ du cacheur, en mètres.")]
        [Min(0f)] public float scatterClearance = 0.3f;
        [Tooltip("Portée pour prendre la place d'une bille quand on est petit, en mètres.")]
        [Min(0.05f)] public float takeReach = 0.3f;

        [Header("Bille cachée et leurres")]
        [Tooltip("Vitesse d'une bille pilotée, en m/s : le cacheur et les leurres roulent exactement de la même façon.")]
        [Min(0.01f)] public float rollSpeed = 0.5f;
        [Tooltip("Accélération d'une bille pilotée, en m/s².")]
        [Min(0.1f)] public float rollAcceleration = 3f;
        [Tooltip("Temps le plus court entre deux billes leurres qui se mettent à rouler, en secondes.")]
        [Min(0.1f)] public float decoyIntervalMin = 2f;
        [Tooltip("Temps le plus long entre deux billes leurres qui se mettent à rouler, en secondes.")]
        [Min(0.1f)] public float decoyIntervalMax = 5f;
        [Tooltip("Durée la plus courte du roulement d'un leurre, en secondes.")]
        [Min(0.1f)] public float decoyRollMin = 1f;
        [Tooltip("Durée la plus longue du roulement d'un leurre, en secondes.")]
        [Min(0.1f)] public float decoyRollMax = 3f;

        [Header("Ruses du cacheur")]
        [Tooltip("Changements de bille par manche.")]
        [Min(0)] public int swapsPerRound = 2;
        [Tooltip("Portée d'un changement de bille, en mètres de centre à centre, dans la direction du stick (sans direction : la bille la plus proche). Courte, pour que le cacheur sache où il arrive sans rien afficher : deux billes qui se touchent sont à 0,1 m.")]
        [Min(0.05f)] public float swapReach = 0.2f;
        [Tooltip("Fausses pistes par manche.")]
        [Min(0)] public int fakesPerRound = 2;
        [Tooltip("Portée d'une fausse piste, en mètres, dans la direction du stick (sans direction : la bille la plus proche).")]
        [Min(0.05f)] public float fakeReach = 1f;
        [Tooltip("Durée du roulement d'une fausse piste, en secondes.")]
        [Min(0.1f)] public float fakeRollTime = 3f;
        [Tooltip("Temps le plus court entre un changement de bille et le message au chercheur, en secondes. Immédiat, le message montrerait quelle bille vient de démarrer.")]
        [Min(0f)] public float swapAlertDelayMin = 1f;
        [Tooltip("Temps le plus long entre un changement de bille et le message au chercheur, en secondes.")]
        [Min(0f)] public float swapAlertDelayMax = 3f;
        [Tooltip("Temps pendant lequel le message reste sur la plaque du chercheur, en secondes.")]
        [Min(0.1f)] public float swapAlertTime = 2.5f;
        [Tooltip("Durée de la vibration qui confirme une ruse à la manette, en secondes.")]
        [Min(0f)] public float rumbleTime = 0.2f;
        [Tooltip("Force de cette vibration (0 à 1).")]
        [Range(0f, 1f)] public float rumbleStrength = 0.5f;

        [Header("Secousse")]
        [Tooltip("Temps le plus court entre deux secousses, en secondes.")]
        [Min(1f)] public float shakeIntervalMin = 15f;
        [Tooltip("Temps le plus long entre deux secousses, en secondes.")]
        [Min(1f)] public float shakeIntervalMax = 25f;
        [Tooltip("Vitesse la plus forte donnée à chaque bille par une secousse, en m/s.")]
        [Min(0f)] public float shakeSpeed = 0.7f;
        [Tooltip("Force de la secousse de la caméra du chercheur (0 = aucune).")]
        [Min(0f)] public float shakeCamera = 0.02f;
        [Tooltip("Durée de la secousse de la caméra, en secondes.")]
        [Min(0f)] public float shakeCameraTime = 0.4f;

        [Header("Vue du cacheur")]
        [Tooltip("Champ de vision de la vue du dessus, en degrés.")]
        [Range(10f, 90f)] public float cameraFov = 40f;
        [Tooltip("Marge autour de la table dans la vue du dessus (1,1 = 10 % de plus).")]
        [Min(1f)] public float cameraMargin = 1.15f;
        [Tooltip("Hauteur au-dessus du tapis sous laquelle la caméra voit encore, en mètres : la lampe et le plafond, plus haut, ne sont pas dessinés.")]
        [Min(0.05f)] public float cameraClearAbove = 0.5f;

        [Header("Textes")]
        public string closeEyesLabel = "Ferme les yeux !";
        public string hideLabel = "Cache-toi !";
        public string seekerLabel = "Chercheur";
        public string hiderLabel = "Cacheur";
        [Tooltip("{0} : secondes restantes.")]
        public string seekerWaitFormat = "le cacheur se cache : {0} s";
        [Tooltip("{0} : secondes restantes.")]
        public string hideFormat = "E / Y : prendre la place d'une bille : {0} s";
        [Tooltip("{0} : secondes restantes.")]
        public string hiddenFormat = "ne bouge plus trop : {0} s";
        [Tooltip("{0} : temps restant, {1} : tirs restants, {2} : changements de bille restants au cacheur, à ce que le chercheur en sait.")]
        public string searchFormat = "{0}  ·  {1} tirs  ·  changements : {2}";
        public string swapAlertLabel = "Il a changé de bille !";
        [Tooltip("{0} : temps restant, {1} : changements restants, {2} : fausses pistes restantes.")]
        public string hiderFormat = "{0}  ·  changer : {1}  ·  fausse piste : {2}";
        public string foundText = "Trouvé !";
        public string notFoundText = "Pas trouvé !";
        [Tooltip("{0} : points du joueur, {1} : points de l'autre.")]
        public string scoreFormat = "{0} - {1}";

        [Header("Diagnostic")]
        [Tooltip("Lignes [HideAndSeek] dans la console : manches, tirs, ruses, secousses.")]
        public bool debugLogs = true;
    }
}
