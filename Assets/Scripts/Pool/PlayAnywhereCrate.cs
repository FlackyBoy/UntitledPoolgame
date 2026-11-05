using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: the crate the players take their object from at each
    // round (Tools > Pool > Play Anywhere > Créer la caisse). The rule set
    // lays the round's choices in a row on its top; this only says where.
    // The top is read from the crate's visible box (its first renderer, on
    // this object or a child), so moving the box alone moves the slots too.
    // While its objects are on offer, neon arrows above it say where to go
    // (PlayAnywhereArrowRing, in the style of the flag's but not to be mistaken
    // for them).
    [AddComponentMenu("Pool/Play Anywhere/Caisse")]
    public class PlayAnywhereCrate : MonoBehaviour, IPlayAnywhereArrowLayout
    {
        [Tooltip("Sans boîte visible (aucun renderer sur l'objet ni ses enfants) : hauteur (mètres) au-dessus du point d'origine où les objets sont posés.")]
        [Min(0f)] [SerializeField] private float topHeight = 0.45f;
        [Tooltip("Écart (mètres) entre deux objets de la caisse, le long de sa largeur (axe rouge de la boîte).")]
        [Min(0.1f)] [SerializeField] private float spacing = 0.7f;

        [Header("Flèches au-dessus de la caisse")]
        [Tooltip("Flèches néon flottantes, pendant la phase de pose seulement. Aperçu cyan dans la Scene quand la caisse est sélectionnée. Modèle : Crate Marker Prefab ; rebonds et clignotement : Flag Marker de PlayAnywhereSettings.")]
        [SerializeField] private bool arrows = true;
        [Tooltip("Nombre de flèches, en couronne. Plusieurs, pour que chaque joueur en voie toujours une de face. 0 = aucune.")]
        [Min(0)] [SerializeField] private int arrowCount = 4;
        [Tooltip("Taille des flèches (1 = 90 cm de long).")]
        [Min(0.05f)] [SerializeField] private float arrowSize = 0.8f;
        [Tooltip("Position (mètres) du point que visent les flèches, par rapport au centre du dessus de la caisse. Y = plus haut, X et Z = sur le côté. Au-dessus des objets posés dessus.")]
        [SerializeField] private Vector3 arrowOffset = new Vector3(0f, 0.6f, 0f);
        [Tooltip("Distance (mètres) entre ce point et le centre d'une flèche, au plus près.")]
        [Min(0f)] [SerializeField] private float arrowDistance = 0.75f;
        [Tooltip("Inclinaison (degrés) des flèches : 5 = presque au-dessus du point visé, 85 = presque à l'horizontale autour de lui.")]
        [Range(5f, 85f)] [SerializeField] private float arrowTilt = 35f;
        [Tooltip("Vitesse (degrés par seconde) à laquelle les flèches tournent. 0 = fixes.")]
        [SerializeField] private float arrowSpin = 30f;

        public int ArrowCount => arrowCount;
        public float ArrowSize => arrowSize;
        public Vector3 ArrowOffset => arrowOffset;
        public float ArrowDistance => arrowDistance;
        public float ArrowTilt => arrowTilt;
        public float ArrowSpin => arrowSpin;

        // The crate whose arrows are up: one at a time, during a build.
        private static PlayAnywhereCrate showingArrows;
        private Transform arrowRing;

        // The first crate enabled in the scene.
        public static PlayAnywhereCrate Current { get; private set; }

        private Renderer box;

        // Its pieces, shown only while it's the crate of the course being
        // played. Those already off stay off. The object itself stays on, for
        // Current.
        private Renderer[] shownRenderers;
        private Collider[] shownColliders;

        // A level is laid out for every mode: in play, a crate stays hidden
        // until a Play Anywhere match shows it (one already running has
        // decided for it).
        private void Awake()
        {
            if (PlayAnywhereRuleSet.Current == null) SetShown(false);
        }

        public void SetShown(bool shown)
        {
            if (shownRenderers == null)
            {
                shownRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r => r.enabled);
                shownColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(), c => c.enabled);
            }
            foreach (Renderer r in shownRenderers)
                if (r != null) r.enabled = shown;
            foreach (Collider c in shownColliders)
                if (c != null) c.enabled = shown;
        }

        private void OnEnable()
        {
            if (Current == null) Current = this;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private Renderer Box
        {
            get
            {
                if (box == null) box = GetComponentInChildren<Renderer>();
                return box;
            }
        }

        // The box's axes, flattened: the row of choices runs along its width.
        private Transform Frame => Box != null ? Box.transform : transform;

        private Vector3 Top
        {
            get
            {
                if (Box == null) return transform.position + Vector3.up * topHeight;
                Bounds b = Box.bounds;
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        // Where the bottom of choice `index` (of `count`) rests.
        public Vector3 Slot(int index, int count)
        {
            Vector3 right = Vector3.ProjectOnPlane(Frame.right, Vector3.up).normalized;
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            return Top + right * ((index - (count - 1) * 0.5f) * spacing);
        }

        public Quaternion SlotRotation => Quaternion.Euler(0f, Frame.eulerAngles.y, 0f);

        public void ShowArrows()
        {
            HideArrows();
            if (!arrows) return;
            showingArrows = this;
            // Beside the crate, not under it: Box takes the first renderer below.
            if (arrowRing == null)
            {
                var settings = PoolSettingsLoader.LoadOrDefault<PlayAnywhereSettings>("PlayAnywhereSettings");
                arrowRing = PlayAnywhereArrowRing.Build("Repère de la caisse", settings.crateMarkerPrefab, this, Top, transform.parent, settings);
            }
            if (arrowRing != null) arrowRing.gameObject.SetActive(true);
        }

        public static void HideArrows()
        {
            if (showingArrows != null && showingArrows.arrowRing != null) showingArrows.arrowRing.gameObject.SetActive(false);
            showingArrows = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (arrows) PlayAnywhereArrowRing.DrawPreviewGizmos(this, Top, new Color(0.2f, 0.85f, 1f));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            for (int i = 0; i < 3; i++) Gizmos.DrawWireSphere(Slot(i, 3), 0.08f);
        }
    }
}
