using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // One direction sign along a Play Anywhere course (PlayAnywhereCourseSigns
    // places them). Only décor: no collider, so it never gets in the way of a
    // ball, a shot or a placed object. On the prefab named by Sign Prefab
    // (PlayAnywhereSettings); the root is at the foot of the post, the board
    // faces +Z and its chevrons point along +X.
    //
    // Signs placed in the scene from the hole's Inspector are moved and sized
    // by hand; each remembers where it was generated, so recalculating replaces
    // only the ones left alone.
    [AddComponentMenu("Pool/Play Anywhere/Panneau")]
    public class PlayAnywhereSign : MonoBehaviour
    {
        [Tooltip("La planche, sans rotation dans le prefab : tournée d'un demi-tour pour inverser le sens des chevrons (le poteau ne bouge pas). Vide = jamais tournée.")]
        [SerializeField] private Transform board;
        [Tooltip("Le poteau, à l'échelle 1 et le pied à la racine dans le prefab : son pied reste au sol, il s'allonge ou se raccourcit selon la hauteur du panneau. Vide = jamais changé.")]
        [SerializeField] private Transform post;
        [Tooltip("Fait pointer les chevrons dans l'autre sens.")]
        [SerializeField] private bool flipChevrons;

        // How far (world metres) below the root the ground is; negative when the
        // sign was lowered and the root is under the ground (the post is then
        // shorter, its foot still on the ground). Kept up to date by
        // PlayAnywhereSignEditor when the sign is moved.
        [HideInInspector] [SerializeField] private float postDrop;

        // The ground it stands on, and where that was looked up. Remembered and
        // not searched again while the sign only goes up and down: from under
        // the floor the floor can't be seen, and from above a search can stop
        // on a shelf or a ceiling.
        [HideInInspector] [SerializeField] private bool groundKnown;
        [HideInInspector] [SerializeField] private float groundHeight;
        [HideInInspector] [SerializeField] private Vector2 groundSpot;

        // Where it was generated: anything else means it was edited by hand.
        [HideInInspector] [SerializeField] private Vector3 generatedPosition;
        [HideInInspector] [SerializeField] private Quaternion generatedRotation = Quaternion.identity;
        [HideInInspector] [SerializeField] private Vector3 generatedScale = Vector3.one;
        [HideInInspector] [SerializeField] private bool generatedFlip;

        public bool GroundKnown => groundKnown;
        public float GroundHeight => groundHeight;
        public bool IsOverGroundSpot => groundKnown && (Spot - groundSpot).sqrMagnitude < 1e-6f;
        public float GeneratedHeight => generatedPosition.y;

        // How far the root can go under the ground before the board reaches it.
        public float MaxSink
        {
            get
            {
                if (board == null) return PostHeight * 0.9f;
                float lowest = float.PositiveInfinity;
                foreach (Renderer r in board.GetComponentsInChildren<Renderer>(true))
                    lowest = Mathf.Min(lowest, r.bounds.min.y);
                return float.IsInfinity(lowest) ? PostHeight * 0.9f : Mathf.Max(0f, lowest - transform.position.y);
            }
        }

        private Vector2 Spot => new Vector2(transform.position.x, transform.position.z);

        // The post's height, unstretched, in world metres.
        public float PostHeight => PostMeshHeight * transform.lossyScale.y;

        // Comes from its mesh; its foot is at its pivot.
        private float PostMeshHeight => post != null && post.TryGetComponent(out MeshFilter mesh) && mesh.sharedMesh != null ? mesh.sharedMesh.bounds.size.y : 1f;

        public bool IsHandEdited =>
            (transform.position - generatedPosition).sqrMagnitude > 1e-6f
            || Quaternion.Angle(transform.rotation, generatedRotation) > 0.1f
            || (transform.localScale - generatedScale).sqrMagnitude > 1e-6f
            || flipChevrons != generatedFlip;

        public void Configure(bool flip, float drop)
        {
            flipChevrons = flip;
            postDrop = drop;
            SetGround(transform.position.y - drop);
            Apply();
        }

        public void SetGround(float height)
        {
            groundKnown = true;
            groundHeight = height;
            groundSpot = Spot;
        }

        public void MarkGenerated()
        {
            generatedPosition = transform.position;
            generatedRotation = transform.rotation;
            generatedScale = transform.localScale;
            generatedFlip = flipChevrons;
        }

        // Back to where it was generated, so that recalculating replaces it.
        public void RestoreGenerated()
        {
            transform.SetPositionAndRotation(generatedPosition, generatedRotation);
            transform.localScale = generatedScale;
            flipChevrons = generatedFlip;
            groundKnown = false;
            Apply();
        }

        public void SetPostDrop(float drop)
        {
            postDrop = drop;
            Apply();
        }

        private void Apply()
        {
            if (board != null) board.localRotation = flipChevrons ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            if (post == null) return;

            float localDrop = postDrop / Mathf.Max(0.001f, transform.lossyScale.y);
            Vector3 position = post.localPosition;
            position.y = -localDrop;
            post.localPosition = position;
            Vector3 scale = post.localScale;
            scale.y = Mathf.Max(0.01f, 1f + localDrop / PostMeshHeight);
            post.localScale = scale;
        }

        private void OnValidate() => Apply();
    }
}
