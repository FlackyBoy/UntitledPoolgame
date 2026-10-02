#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UntitledPoolGame.Core;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.PoolEditor
{
    // What the Level Maker builds levels from. Editor-only (Assets/Editor),
    // like PoolTableAssetSettings: nothing here is read at runtime. Filled
    // with whatever the project already has the first time it's created,
    // then left alone so hand-picked values stick.
    public class LevelMakerSettings : ScriptableObject
    {
        private const string AssetPath = "Assets/Editor/LevelMakerSettings.asset";

        [Header("Prefabs")]
        [Tooltip("Prefab instancié par le PlayerInputManager pour chaque joueur qui rejoint.")]
        public GameObject playerPrefab;
        [Tooltip("Modèle de table placé par l'étape Table. S'il ne contient pas déjà la physique (PoolMatchRules), l'outil propose de la générer.")]
        public GameObject tablePrefab;
        [Tooltip("Queue placée à côté de la table (doit avoir LocalGrabbable, Cue, CueChargeSlide et les deux poignées InteractionTarget).")]
        public GameObject cuePrefab;

        [Header("Nouveau niveau")]
        [Tooltip("Dossier où les nouvelles scènes de niveau sont enregistrées.")]
        public string levelFolder = "Assets/Scenes/Levels";
        [Tooltip("Profil de post-process du volume global ajouté aux nouveaux niveaux (vide = pas de volume).")]
        public VolumeProfile volumeProfile;
        [Tooltip("Ajoute un sol provisoire de 20 × 20 m aux nouveaux niveaux, pour pouvoir jouer avant d'avoir construit la salle.")]
        public bool addTemporaryFloor = true;

        [Header("Salle")]
        [Tooltip("Kit utilisé pour les nouvelles salles et le pinceau à props.")]
        public PrefabPalette palette;
        [Tooltip("Pas de la grille d'aimantation, en mètres.")]
        public float gridStep = 0.5f;
        [Tooltip("Hauteur du sol des salles tracées.")]
        public float floorHeight;

        [Header("Placement")]
        [Tooltip("Nombre de queues attendues dans un niveau (une par joueur).")]
        public int cueCount = 2;
        [Tooltip("Pas de rotation de la table, en degrés.")]
        public float tableRotationStep = 90f;

        public static LevelMakerSettings GetOrCreate()
        {
            LevelMakerSettings asset = AssetDatabase.LoadAssetAtPath<LevelMakerSettings>(AssetPath);
            if (asset != null) return asset;

            if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
            asset = CreateInstance<LevelMakerSettings>();
            asset.playerPrefab = FindPrefabWith<LocalFpsPlayerController>("Assets/Prefabs/PlayerLocal");
            asset.tablePrefab = PoolTableBuilder.TableAssetSettings.customTablePrefab;
            if (asset.tablePrefab == null) asset.tablePrefab = FindPrefabWith<PoolMatchRules>("Assets/Prefabs");
            asset.cuePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cue/Cue.prefab");
            if (asset.cuePrefab == null) asset.cuePrefab = FindPrefabWith<Cue>("Assets/Prefabs");
            asset.volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            AssetDatabase.CreateAsset(asset, AssetPath);
            return asset;
        }

        // First prefab under folder (outside "Old" backups) carrying T.
        private static GameObject FindPrefabWith<T>(string folder) where T : Component
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Old/")) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponentInChildren<T>(true) != null) return prefab;
            }
            return null;
        }
    }
}
#endif
