using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: the crate the players take their object from at each
    // round (Tools > Pool > Play Anywhere > Créer la caisse). The rule set
    // lays the round's choices in a row on its top; this only says where.
    // The top is read from the crate's visible box (its first renderer, on
    // this object or a child), so moving the box alone moves the slots too.
    [AddComponentMenu("Pool/Play Anywhere/Caisse")]
    public class PlayAnywhereCrate : MonoBehaviour
    {
        [Tooltip("Sans boîte visible (aucun renderer sur l'objet ni ses enfants) : hauteur (mètres) au-dessus du point d'origine où les objets sont posés.")]
        [Min(0f)] [SerializeField] private float topHeight = 0.45f;
        [Tooltip("Écart (mètres) entre deux objets de la caisse, le long de sa largeur (axe rouge de la boîte).")]
        [Min(0.1f)] [SerializeField] private float spacing = 0.7f;

        // The first crate enabled in the scene.
        public static PlayAnywhereCrate Current { get; private set; }

        private Renderer box;

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

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            for (int i = 0; i < 3; i++) Gizmos.DrawWireSphere(Slot(i, 3), 0.08f);
        }
    }
}
