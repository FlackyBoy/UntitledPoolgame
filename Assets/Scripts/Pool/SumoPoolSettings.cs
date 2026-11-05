using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Sumo Pool: each player is a ball on a small square table, pushing the
    // other into the pockets (SumoPoolRuleSet). Distances and speeds are for a
    // table at scale 1 (felt 1.97 m wide, ball radius 5 cm), they follow the
    // table's, as the balls do. Loaded from Resources.
    [CreateAssetMenu(fileName = "SumoPoolSettings", menuName = "Pool/Sumo Pool Settings")]
    public class SumoPoolSettings : ScriptableObject
    {
        [Header("Partie")]
        [Tooltip("Points pour gagner la partie. Une bille qui tombe donne un point à l'autre joueur, même si elle est tombée toute seule.")]
        [Min(1)] public int pointsToWin = 3;
        [Tooltip("Compte à rebours avant chaque manche, en secondes : les billes ne bougent pas, on peut déjà regarder autour.")]
        [Min(0f)] public float countdown = 2f;
        [Tooltip("Temps entre une chute et la manche suivante, en secondes.")]
        [Min(0f)] public float roundEndDelay = 1.5f;
        [Tooltip("Temps entre la dernière chute et l'écran de fin, en secondes.")]
        [Min(0f)] public float endDelay = 2f;
        [Tooltip("Durée de la chute dans le trou, en secondes.")]
        [Min(0.01f)] public float sinkTime = 0.5f;

        [Header("Table")]
        [Tooltip("Côté du tapis carré, en mètres. Jamais plus que la largeur de la vraie table, qui reste invisible en dessous.")]
        [Min(0.3f)] public float tableSide = 1.7f;
        [Tooltip("Rayon des six trous, en mètres. Une bille tombe quand son centre passe au-dessus du bord.")]
        [Min(0.01f)] public float holeRadius = 0.13f;
        [Tooltip("Largeur des bandes, en mètres.")]
        [Min(0.01f)] public float railWidth = 0.1f;
        [Tooltip("Hauteur des bandes au-dessus du tapis, en mètres.")]
        [Min(0.01f)] public float railHeight = 0.09f;
        [Tooltip("Rebond sur les bandes (0 = aucun, 1 = sans perte).")]
        [Range(0f, 1f)] public float railBounciness = 0.85f;
        public Color feltColor = new Color(0.72f, 0.16f, 0.3f);
        public Color railColor = new Color(0.16f, 0.09f, 0.06f);

        [Header("Billes")]
        [Tooltip("Taille des billes des joueurs (1 = une bille de la table).")]
        [Min(0.2f)] public float ballScale = 1.5f;
        public Color player1Color = new Color(0.2f, 0.5f, 1f);
        public Color player2Color = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Accélération de la bille, en m/s². Sert aussi à tourner et à freiner en poussant à l'opposé : à la vitesse de roulage, la bille va où on la pousse.")]
        [Min(0f)] public float acceleration = 10f;
        [Tooltip("Vitesse maximale en roulant, en m/s. Un dash ou un choc peuvent la dépasser.")]
        [Min(0f)] public float maxSpeed = 1.9f;
        [Tooltip("Ralentissement d'une bille lancée à deux fois Max Speed ou plus (dash, choc), en m/s². Plus haut = elle va moins loin. Il rejoint Braking en douceur à mesure qu'elle ralentit.")]
        [Min(0f)] public float rollingFriction = 1.8f;
        [Tooltip("Freinage à Max Speed quand on ne pousse plus, en m/s². Il diminue ensuite avec la vitesse, pour une fin de glisse progressive. Plus haut = la bille s'arrête plus court, moins de glisse.")]
        [Min(0f)] public float braking = 8f;
        [Tooltip("Freinage minimal, tout à la fin de la glisse, en m/s². Plus bas = la bille traîne plus longtemps avant de s'arrêter. À 0, elle ne s'arrête jamais tout à fait.")]
        [Min(0f)] public float minBraking = 0.3f;
        [Tooltip("Vitesse ajoutée par un dash léger (appui bref), en m/s.")]
        [Min(0f)] public float dashSpeed = 1.4f;
        [Tooltip("Vitesse ajoutée par un dash chargé à fond, en m/s.")]
        [Min(0f)] public float chargedDashSpeed = 4f;
        [Tooltip("Temps à maintenir le bouton pour une charge complète, en secondes. La vitesse monte peu à peu entre les deux dashs, et la bille blanchit.")]
        [Min(0f)] public float dashChargeTime = 0.7f;
        [Tooltip("Temps entre deux dashs, en secondes, à partir du moment où on lâche.")]
        [Min(0f)] public float dashCooldown = 1f;
        [Tooltip("Force du choc entre les deux billes : part de la vitesse de l'autre ajoutée à celle qu'on reçoit déjà du rebond (0 = un choc de billard pur).")]
        [Min(0f)] public float bumpBoost = 0.3f;

        [Header("Sumo Billard")]
        [Tooltip("Temps avant de revenir sur la table après une chute, en secondes (jamais moins que Sink Time).")]
        [Min(0f)] public float fallRespawnDelay = 1.5f;
        [Tooltip("Une chute fait ressortir la dernière bille rentrée du joueur qui tombe.")]
        public bool fallReturnsBall = true;
        [Tooltip("Écart entre les billes au départ, en diamètres (1 = elles se touchent).")]
        [Min(1f)] public float rackSpacing = 1.15f;
        [Tooltip("Ralentissement des billes à rentrer, en m/s². Plus haut = elles s'arrêtent plus vite. La table normale : 0,36.")]
        [Min(0f)] public float tableBallFriction = 0.8f;
        public string solidsLabel = "Pleines";
        public string stripesLabel = "Rayées";
        public string billiardGoal = "Rentre toutes tes billes";
        [Header("Caméra")]
        [Tooltip("Distance de la caméra à la bille, en mètres.")]
        [Min(0.05f)] public float cameraDistance = 0.95f;
        [Tooltip("Inclinaison de la caméra au départ, en degrés (90 = vue de dessus).")]
        [Range(0f, 89f)] public float cameraPitch = 35f;
        [Range(0f, 89f)] public float minCameraPitch = 10f;
        [Range(0f, 89f)] public float maxCameraPitch = 75f;
        [Tooltip("Vitesse à laquelle la caméra rattrape la bille (plus haut = plus sec).")]
        [Min(0.1f)] public float cameraFollow = 12f;
        [Range(10f, 120f)] public float cameraFov = 60f;
        [Tooltip("Plan proche de la caméra, en mètres.")]
        [Min(0.001f)] public float cameraNearClip = 0.03f;
        [Min(0f)] public float mouseSensitivity = 0.1f;
        [Tooltip("En degrés par seconde.")]
        [Min(0f)] public float stickSensitivity = 150f;

        [Header("Textes")]
        [Tooltip("Plaque du HUD. {0} = points du joueur, {1} = points de l'autre.")]
        public string scoreFormat = "{0} - {1}";
        [Tooltip("{0} = points pour gagner.")]
        public string goalFormat = "Premier à {0}";
        [Tooltip("{0} = secondes restantes.")]
        public string countdownFormat = "Départ dans {0}";

        [Header("Diagnostic")]
        [Tooltip("Lignes [SumoPool] dans la console : joueurs posés, chutes, score.")]
        public bool debugLogs = true;
    }
}
