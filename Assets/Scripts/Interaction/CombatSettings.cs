using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // The R&D bare-handed combat (LocalUnarmedMelee with Boxing Punches
    // ticked — the R&D player variant only), in one place:
    // - the punches: animated (PROTOFACTOR clips) or procedural (IK), one
    //   button per hand, the guard after punching, their strength and feel;
    // - what the one hit goes through (LocalHitGauge): hit gauge, stagger,
    //   daze, knock-out, wake-up, hit zones and reactions;
    // - the held guard (block meter, guard break, counter) and the
    //   attacker's stamina.
    // The kick's reach, charge and Spartan launch stay on the component.
    // Assets/Resources/CombatSettings, created by Tools > Pool > Ensure
    // Config Assets Exist; read every frame, so it can be tuned while
    // playing. Tooltips in French: read by the designer in the Inspector.
    [CreateAssetMenu(fileName = "CombatSettings", menuName = "Pool/Combat Settings")]
    public class CombatSettings : ScriptableObject
    {
        [Header("Coups animés (Tools > Pool > R&D > Préparer le joueur de R&D)")]
        [Tooltip("Coups et garde joués par des animations (calque « R&D Combat » du contrôleur d'animation). Décoché = coups et garde procéduraux (IK).")]
        public bool useAnimations = true;
        [Tooltip("Nom du calque du contrôleur d'animation qui porte la garde et les coups.")]
        public string animationLayer = "R&D Combat";
        [Tooltip("Vitesse des animations de coup (1 = normale, plus haut = plus nerveux).")]
        [Range(0.3f, 3f)] public float animationSpeed = 1.3f;
        [Tooltip("Fondu entre deux animations (garde → coup, coup → coup), en secondes.")]
        [Range(0f, 0.3f)] public float animationFade = 0.06f;
        [Tooltip("Fenêtre où le coup peut toucher, en part de l'animation (0 = début, 1 = fin) : début. La console donne, pour chaque coup, le moment où la main va le plus vite (Debug Logs).")]
        [Range(0f, 1f)] public float hitWindowStart = 0.2f;
        [Tooltip("Fenêtre où le coup peut toucher : fin.")]
        [Range(0f, 1f)] public float hitWindowEnd = 0.55f;
        [Tooltip("À partir de ce moment de l'animation, l'autre main peut enchaîner.")]
        [Range(0f, 1f)] public float chainFrom = 0.4f;
        [Tooltip("À ce moment de l'animation, on repart vers la garde (le coup est fini).")]
        [Range(0.1f, 1f)] public float returnAt = 0.85f;
        [Tooltip("Inverse les deux animations si le poing gauche joue le coup du droit.")]
        public bool swapHands;
        [Tooltip("Correction de la direction du poing gauche, en degrés (rotation du buste pendant le coup ; positif = vers la droite). L'animation garde l'orientation de sa garde de profil : la console (Debug Logs) donne après chaque coup l'angle mesuré et la valeur à mettre ici.")]
        [Range(-90f, 90f)] public float punchAimLeft;
        [Tooltip("Correction de la direction du poing droit, en degrés (voir le poing gauche).")]
        [Range(-90f, 90f)] public float punchAimRight;

        [Header("Garde")]
        [Tooltip("Combien de temps les poings restent levés en garde après le dernier coup, en secondes (0 = pas de garde : les bras retombent après chaque coup).")]
        [Min(0f)] public float guardTime = 1.5f;
        [Tooltip("Temps pour lever la garde, en secondes.")]
        [Min(0.01f)] public float guardRaiseTime = 0.08f;
        [Tooltip("Temps pour baisser la garde, en secondes.")]
        [Min(0.01f)] public float guardLowerTime = 0.35f;

        [Header("Poing fermé et poignet")]
        [Tooltip("Repli de chaque phalange des doigts quand le poing est fermé, en degrés (0 = main ouverte).")]
        [Range(0f, 110f)] public float fingerCurl = 85f;
        [Tooltip("Repli du pouce, en degrés.")]
        [Range(0f, 90f)] public float thumbCurl = 40f;
        [Tooltip("Rotation du poignet pendant le coup, en degrés : le poing est vertical en garde et tourne paume vers le bas à l'impact. Négatif si ça tourne dans le mauvais sens.")]
        [Range(-180f, 180f)] public float corkscrew = 80f;
        [Tooltip("Où finissent les poings sur le côté, en largeurs d'épaules : 1 = droit devant chaque épaule (bras parallèles), plus petit = vers l'axe du corps comme un vrai direct.")]
        [Range(0f, 1.5f)] public float fistSpread = 0.45f;

        [Header("Nervosité du poing")]
        [Tooltip("Temps pour amener le poing en garde avant qu'il parte (premier coup, quand la garde n'est pas encore levée), en secondes.")]
        [Min(0f)] public float windupTime = 0.05f;
        [Tooltip("Durée de la frappe, en secondes : plus court = plus sec.")]
        [Min(0.02f)] public float strikeTime = 0.07f;
        [Tooltip("Bras tendu avant le retour, en secondes.")]
        [Min(0f)] public float holdTime = 0.03f;
        [Tooltip("Durée du retour en garde, en secondes : plus court = on enchaîne plus vite.")]
        [Min(0.02f)] public float recoverTime = 0.12f;
        [Tooltip("Forme du coup : 0 = tout droit (direct), 1 = crochet large (l'ancien coup).")]
        [Range(0f, 1f)] public float hookAmount = 0.2f;

        [Header("Le corps derrière le coup")]
        [Tooltip("Rotation du buste vers l'avant côté poing, en degrés.")]
        [Min(0f)] public float torsoTwist = 18f;
        [Tooltip("Avancée de l'épaule qui frappe, en mètres (bras de référence) : plus d'allonge et de poids.")]
        [Min(0f)] public float shoulderDrive = 0.16f;
        [Tooltip("Le corps qui s'engage vers l'avant, en mètres.")]
        [Min(0f)] public float bodyLean = 0.14f;

        [Header("Force")]
        [Tooltip("Déséquilibre (unpin PuppetMaster) d'un coup de poing. Repère : 5 suffit en général à faire tomber.")]
        [Min(0f)] public float unpin = 2f;
        [Tooltip("Force (newtons) d'un coup de poing, appliquée là où il touche.")]
        [Min(0f)] public float force = 650f;

        [Header("Ressenti dans la vue")]
        [Tooltip("La vue roule du côté du poing quand il part, en degrés (0 = rien).")]
        [Min(0f)] public float throwViewRoll = 1.6f;
        [Tooltip("La vue avance un peu avec le coup, en mètres.")]
        [Min(0f)] public float throwViewPush = 0.035f;
        [Tooltip("Durée de ce mouvement de la vue, en secondes.")]
        [Min(0.02f)] public float throwViewTime = 0.16f;
        [Tooltip("Secousse de la vue de celui qui frappe quand le coup touche (0 = aucune).")]
        [Min(0f)] public float hitShake = 1f;

        [Header("Encaissement : titube → sonné → K.O. → réveil")]
        [Tooltip("Jauge d'encaissement sur ceux que les poings de boxe touchent. Décoché = les coups font tomber comme avant (selon la physique).")]
        public bool useHitGauge = true;
        [Tooltip("Ce qu'un coup de poing remplit dans la jauge (1 = pleine).")]
        [Range(0f, 1f)] public float punchDamage = 0.14f;
        [Tooltip("Ce qu'un coup de pied simple remplit (le coup de pied spartiate met K.O. d'office).")]
        [Range(0f, 1f)] public float kickDamage = 0.3f;
        [Tooltip("Tant que la jauge n'est pas pleine, un coup fait seulement tituber : pendant ce temps (secondes) après le coup, le corps tient beaucoup mieux debout.")]
        [Min(0f)] public float staggerTime = 0.7f;
        [Tooltip("Pendant ce temps, résistance des muscles aux coups (PuppetMaster) : plus haut = le coup lâche moins le muscle touché. Le prefab du joueur est à 0 (tout coup lâche le muscle complètement).")]
        [Min(0.01f)] public float staggerResistance = 1f;
        [Tooltip("Pendant ce temps, distance (mètres) dont un muscle peut s'écarter de sa pose sans que le corps perde l'équilibre. Le prefab du joueur est à 0 (le moindre écart fait tomber).")]
        [Min(0f)] public float staggerDrift = 0.6f;
        [Tooltip("Au-dessus de ce remplissage, le joueur est sonné : il ralentit, titube et la vue tangue.")]
        [Range(0f, 1f)] public float dazedFrom = 0.6f;
        [Tooltip("Vitesse de déplacement d'un joueur complètement sonné (1 = normale).")]
        [Range(0.1f, 1f)] public float dazedSpeed = 0.55f;
        [Tooltip("Dérive sur le côté d'un joueur complètement sonné, en m/s (en plus de l'animation : il a du mal à aller droit).")]
        [Min(0f)] public float dazedStagger = 0.25f;
        [Tooltip("Animation « Stunned » sur celui qui est sonné (calques « R&D Sonné », créés par Tools > Pool > R&D > Préparer le joueur de R&D). Tout le corps à l'arrêt, le haut seulement quand il marche.")]
        public bool dazedAnimation = true;
        [Tooltip("Tangage de la vue d'un joueur complètement sonné, en degrés.")]
        [Min(0f)] public float dazedViewSway = 4f;
        [Tooltip("La jauge commence à redescendre ce temps (secondes) après le dernier coup reçu.")]
        [Min(0f)] public float drainDelay = 1.5f;
        [Tooltip("Vitesse à laquelle la jauge redescend ensuite, par seconde.")]
        [Min(0f)] public float drainRate = 0.2f;
        [Tooltip("Durée du K.O. au sol avant de se relever, en secondes.")]
        [Min(0f)] public float knockOutTime = 3f;
        [Tooltip("Après s'être relevé, temps (secondes) pendant lequel les coups ne remplissent pas la jauge : pas de K.O. en boucle.")]
        [Min(0f)] public float wakeProtection = 2f;

        [Header("Garde (maintenir Ctrl gauche / LT)")]
        [Tooltip("La garde : maintenue, elle encaisse les coups de face à la tête et au corps. Ni poing ni pied pendant ce temps.")]
        public bool blockEnabled = true;
        [Tooltip("Vitesse de déplacement en garde (1 = normale). Pas de sprint.")]
        [Range(0.1f, 1f)] public float blockMoveSpeed = 0.6f;
        [Tooltip("Demi-angle (degrés) devant le joueur dans lequel un coup est bloqué : au-delà (de côté, de dos), il passe.")]
        [Range(10f, 180f)] public float blockAngle = 70f;
        [Tooltip("Part de la jauge d'encaissement qu'un coup de poing bloqué remplit encore (0 = rien).")]
        [Range(0f, 1f)] public float blockedDamage = 0.1f;
        [Tooltip("Part qu'un coup de pied bloqué remplit encore : le pied passe en partie.")]
        [Range(0f, 1f)] public float blockedKickDamage = 0.5f;
        [Tooltip("Part de la force et du déséquilibre d'un coup bloqué qui arrive quand même sur le corps.")]
        [Range(0f, 1f)] public float blockedForce = 0.3f;
        [Tooltip("Ce qu'un coup de poing bloqué enlève à la jauge de garde (1 = pleine).")]
        [Range(0f, 1f)] public float blockCostPunch = 0.25f;
        [Tooltip("Ce qu'un coup de pied bloqué enlève à la jauge de garde.")]
        [Range(0f, 1f)] public float blockCostKick = 0.45f;
        [Tooltip("La jauge de garde remonte ce temps (secondes) après le dernier coup bloqué.")]
        [Min(0f)] public float blockRegenDelay = 1f;
        [Tooltip("Vitesse à laquelle la jauge de garde remonte, par seconde.")]
        [Min(0f)] public float blockRegenRate = 0.5f;
        [Tooltip("Garde brisée (jauge de garde vide, ou coup de pied spartiate) : temps sans pouvoir garder, en secondes. Les coups suivants comptent normalement sur la jauge d'encaissement (la rupture elle-même n'y retire rien).")]
        [Min(0f)] public float guardBreakTime = 1.5f;
        [Tooltip("Coup de pied spartiate sur quelqu'un qui garde de face : coché = il brise la garde (comme un coup bloqué, sans K.O. ni projection) ; décoché = il passe à travers la garde (K.O. et projection).")]
        public bool spartanBreaksGuardOnly = true;
        [Tooltip("Contre-attaque : après un coup bloqué, un coup de poing donné dans ce délai (secondes) part plus vite et frappe plus fort.")]
        [Min(0f)] public float counterWindow = 0.6f;
        [Tooltip("Vitesse de l'animation du coup de poing en contre-attaque (multiplicateur).")]
        [Range(1f, 3f)] public float counterSpeed = 1.5f;
        [Tooltip("Jauge remplie et force d'un coup de poing en contre-attaque (multiplicateur).")]
        [Range(1f, 3f)] public float counterDamage = 1.5f;

        [Header("Endurance de celui qui frappe")]
        [Tooltip("Chaque coup (poing, pied) coûte de l'endurance ; sans assez d'endurance, le coup ne part pas. Empêche de spammer.")]
        public bool staminaEnabled = true;
        [Tooltip("Endurance d'un coup de poing (1 = jauge pleine).")]
        [Range(0f, 1f)] public float punchStamina = 0.15f;
        [Tooltip("Endurance d'un coup de pied.")]
        [Range(0f, 1f)] public float kickStamina = 0.3f;
        [Tooltip("En plus, pour un coup de pied spartiate (pris au moment où il part).")]
        [Range(0f, 1f)] public float spartanStamina = 0.25f;
        [Tooltip("L'endurance remonte ce temps (secondes) après le dernier coup donné.")]
        [Min(0f)] public float staminaRegenDelay = 0.6f;
        [Tooltip("Vitesse à laquelle l'endurance remonte, par seconde.")]
        [Min(0f)] public float staminaRegenRate = 0.45f;
        [Tooltip("En dessous de ce niveau, le joueur est fatigué : ses coups de poing sont plus lents et plus faibles.")]
        [Range(0f, 1f)] public float tiredBelow = 0.3f;
        [Tooltip("Vitesse de l'animation d'un coup de poing fatigué (multiplicateur).")]
        [Range(0.3f, 1f)] public float tiredSpeed = 0.75f;
        [Tooltip("Jauge remplie et force d'un coup de poing fatigué (multiplicateur).")]
        [Range(0f, 1f)] public float tiredDamage = 0.7f;

        [Header("Coups localisés")]
        [Tooltip("Jauge remplie par un coup à la tête, multiplicateur.")]
        [Min(0f)] public float headDamage = 1.5f;
        [Tooltip("Jauge remplie par un coup au corps (buste, bassin, bras), multiplicateur.")]
        [Min(0f)] public float bodyDamage = 1f;
        [Tooltip("Jauge remplie par un coup aux jambes, multiplicateur.")]
        [Min(0f)] public float legDamage = 0.7f;
        [Tooltip("Animation de coup reçu (de face, de dos, de gauche, de droite), sur le calque « R&D Coup reçu ». Version lourde pour un coup à la tête ou sur quelqu'un de sonné.")]
        public bool hitReactions = true;
        [Tooltip("Fondu d'entrée et de sortie de l'animation de coup reçu, en secondes.")]
        [Range(0.01f, 0.4f)] public float hitReactionFade = 0.1f;
        [Tooltip("Inverse gauche et droite des animations de coup reçu, si le personnage part du mauvais côté.")]
        public bool hitReactionSwapSides;
        [Tooltip("Au K.O., vitesse (m/s) donnée au corps dans le sens du dernier coup. Tête : il part en arrière, tout mou ; corps : il se plie ; jambes : elles se dérobent.")]
        [Min(0f)] public float knockOutPush = 3f;
        [Tooltip("Force des muscles pendant le K.O. (0 = tout mou, 1 = raide). Rétablie au réveil.")]
        [Range(0f, 1f)] public float knockOutLimp = 0.15f;

        private static CombatSettings instance;
        public static CombatSettings Instance =>
            instance != null ? instance : instance = Pool.PoolSettingsLoader.LoadOrDefault<CombatSettings>("CombatSettings");
    }
}
