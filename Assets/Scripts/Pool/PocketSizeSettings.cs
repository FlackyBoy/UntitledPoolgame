using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Pocket Size: one player shrunk on the felt, the other hunts them with
    // the cue ball (PocketSizeRuleSet). Loaded from Resources.
    [CreateAssetMenu(fileName = "PocketSizeSettings", menuName = "Pool/Pocket Size Settings")]
    public class PocketSizeSettings : ScriptableObject
    {
        [Header("Manches")]
        [Tooltip("Temps que le petit doit tenir pour gagner la manche, en secondes.")]
        [Min(5f)] public float roundDuration = 120f;
        [Tooltip("Touches qui font perdre le petit.")]
        [Min(1)] public int hitsToLose = 3;
        [Tooltip("Compte à rebours avant la chasse, en secondes : le petit est posé sur la table, le grand ne peut pas encore tirer.")]
        [Min(0f)] public float countdown = 3f;
        [Tooltip("Temps pendant lequel le résultat d'une manche reste affiché avant la suivante, en secondes.")]
        [Min(0f)] public float roundOverTime = 4f;
        [Tooltip("Joueur qui est le grand dans la première manche (0 = J1, 1 = J2). Les rôles s'inversent à la deuxième.")]
        [Range(0, 1)] public int firstGiant = 0;

        [Header("Le petit")]
        [Tooltip("Taille du petit par rapport à la normale (0,07 ≈ 12 cm : une bille lui arrive à mi-cuisse).")]
        [Range(0.02f, 0.5f)] public float tinyScale = 0.07f;
        [Tooltip("Collisions entre le petit et les billes : elles le bloquent, rebondissent sur lui, il s'en sert comme abri. Décoché = il les traverse, seule la touche compte. (Son ragdoll est coupé tant qu'il est petit.)")]
        public bool tinyBlocksBalls = true;
        [Tooltip("Hauteur de saut du petit quand son personnage n'a pas le déplacement R&D, en mètres à taille normale (multipliée par Tiny Scale).")]
        [Min(0f)] public float tinyJumpHeight = 1.2f;
        [Tooltip("Plan proche de la caméra du petit, en mètres. Trop grand : le tapis et les billes sont coupés devant lui.")]
        [Min(0.0005f)] public float tinyNearClip = 0.005f;

        [Header("Touches")]
        [Tooltip("Vitesse minimale d'une bille (la blanche ou une bille de couleur) pour qu'elle compte comme une touche, en m/s. Seule une bille qui va vers le petit compte, pas celle qu'il pousse.")]
        [Min(0f)] public float minHitSpeed = 0.25f;
        [Tooltip("Marge ajoutée au contact blanche / petit, en mètres (la blanche rebondit sur lui avant de le traverser).")]
        [Min(0f)] public float hitMargin = 0.012f;
        [Tooltip("Après une touche ou une chute, le petit ne peut plus être touché pendant ce temps, en secondes.")]
        [Min(0f)] public float invulnerableTime = 1.5f;
        [Tooltip("Vitesse de projection du petit touché, pour 1 m/s de bille qui arrive sur lui (de face ; de biais, moins). 1,5 = un peu plus vite que la bille, comme un corps léger percuté.")]
        [Min(0f)] public float knockTransfer = 1.5f;
        [Tooltip("Vitesse de projection maximale, en m/s (vraie échelle de la table).")]
        [Min(0f)] public float knockMaxSpeed = 3f;
        [Tooltip("Direction de la projection : 0 = dans le sens où roulait la bille, 1 = à l'opposé du point d'impact (une bille qui l'effleure le pousse de côté).")]
        [Range(0f, 1f)] public float knockAlongImpact = 0.6f;
        [Tooltip("Part de la projection vers le haut (0 = à plat, 1 = autant vers le haut que vers l'avant).")]
        [Range(0f, 1.5f)] public float knockLift = 0.35f;
        [Tooltip("Freinage de la projection, par seconde (plus haut = il s'arrête plus vite, plus près).")]
        [Min(0.1f)] public float knockDamping = 3f;
        [Tooltip("Le petit est tombé (poche, hors de la table) quand ses pieds passent cette distance sous le tapis, en mètres. Compte une touche, il revient sur la table.")]
        [Min(0.01f)] public float fallDepth = 0.06f;

        [Header("Table")]
        [Tooltip("Billes de couleur laissées sur la table, éparpillées : des obstacles pour le grand, des abris pour le petit.")]
        [Range(0, 15)] public int obstacleBalls = 6;
        [Tooltip("Distance minimale entre une bille éparpillée et la blanche ou le départ du petit, en mètres.")]
        [Min(0f)] public float obstacleClearance = 0.2f;
        [Tooltip("Une bille de couleur rentrée revient sur la table au bout de ce temps, en secondes.")]
        [Min(0f)] public float respotDelay = 1.5f;
        [Tooltip("Couleur de la blanche du grand.")]
        public Color giantCueColor = Color.white;

        [Header("Pouvoirs")]
        [Tooltip("Couleur des caisses de pouvoirs du grand. Le petit qui passe dessus la détruit.")]
        public Color giantCrateColor = new Color(0.95f, 0.3f, 0.2f);
        [Tooltip("Couleur des caisses de pouvoirs du petit. La blanche du grand qui passe dessus la détruit.")]
        public Color tinyCrateColor = new Color(0.25f, 0.8f, 1f);
        [Tooltip("Distance de ramassage d'une caisse par le petit, en mètres depuis son centre (les caisses sont faites pour la blanche).")]
        [Min(0.01f)] public float tinyCrateReach = 0.09f;

        [Header("Textes (plaque du HUD)")]
        public string giantLabel = "GRAND";
        public string tinyLabel = "PETIT";
        [Tooltip("{0} = manche, {1} = nombre de manches.")]
        public string roundFormat = "Manche {0}/{1}";
        [Tooltip("{0} = secondes avant la chasse.")]
        public string countdownFormat = "Prêt ? {0}";
        [Tooltip("{0} = touches du petit, {1} = touches pour perdre.")]
        public string hitsFormat = "touché {0}/{1}";
        public string roundWonText = "Manche gagnée !";
        public string roundLostText = "Manche perdue";

        [Header("Diagnostic")]
        [Tooltip("Logs [PocketSize] : manches, touches, chutes, caisses, pouvoirs.")]
        public bool debugLogs = true;
    }
}
