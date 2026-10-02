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
        [Tooltip("Source de la lumière ambiante.")]
        public AmbientMode ambientMode = AmbientMode.Trilight;
        [ColorUsage(false, true)] public Color ambientSky = new Color(0.25f, 0.25f, 0.3f);
        [ColorUsage(false, true)] public Color ambientEquator = new Color(0.18f, 0.18f, 0.2f);
        [ColorUsage(false, true)] public Color ambientGround = new Color(0.1f, 0.1f, 0.1f);
        [Tooltip("Intensité de la lumière ambiante venant du ciel (mode Skybox).")]
        public float ambientIntensity = 1f;
        [Tooltip("Intensité des reflets de l'environnement.")]
        [Range(0f, 1f)] public float reflectionIntensity = 1f;

        [Header("Lumière principale (soleil, lune)")]
        public Color mainLightColor = Color.white;
        public float mainLightIntensity = 1f;
        [Tooltip("Orientation (degrés) : X = hauteur, Y = direction.")]
        public Vector3 mainLightRotation = new Vector3(50f, -30f, 0f);
        public LightShadows mainLightShadows = LightShadows.Soft;
        [Range(0f, 1f)] public float mainLightShadowStrength = 1f;

        [Header("Post-process et brouillard")]
        [Tooltip("Profil du volume global (étalonnage, bloom, vignette…).")]
        public VolumeProfile volumeProfile;
        public bool fog;
        public Color fogColor = Color.gray;
        public FogMode fogMode = FogMode.ExponentialSquared;
        [Tooltip("Densité (modes exponentiels).")]
        public float fogDensity = 0.02f;
        [Tooltip("Début et fin (mode linéaire), en mètres.")]
        public Vector2 fogLinearRange = new Vector2(5f, 40f);

        [Header("Lumières du décor (néons, lampes posées dans la salle)")]
        [Tooltip("Teinte appliquée aux lumières du décor.")]
        public Color decorTint = Color.white;
        [Tooltip("Force de la teinte : 0 = couleur d'origine de chaque lampe, 1 = teinte pure.")]
        [Range(0f, 1f)] public float decorTintStrength;
        [Tooltip("Multiplicateur de l'intensité d'origine des lumières du décor.")]
        public float decorIntensity = 1f;

        public string Scenario => string.IsNullOrEmpty(scenarioName) ? name : scenarioName;
    }
}
