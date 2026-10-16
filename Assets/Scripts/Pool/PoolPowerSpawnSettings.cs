using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Everything about HOW powers get randomly handed out — the pool to draw
    // from, the color code per PowerType (crates and the ball glow both read
    // this, so they always agree), and the random timing for crate respawns
    // and ball power rotation. One shared asset, same Resources-loaded
    // pattern as PoolPhysicsSettings — this is the "config file" the powers'
    // random distribution is meant to be tunable from.
    [CreateAssetMenu(fileName = "PoolPowerSpawnSettings", menuName = "Pool/Power Spawn Settings")]
    public class PoolPowerSpawnSettings : ScriptableObject
    {
        [Header("Power pool")]
        // Drawn from at random for both crates and the ball rotation below —
        // an empty/missing pool just means nothing spawns (checked by both
        // callers) rather than a hard error, so this can be filled in
        // gradually as more powers exist.
        public PoolPower[] availablePowers;

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
        // How many PoolPowerCrate instances are alive/spawned on the table
        // at once — PoolPowerCrateManager keeps this many active, drawing
        // spawn locations from PoolPowerSpawnPoint markers in the scene.
        public int activeCrateCount = 3;
        // Random real-world delay range before a picked-up crate respawns
        // (new location, freshly rolled power).
        public float crateRespawnMinDelay = 15f;
        public float crateRespawnMaxDelay = 30f;

        [Header("Crate visuals — optional, one prefab per PowerType")]
        // Assign your own crate/chest model here to use instead of the
        // auto-generated placeholder cube — PoolPowerCrate swaps to whichever
        // one matches the power it's currently carrying (and swaps again on
        // respawn if the newly rolled power is a different type). Leaving
        // one unassigned falls back to the tinted placeholder cube for that
        // type specifically, so this can be filled in gradually.
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
        // How often the ball currently carrying a power changes — a new
        // random active (not yet pocketed) ball is chosen, with a freshly
        // rolled power, on a random interval in this range.
        public float ballRotationMinInterval = 20f;
        public float ballRotationMaxInterval = 40f;
        // Point light intensity on the glowing ball — same caveat as
        // PoolPotEffectSettings.haloIntensity (depends on URP Physical
        // Light Units).
        public float ballGlowIntensity = 4f;
        public float ballGlowRange = 0.3f;

        [Header("Power ball visuals — optional, one material per PowerType")]
        // Assign your own shader/material here to swap the ball's own
        // renderer to for as long as it carries this power (on top of the
        // glow light above) — PowerBall restores its normal material via
        // ClearGlow() once it stops. Leaving one unassigned just skips the
        // swap for that type, so this can be filled in gradually; the light
        // alone still marks which ball currently carries a power either way.
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

        // The launcher's gestures (LocalPowerGesture): pointing at the pocket
        // or the spot of the table being picked, then a thumbs up once the
        // power is sent. Procedural (FBBIK + finger bones), the same for
        // every power, so here with the other shared power settings.
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
