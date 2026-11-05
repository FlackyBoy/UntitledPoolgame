using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Shooter miniature: both players shrunk on the felt, shooting at each
    // other (ShooterMiniRuleSet). The body itself is Pocket Size's: its Tiny
    // Prefab and tiny settings (PocketSizeSettings). Loaded from Resources.
    [CreateAssetMenu(fileName = "ShooterMiniSettings", menuName = "Pool/Shooter Mini Settings")]
    public class ShooterMiniSettings : ScriptableObject
    {
        [Header("Partie")]
        [Tooltip("Éliminations pour gagner la partie.")]
        [Min(1)] public int eliminationsToWin = 5;
        [Tooltip("Compte à rebours avant le combat, en secondes : les joueurs sont posés sur la table, personne ne peut tirer.")]
        [Min(0f)] public float countdown = 3f;
        [Tooltip("Temps entre la dernière élimination et l'écran de fin, en secondes.")]
        [Min(0f)] public float endDelay = 2f;

        [Header("Vie")]
        [Tooltip("Vie d'un joueur, rendue à chaque réapparition.")]
        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("Vie perdue par tir reçu.")]
        [Min(0f)] public float shotDamage = 20f;
        [Tooltip("Vie perdue quand une bille arrive sur le joueur. Il tombe aussi en ragdoll.")]
        [Min(0f)] public float ballDamage = 40f;
        [Tooltip("Vie perdue en tombant dans une poche ou hors de la table. Le joueur revient sur le tapis.")]
        [Min(0f)] public float fallDamage = 30f;
        [Tooltip("Vie perdue par un Spartan kick reçu. Un coup de pied simple en retire environ la moitié, selon sa charge.")]
        [Min(0f)] public float kickDamage = 60f;
        [Tooltip("Temps à terre après une élimination, avant de réapparaître, en secondes.")]
        [Min(0f)] public float respawnDelay = 3f;
        [Tooltip("Après une réapparition, le joueur ne perd pas de vie pendant ce temps, en secondes.")]
        [Min(0f)] public float respawnSafeTime = 1.5f;
        [Tooltip("Distance minimale entre un joueur qui réapparaît et l'autre joueur, en mètres.")]
        [Min(0f)] public float respawnClearance = 0.5f;
        [Tooltip("Le joueur est tombé (poche, hors de la table) quand ses pieds passent cette distance sous le tapis, en mètres.")]
        [Min(0.01f)] public float fallDepth = 0.0432f;

        [Tooltip("Distance à laquelle un joueur ramasse une caisse de pouvoir en marchant dessus, en mètres.")]
        [Min(0.001f)] public float crateReach = 0.06f;
        [Tooltip("Couleur du viseur et du nom du pouvoir quand un pouvoir activé attend le prochain tir.")]
        public Color armedCrosshairColor = new Color(1f, 0.8f, 0.15f);
        [Tooltip("Couleur du viseur quand le pouvoir activé ne peut pas partir (grappin : rien à portée). Le tir ne part pas et le pouvoir est gardé.")]
        public Color armedNoTargetColor = new Color(1f, 0.2f, 0.15f);

        [Header("Queue-fusil")]
        [Tooltip("Temps minimal entre deux tirs, en secondes.")]
        [Min(0.02f)] public float fireInterval = 0.25f;
        [Tooltip("Vitesse d'un tir, en m/s. Il va tout droit (pas de gravité) et rebondit.")]
        [Min(0.1f)] public float shotSpeed = 3f;
        [Tooltip("Rayon d'une petite bille tirée, en mètres.")]
        [Min(0.001f)] public float shotRadius = 0.006f;
        [Tooltip("Masse d'une petite bille tirée, en kg (une bille de la table : 0,17). Plus haut = les tirs déplacent plus les billes.")]
        [Min(0.001f)] public float shotMass = 0.03f;
        [Tooltip("Rebond d'un tir (0 = aucun, 1 = sans perte).")]
        [Range(0f, 1f)] public float shotBounciness = 0.8f;
        [Tooltip("Rebonds avant qu'un tir disparaisse.")]
        [Min(0)] public int shotBounces = 3;
        [Tooltip("Durée de vie maximale d'un tir, en secondes.")]
        [Min(0.1f)] public float shotLifetime = 2f;
        [Tooltip("Marge ajoutée au contact tir / joueur, en mètres.")]
        [Min(0f)] public float shotHitMargin = 0.004f;
        [Tooltip("La queue tenue : ses meshes sont copiés à la taille du corps, sans sa physique.")]
        public GameObject cuePrefab;
        [Tooltip("Aim IK : part que prend la colonne pour pointer la queue vers le viseur. Plus haut = le buste se tourne plus, le bras moins.")]
        [Range(0f, 1f)] public float aimSpineWeight = 0.25f;
        [Tooltip("Aim IK : part que prend le bras droit pour pointer la queue vers le viseur. Plus bas = le poignet plie plus pour finir la visée.")]
        [Range(0f, 1f)] public float aimArmWeight = 0.6f;
        [Tooltip("Distance entre les deux mains sur la queue, en mètres à taille normale. Trop grand, le bras gauche n'arrive plus jusqu'à la queue.")]
        [Min(0f)] public float cueHandSpan = 0.25f;
        [Tooltip("Déplacement de la main droite par rapport à l'animation, en mètres à taille normale : X vers la droite du joueur, Y vers le haut, Z vers l'avant. Sort les bras du ventre. Trop loin, les bras n'atteignent plus la queue. Se règle pendant le jeu.")]
        public Vector3 cueHandOffset = new Vector3(-0.08f, 0.08f, 0.28f);
        [Tooltip("Distance entre la main droite et le talon de la queue, en mètres à taille normale. Plus petit = moins de queue derrière la main, qui rentre dans le corps.")]
        [Min(0f)] public float cueButtMargin = 0.06f;
        [Tooltip("Avance de la caméra, en mètres à taille normale. Plus haut = on voit moins l'intérieur du personnage.")]
        public float cameraForward = 0.12f;
        [Tooltip("Décalage de la queue vers l'intérieur de la paume, en mètres à taille normale.")]
        public float cuePalmOffset = 0.03f;
        [Tooltip("Distance visée par la queue et portée de la recherche du point visé, en mètres.")]
        [Min(0.1f)] public float cueAimDistance = 3f;
        [Tooltip("Couleur des tirs de chaque joueur (J1, J2).")]
        public Color[] playerColors = { new Color(0.25f, 0.55f, 1f), new Color(1f, 0.3f, 0.25f) };

        [Header("Réaction à un tir")]
        [Tooltip("Force du maintien sur l'animation juste après un tir, sur le muscle touché et tout ce qui s'y rattache (1 = aucun relâchement). Plus bas = le corps part plus loin. Reste au-dessus de 0,5, le seuil où le joueur tomberait.")]
        [Range(0.51f, 1f)] public float shotPin = 0.55f;
        [Tooltip("Vitesse donnée au muscle touché par un tir, en m/s à taille normale ; la moitié au reste de la chaîne, jusqu'aux hanches. Plus haut = le corps part plus loin.")]
        [Min(0f)] public float shotImpactSpeed = 5f;
        [Tooltip("Durée pendant laquelle le modèle suit le ragdoll après un tir, en secondes. Le reste du temps, debout, il suit l'animation seule.")]
        [Min(0.25f)] public float shotReactionTime = 0.7f;
        [Tooltip("Vitesse de projection quand un tir fait tomber le joueur, en m/s, dans le sens du tir.")]
        [Min(0f)] public float shotKnockSpeed = 0.35f;

        [Header("Animations")]
        [Tooltip("Temps de fondu des animations de l'arme, en secondes.")]
        [Min(0f)] public float animationFade = 0.15f;
        [Tooltip("Durée de l'animation de mise en joue, au début de la partie et en se relevant, en secondes.")]
        [Min(0f)] public float readyTime = 1f;
        [Tooltip("Temps pendant lequel l'animation de tir continue après le dernier tir, en secondes.")]
        [Min(0f)] public float shootHold = 0.15f;

        [Header("Coup de pied dans une bille")]
        [Tooltip("Vitesse donnée à une bille par un coup de pied simple, en m/s.")]
        [Min(0f)] public float ballKickSpeed = 0.8f;
        [Tooltip("Vitesse donnée à une bille par un Spartan kick à pleine charge, en m/s.")]
        [Min(0f)] public float spartanBallKickSpeed = 1.6f;
        [Tooltip("Force de la projection d'un joueur par un Spartan kick, en part de celle d'un joueur de taille normale (déjà à l'échelle du corps).")]
        [Range(0f, 2f)] public float spartanLaunchScale = 1f;

        [Header("Billes")]
        [Tooltip("Billes de la table laissées sur le tapis, éparpillées : des abris à déplacer et des projectiles.")]
        [Range(0, 15)] public int coverBalls = 8;
        [Tooltip("Taille des billes pendant ce mode, en multiple de leur taille normale. 3 ≈ la hauteur d'un joueur : on peut se cacher derrière.")]
        [Min(0.1f)] public float coverBallScale = 3f;
        [Tooltip("Masse des billes selon leur taille : masse × taille ^ ceci. 1 = les tirs les déplacent bien, 2 = les tirs les poussent à peine, 3 = masse réelle, seuls les coups de pied les bougent.")]
        [Min(0f)] public float coverBallMassExponent = 2f;
        [Tooltip("Distance minimale entre une bille éparpillée et le départ d'un joueur, en mètres.")]
        [Min(0f)] public float coverClearance = 0.144f;
        [Tooltip("Une bille rentrée revient sur la table au bout de ce temps, en secondes.")]
        [Min(0f)] public float respotDelay = 1.5f;
        [Tooltip("Vitesse minimale d'une bille pour qu'elle blesse un joueur, en m/s. Seule une bille qui va vers lui compte.")]
        [Min(0f)] public float minBallHitSpeed = 0.18f;
        [Tooltip("Marge ajoutée au contact bille / joueur, en mètres (la bille rebondit sur lui avant de le traverser).")]
        [Min(0f)] public float ballHitMargin = 0.00864f;
        [Tooltip("Après un choc de bille, le joueur ne peut pas être blessé par une autre bille pendant ce temps, en secondes.")]
        [Min(0f)] public float ballSafeTime = 1.5f;
        [Tooltip("Vitesse de projection, en m/s, pour 1 m/s de bille qui arrive sur le joueur.")]
        [Min(0f)] public float knockTransfer = 0.4f;
        [Tooltip("Vitesse de projection maximale, en m/s.")]
        [Min(0f)] public float knockMaxSpeed = 0.46f;
        [Tooltip("Part de la projection vers le haut (0 = à plat, 1 = autant vers le haut que vers l'avant).")]
        [Range(0f, 1.5f)] public float knockLift = 0.35f;

        [Header("Shooter Billard (pas de vie : un tir fait perdre du temps)")]
        [Tooltip("Temps pendant lequel un joueur touché avance moins vite, en secondes.")]
        [Min(0f)] public float hitSlowTime = 2.5f;
        [Tooltip("Vitesse d'un joueur ralenti, en part de sa vitesse normale.")]
        [Range(0.1f, 1f)] public float hitSlowMultiplier = 0.6f;
        [Tooltip("Coups reçus d'affilée qui font tomber le joueur.")]
        [Min(1)] public int hitsToFall = 4;
        [Tooltip("Deux coups séparés de plus que ce temps ne comptent plus comme d'affilée, en secondes.")]
        [Min(0f)] public float hitWindow = 3f;

        [Header("Textes (plaque du HUD)")]
        [Tooltip("{0} = vie restante.")]
        public string healthFormat = "Vie {0}";
        [Tooltip("{0} = secondes avant le combat.")]
        public string countdownFormat = "Prêt ? {0}";
        [Tooltip("{0} = tes éliminations, {1} = celles de l'autre, {2} = éliminations pour gagner.")]
        public string scoreFormat = "{0} - {1}  ·  premier à {2}";
        public string eliminatedText = "Éliminé !";
        public string solidsLabel = "Pleines";
        public string stripesLabel = "Rayées";
        [Tooltip("Shooter Billard. {0} = tes billes restantes, {1} = celles de l'autre.")]
        public string ballsLeftFormat = "{0} à rentrer  ·  l'autre : {1}";

        [Header("Diagnostic")]
        [Tooltip("Logs [ShooterMini] : placement, tirs reçus, chocs, chutes, éliminations.")]
        public bool debugLogs = true;
    }
}
