using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // One building kit for the Level Maker: the modular pieces rooms are
    // built from, plus the props painted into them, each kind with its own
    // list of variants (the Level Maker shows them as thumbnails; rooms
    // remember the index of the variant they use, so swapping the palette
    // of a room rebuilds it in another style with the same layout). Sizes
    // are measured on the prefabs themselves, so any kit works whatever its
    // pivots; the direction a wall piece runs along is its longest
    // horizontal side unless set by hand.
    [CreateAssetMenu(menuName = "Pool/Level Maker Palette", fileName = "Palette")]
    public class PrefabPalette : ScriptableObject
    {
        public enum Axis { Auto, X, Z }

        [Header("Murs (variantes)")]
        [Tooltip("Modules de mur plein, posés tous les « largeur de module » le long du tracé. Toutes les variantes doivent avoir la même largeur.")]
        public List<GameObject> walls = new List<GameObject>();
        [Tooltip("Modules de mur avec porte (même largeur que les murs).")]
        public List<GameObject> doors = new List<GameObject>();
        [Tooltip("Modules de mur avec fenêtre (même largeur que les murs).")]
        public List<GameObject> windows = new List<GameObject>();
        [Tooltip("Poteaux posés à chaque angle du tracé (cachent la jonction des murs). Liste vide = pas de poteau.")]
        public List<GameObject> pillars = new List<GameObject>();
        // Renamed from wallLengthAxis (X/Z only) so existing palettes start
        // on Auto: Forest Bar's walls run along Z, not the assumed X.
        [Tooltip("Axe local des prefabs de mur qui suit la longueur du mur. Auto = le plus long des deux côtés horizontaux (un mur est plus long qu'épais).")]
        public Axis wallAxis = Axis.Auto;
        [Tooltip("Retourne tous les murs d'un demi-tour (si leur face intérieure regarde dehors).")]
        public bool flipWalls;
        [Tooltip("Largeur d'un module de mur en mètres. 0 = mesurée sur le premier mur.")]
        public float moduleWidthOverride;

        [Header("Sol et plafond (variantes)")]
        [Tooltip("Dalles de sol : leur dessus est posé à la hauteur du sol.")]
        public List<GameObject> floorTiles = new List<GameObject>();
        [Tooltip("Dalles de plafond : leur dessous est posé en haut des murs.")]
        public List<GameObject> ceilingTiles = new List<GameObject>();
        [Tooltip("Ajuste à la taille d'un module de mur les dalles (sol et plafond) dont la taille en est proche (voir Tile Fit Tolerance), " +
                 "pour finir pile aux murs sans joint. Les autres dalles gardent leur taille et débordent sous les murs.")]
        public bool fitTilesToModule = true;
        [Tooltip("Écart maximal (part du module) entre la taille d'une dalle et le module pour qu'elle soit ajustée : 0,25 = jusqu'à 25 % plus grande ou plus petite.")]
        [Range(0f, 1f)] public float tileFitTolerance = 0.25f;

        [Header("Étages (variantes)")]
        [Tooltip("Escaliers : posés sur le sol d'une salle, mis à l'échelle de la hauteur d'un étage ; ils percent le sol de l'étage au-dessus.")]
        public List<GameObject> stairs = new List<GameObject>();
        [Tooltip("Inverse le sens de montée détecté sur les escaliers (si la flèche de l'outil Escaliers pointe vers le bas des marches).")]
        public bool stairsReversed;
        [Tooltip("Garde-corps : posés sur un côté de salle ouvert (bord de mezzanine), comme un module de mur.")]
        public List<GameObject> railings = new List<GameObject>();

        [Header("Props")]
        [Tooltip("Objets de décor proposés par le pinceau à props.")]
        public List<GameObject> props = new List<GameObject>();

        [Header("Options")]
        [Tooltip("Ajoute un MeshCollider aux pièces qui n'ont aucun collider (sinon on traverse les murs).")]
        public bool addMissingColliders = true;

        // Single prefabs of the first palettes, before variants: moved into
        // the lists when such a palette is loaded.
        [SerializeField, HideInInspector] private GameObject wall, door, window, pillar, floorTile, ceilingTile;

        public GameObject Wall(int variant) => Pick(walls, variant);
        public GameObject Door(int variant) => Pick(doors, variant);
        public GameObject Window(int variant) => Pick(windows, variant);
        public GameObject Pillar(int variant) => Pick(pillars, variant);
        public GameObject FloorTile(int variant) => Pick(floorTiles, variant);
        public GameObject CeilingTile(int variant) => Pick(ceilingTiles, variant);
        public GameObject Stairs(int variant) => Pick(stairs, variant);
        public GameObject Railing(int variant) => Pick(railings, variant);

        // The variant at index, or the first one when the index doesn't
        // exist in this palette (a room built with another kit).
        public static GameObject Pick(List<GameObject> list, int variant)
        {
            if (list == null || list.Count == 0) return null;
            if (variant >= 0 && variant < list.Count && list[variant] != null) return list[variant];
            foreach (GameObject prefab in list)
                if (prefab != null) return prefab;
            return null;
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            bool migrated = Migrate(walls, ref wall) | Migrate(doors, ref door) | Migrate(windows, ref window)
                            | Migrate(pillars, ref pillar) | Migrate(floorTiles, ref floorTile) | Migrate(ceilingTiles, ref ceilingTile);
            if (migrated) UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private static bool Migrate(List<GameObject> list, ref GameObject single)
        {
            if (single == null) return false;
            if (!list.Contains(single)) list.Insert(0, single);
            single = null;
            return true;
        }
    }
}
