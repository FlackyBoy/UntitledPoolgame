using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // What speeds the living holes up over a Catch the Hole match.
    public enum CatchTheHoleRamp
    {
        None,      // always Base Speed
        Progress,  // share of the object balls potted
        Timer,     // time since the holes appeared, over Ramp Seconds
        Both,      // whichever of the two is further along
    }

    // Catch the Hole: the living holes on the felt (CatchTheHole, LivingHole).
    // Loaded from Assets/Resources/CatchTheHoleSettings.
    [CreateAssetMenu(fileName = "CatchTheHoleSettings", menuName = "Pool/Catch The Hole Settings")]
    public class CatchTheHoleSettings : ScriptableObject
    {
        [Header("Apparence")]
        [Tooltip("Asset du pouvoir Trou dont le trou vivant reprend l'apparence : forme, bord fondu, braises, decal brûlé, fumée, temps de chute d'une bille. Rempli par Ensure Config Assets Exist.")]
        public TableHolePower look;
        [Tooltip("Garder la fumée du Trou sur les trous vivants.")]
        public bool smoke;

        [Header("Nombre de trous")]
        [Tooltip("Trous au début de la partie.")]
        [Min(1)] public int startHoles = 1;
        [Tooltip("Nombre maximal de trous en même temps.")]
        [Min(1)] public int maxHoles = 3;
        [Tooltip("Un trou de plus toutes les N billes rentrées (poches et trous). 0 = jamais.")]
        [Min(0)] public int addHoleEveryPots = 5;
        [Tooltip("Un trou de plus toutes les N secondes. 0 = jamais.")]
        [Min(0f)] public float addHoleEverySeconds = 0f;
        [Tooltip("Durée (secondes) pendant laquelle un nouveau trou s'ouvre, de rien à sa taille.")]
        [Min(0f)] public float openTime = 0.8f;

        [Header("Taille")]
        [Tooltip("Rayon le plus petit (mètres). Une bille fait ~3 cm de rayon : sous 3 cm, une bille ne tombe que si son centre passe dedans.")]
        [Min(0.01f)] public float minRadius = 0.05f;
        [Tooltip("Rayon le plus grand (mètres).")]
        [Min(0.01f)] public float maxRadius = 0.12f;
        [Tooltip("Vitesse à laquelle la taille change (variations par seconde, environ). 0 = taille fixe, au milieu des deux.")]
        [Min(0f)] public float sizeChangeRate = 0.12f;

        [Header("Déplacement")]
        [Tooltip("Vitesse de croisière (mètres par seconde) au début de la partie.")]
        [Min(0f)] public float baseSpeed = 0.1f;
        [Tooltip("Vitesse de croisière quand l'accélération est au bout.")]
        [Min(0f)] public float maxSpeed = 0.35f;
        [Tooltip("Accélération (m/s²) : plus elle est faible, plus les virages sont larges et les départs doux. Mise à l'échelle avec la vitesse, pour qu'un trou rapide tourne aussi.")]
        [Min(0.01f)] public float acceleration = 0.12f;
        [Tooltip("Distance (mètres) à partir de laquelle le trou ralentit en approchant de sa cible.")]
        [Min(0.01f)] public float arriveDistance = 0.15f;
        [Tooltip("Durée maximale (secondes) d'un trajet avant de choisir une autre cible (s'il n'arrive pas, coincé contre une poche).")]
        [Min(1f)] public float maxLegTime = 8f;

        [Header("Pauses")]
        [Tooltip("Chance que le trou s'arrête un moment en arrivant à sa cible (0 = jamais, 1 = toujours).")]
        [Range(0f, 1f)] public float pauseChance = 0.35f;
        [Tooltip("Durée d'une pause (secondes), tirée entre ces deux valeurs.")]
        public Vector2 pauseTime = new Vector2(1f, 3f);

        [Header("Écarts")]
        [Tooltip("Écart (mètres) gardé entre le bord du trou et les bandes, et entre le bord du trou et les poches.")]
        [Min(0f)] public float edgeClearance = 0.03f;
        [Tooltip("Écart (mètres) gardé entre deux trous, bord à bord.")]
        [Min(0f)] public float holeGap = 0.08f;

        [Header("Accélération au fil de la partie")]
        [Tooltip("Aucune : toujours Base Speed. Déroulé : selon la part des billes de couleur rentrées. Minuteur : selon le temps écoulé (Ramp Seconds). Les deux : le plus avancé des deux.")]
        public CatchTheHoleRamp ramp = CatchTheHoleRamp.Both;
        [Tooltip("Minuteur : secondes de partie pour passer de Base Speed à Max Speed.")]
        [Min(1f)] public float rampSeconds = 300f;

        [Header("Diagnostic")]
        [Tooltip("Console : chaque bille avalée (vitesse, tir en cours, décision), chaque trou ajouté ([CatchTheHole]).")]
        public bool debugLogs = true;
    }
}
