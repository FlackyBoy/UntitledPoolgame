using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // How powers are handed out: the pool to draw from, the colour of each
    // category (crates and ball glow), and the timings of crates and ball
    // rotation.
    [CreateAssetMenu(fileName = "PoolPowerSpawnSettings", menuName = "Pool/Power Spawn Settings")]
    public class PoolPowerSpawnSettings : ScriptableObject
    {
        [Header("Power pool")]
        // Drawn from for the crates and the balls. Empty = nothing spawns.
        public PoolPower[] availablePowers;

        // A random power of the pool allowed in this mode (PoolPower.AvailableIn);
        // null if none is.
        public PoolPower PickPower(PoolGameMode mode)
        {
            if (availablePowers == null) return null;
            int count = 0;
            foreach (PoolPower power in availablePowers)
                if (Offered(power, mode)) count++;
            if (count == 0) return null;
            int pick = Random.Range(0, count);
            foreach (PoolPower power in availablePowers)
            {
                if (!Offered(power, mode)) continue;
                if (pick-- == 0) return power;
            }
            return null;
        }

        // Pocket Size has its own powers (one set per role), and the others
        // only when Pocket Size is ticked in their Only In Modes: most are
        // about pockets and groups it doesn't have. Its own powers come out
        // nowhere else (PocketSizePower.AvailableIn).
        private static bool Offered(PoolPower power, PoolGameMode mode) =>
            power != null && power.AvailableIn(mode)
            && (mode != PoolGameMode.PocketSize || power is PocketSizePower || power.ListsMode(mode));

        [Header("Color code")]
        public Color attackColor = new Color(0.9f, 0.2f, 0.2f);
        public Color defenseColor = new Color(0.25f, 0.55f, 1f);
        public Color effectColor = new Color(0.3f, 0.9f, 0.4f);

        public Color GetColor(PowerType type) => type switch
        {
            PowerType.Attack => attackColor,
            PowerType.Defense => defenseColor,
            PowerType.Effect => effectColor,
            _ => Color.white,
        };

        [Header("Power crates")]
        // Crates on the table at once, on PoolPowerSpawnPoint markers.
        public int activeCrateCount = 3;
        // Random real-world delay range before a picked-up crate respawns
        // (new location, freshly rolled power).
        public float crateRespawnMinDelay = 15f;
        public float crateRespawnMaxDelay = 30f;

        [Header("Crate visuals — optional, one prefab per PowerType")]
        // Optional crate model per category; empty = tinted placeholder cube.
        public GameObject attackCratePrefab;
        public GameObject defenseCratePrefab;
        public GameObject effectCratePrefab;

        public GameObject GetCratePrefab(PowerType type) => type switch
        {
            PowerType.Attack => attackCratePrefab,
            PowerType.Defense => defenseCratePrefab,
            PowerType.Effect => effectCratePrefab,
            _ => null,
        };

        [Header("Power ball rotation")]
        // The ball carrying a power changes at a random interval in this range.
        public float ballRotationMinInterval = 20f;
        public float ballRotationMaxInterval = 40f;
        // Depends on URP Physical Light Units, like the pot halo.
        public float ballGlowIntensity = 4f;
        public float ballGlowRange = 0.3f;

        [Header("Power ball visuals — optional, one material per PowerType")]
        // Optional material for the ball while it carries a power of that
        // category (restored by ClearGlow); the light marks it anyway.
        public Material attackBallMaterial;
        public Material defenseBallMaterial;
        public Material effectBallMaterial;

        public Material GetBallMaterial(PowerType type) => type switch
        {
            PowerType.Attack => attackBallMaterial,
            PowerType.Defense => defenseBallMaterial,
            PowerType.Effect => effectBallMaterial,
            _ => null,
        };

        // The launcher's gestures (LocalPowerGesture): pointing at the pick,
        // thumbs up once sent. The same for every power.
        [Header("Geste : pointer la cible")]
        [Tooltip("Pendant le choix d'un trou ou d'un point de la table, le personnage tend le bras et pointe l'endroit visé du doigt.")]
        public bool pointAtTarget = true;
        [Tooltip("Main qui pointe et lève le pouce : droite (coché) ou gauche. Avec la queue en main, cette main la lâche le temps du geste.")]
        public bool gestureWithRightHand = true;
        [Tooltip("Bras tendu vers la cible, en part de la longueur du bras (1 = complètement tendu).")]
        [Range(0.5f, 1f)] public float pointReach = 0.9f;
        [Tooltip("Le bras reste devant le personnage, quelle que soit la cible ou la direction du regard (degrés par rapport à l'avant du corps) : vers l'extérieur (côté de la main), vers l'intérieur (à travers le corps), vers le haut, vers le bas.")]
        public Vector4 pointLimits = new Vector4(80f, 30f, 50f, 75f);
        [Tooltip("Fermeture des autres doigts (degrés) pendant qu'on pointe.")]
        [Range(0f, 110f)] public float pointFingerCurl = 85f;
        [Tooltip("Fermeture du pouce (degrés) pendant qu'on pointe.")]
        [Range(-30f, 90f)] public float pointThumbCurl = 40f;
        [Tooltip("Index : 0 = comme l'animation ; négatif = plus tendu.")]
        [Range(-30f, 30f)] public float pointIndexCurl = -5f;

        [Header("Geste : pouce levé après l'envoi")]
        [Tooltip("Une fois le pouvoir envoyé, le personnage lève le pouce.")]
        public bool thumbsUp = true;
        [Tooltip("Durée (secondes) du pouce levé, fondus compris.")]
        [Range(0.3f, 3f)] public float thumbsUpDuration = 1.2f;
        [Tooltip("Position de la main par rapport à l'épaule : devant (m), en hauteur (m), vers le milieu du corps (m). Pour un bras de 0,6 m : mis à l'échelle du personnage.")]
        public Vector3 thumbsUpOffset = new Vector3(0.32f, 0.02f, 0.1f);
        [Tooltip("Où va le coude, par rapport à l'épaule : devant (m), en hauteur (m), vers le milieu du corps (m ; négatif = vers l'extérieur). Pour un bras de 0,6 m. Sous l'épaule et près du corps : l'avant-bras remonte vers le pouce.")]
        public Vector3 thumbsUpElbowOffset = new Vector3(0.08f, -0.3f, -0.06f);
        [Tooltip("Fermeture des quatre doigts (degrés) : le poing.")]
        [Range(0f, 110f)] public float thumbsUpFingerCurl = 95f;
        [Tooltip("Pouce : 0 = comme l'animation ; négatif = plus ouvert, vers le haut.")]
        [Range(-45f, 30f)] public float thumbsUpThumbCurl = -15f;

        [Tooltip("Coude guidé vers l'extérieur et un peu vers le bas pendant les gestes (0 = il suit l'animation et peut rentrer dans le corps).")]
        [Range(0f, 1f)] public float gestureElbowOut = 0.7f;
        [Tooltip("Une ligne [PowerGesture] dans la console à chaque pouce levé : où sont la main et le coude par rapport au buste.")]
        public bool gestureDebugLogs = true;
        [Tooltip("Durée (secondes) pour lever le bras (pointer, pouce levé). La main passe devant le corps, en arc autour de l'épaule.")]
        [Range(0.02f, 1.5f)] public float gestureBlendTime = 0.3f;
        [Tooltip("Durée (secondes) pour redescendre le bras, quand le pointeur quitte la table ou à la fin du pouce levé.")]
        [Range(0.02f, 1.5f)] public float gestureLowerTime = 0.5f;
    }
}
