using UnityEngine;
using UnityEngine.Rendering;

namespace UntitledPoolGame.Core
{
    // One mood for a room: sky and ambient light, main light, post-process
    // and fog, and how the room's own lamps are tinted. Applied by
    // AmbienceController, in the editor (Level Maker) or while playing; its
    // indirect lighting is baked as its own Adaptive Probe Volume lighting
    // scenario (named after Scenario Name), so one room can switch moods.
    [CreateAssetMenu(menuName = "Pool/Level Maker Ambience", fileName = "Ambience")]
    public class AmbiencePreset : ScriptableObject
    {
        [Tooltip("Nom du scénario d'éclairage APV précalculé pour cette ambiance (vide = nom de l'asset).")]
        public string scenarioName;

        [Header("Ciel et lumière ambiante")]
        [Tooltip("Matériau de ciel (vide = pas de ciel, fond de couleur).")]
        public Material skybox;
        [Tooltip("D'où vient la lumière ambiante, celle qui éclaire tout un peu, même à l'ombre. Trilight : trois couleurs (dessus, côtés, dessous), le plus simple à régler en intérieur. Skybox : tirée du ciel. Flat : une seule couleur (Ambient Sky).")]
        public AmbientMode ambientMode = AmbientMode.Trilight;
        [Tooltip("Lumière ambiante venant d'en haut (faces tournées vers le plafond). Plus clair = ombres moins noires. Teinte froide = ombres bleutées.")]
        [ColorUsage(false, true)] public Color ambientSky = new Color(0.25f, 0.25f, 0.3f);
        [Tooltip("Lumière ambiante venant des côtés (murs, visages, flancs des billes).")]
        [ColorUsage(false, true)] public Color ambientEquator = new Color(0.18f, 0.18f, 0.2f);
        [Tooltip("Lumière ambiante venant d'en bas (dessous de la table, menton). Garder sombre pour que les objets aient l'air posés.")]
        [ColorUsage(false, true)] public Color ambientGround = new Color(0.1f, 0.1f, 0.1f);
        [Tooltip("Intensité de la lumière ambiante venant du ciel (mode Skybox).")]
        public float ambientIntensity = 1f;
        [Tooltip("Force des reflets de l'environnement sur les surfaces brillantes (billes, bois verni, verre). 0 = aucun reflet, 1 = complet. À baisser si les billes paraissent grises ou délavées.")]
        [Range(0f, 1f)] public float reflectionIntensity = 1f;

        [Header("Lumière principale (soleil, lune)")]
        [Tooltip("Couleur de la lumière principale. Blanc chaud (orangé) = intérieur accueillant ; blanc froid (bleuté) = lune, néon, hôpital.")]
        public Color mainLightColor = Color.white;
        [Tooltip("Force de la lumière principale. C'est elle qui fait les ombres nettes : plus forte = plus de contraste entre lumière et ombre.")]
        public float mainLightIntensity = 1f;
        [Tooltip("Orientation (degrés) : X = hauteur, Y = direction.")]
        public Vector3 mainLightRotation = new Vector3(50f, -30f, 0f);
        [Tooltip("Tourner la lumière principale vers Main Light Rotation. Décoché : elle garde l'orientation donnée dans la scène (fenêtres, verrières déjà placées).")]
        public bool applyMainLightRotation = true;
        [Tooltip("Ombres de la lumière principale. Soft : bords doux. Hard : bords nets, plus dur et un peu moins cher. None : aucune ombre.")]
        public LightShadows mainLightShadows = LightShadows.Soft;
        [Tooltip("Noirceur des ombres : 1 = ombre pleine, 0 = invisible. Vers 0,7–0,85, les ombres restent lisibles sans boucher les détails.")]
        [Range(0f, 1f)] public float mainLightShadowStrength = 1f;

        [Header("Post-process et brouillard")]
        [Tooltip("Profil de post-process (le fichier « … Volume » à côté) : Bloom, étalonnage, vignette, tonemapping… Il s'édite comme un Volume : cocher un réglage pour qu'il compte.")]
        public VolumeProfile volumeProfile;
        [Tooltip("Brouillard : le lointain se fond dans Fog Color. Donne de la profondeur et cache le bout d'une grande salle.")]
        public bool fog;
        [Tooltip("Couleur vers laquelle tend le lointain. Proche de la couleur des murs ou de la lumière ambiante, sinon il fait « filtre ».")]
        public Color fogColor = Color.gray;
        [Tooltip("Exponential : monte doucement dès la caméra. Exponential Squared : clair de près, épais d'un coup plus loin. Linear : entre Fog Linear Range début et fin.")]
        public FogMode fogMode = FogMode.ExponentialSquared;
        [Tooltip("Épaisseur du brouillard (modes exponentiels). 0,01 = à peine visible au fond d'une salle, 0,03 = salle enfumée, 0,08 = purée de pois.")]
        public float fogDensity = 0.02f;
        [Tooltip("Début et fin (mode linéaire), en mètres.")]
        public Vector2 fogLinearRange = new Vector2(5f, 40f);

        [Header("Particules d'ambiance (en jeu seulement)")]
        [Tooltip("Poussière qui flotte dans l'air : 0 = aucune, 1 = normal, 3 = salle très poussiéreuse. Change le nombre de grains et leur visibilité.")]
        [Range(0f, 3f)] public float dustIntensity;
        [Tooltip("Couleur des grains ; l'alpha règle leur visibilité à l'intensité 1.")]
        public Color dustColor = new Color(1f, 0.95f, 0.85f, 0.35f);
        [Tooltip("Taille d'un grain, en mètres (0,015 = 1,5 cm).")]
        [Range(0.005f, 0.06f)] public float dustSize = 0.015f;
        [Tooltip("Vitesse à laquelle les grains dérivent (0 = immobiles).")]
        [Range(0f, 1f)] public float dustDrift = 0.15f;
        [Tooltip("Brume basse : nappes douces qui flottent près du sol. 0 = aucune, 1 = légère, 3 = épaisse. S'ajoute au brouillard (Fog), qui, lui, ne bouge pas.")]
        [Range(0f, 3f)] public float hazeIntensity;
        [Tooltip("Couleur de la brume ; l'alpha règle son épaisseur à l'intensité 1 (garder bas : 0,05–0,12).")]
        public Color hazeColor = new Color(0.75f, 0.78f, 0.85f, 0.08f);
        [Tooltip("Hauteur de la nappe de brume au-dessus du sol, en mètres.")]
        [Min(0.2f)] public float hazeHeight = 1.2f;
        [Tooltip("Matériau des grains (créé par Ensure Config Assets Exist : Assets/LevelKits/Ambiances/Particules).")]
        public Material dustMaterial;
        [Tooltip("Matériau de la brume (créé par Ensure Config Assets Exist).")]
        public Material hazeMaterial;

        [Header("Lumières du décor (néons, lampes posées dans la salle)")]
        [Tooltip("Teinte appliquée aux lumières du décor.")]
        public Color decorTint = Color.white;
        [Tooltip("Force de la teinte : 0 = couleur d'origine de chaque lampe, 1 = teinte pure.")]
        [Range(0f, 1f)] public float decorTintStrength;
        [Tooltip("Multiplicateur de l'intensité d'origine des lumières du décor.")]
        public float decorIntensity = 1f;

        public string Scenario => string.IsNullOrEmpty(scenarioName) ? name : scenarioName;

        // Raised when the preset is edited in the Inspector, so the ambience
        // in use follows the tweak while playing (AmbienceController).
        public static event System.Action<AmbiencePreset> Edited;

        private void OnValidate() => Edited?.Invoke(this);
    }
}
