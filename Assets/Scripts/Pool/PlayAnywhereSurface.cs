using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: marks a décor object (and its children's colliders) as
    // one kind of ground, for the exceptions: most objects are recognised
    // by their material or name (PlayAnywhereSurfaceType). Read by
    // PlayAnywhereRuleSet at the start of the match, which also gives the
    // colliders the type's bounce (put back after).
    [AddComponentMenu("Pool/Play Anywhere/Surface")]
    public class PlayAnywhereSurface : MonoBehaviour
    {
        [Tooltip("Type de surface (frottement, rebond) : un asset de Assets/Resources/PlayAnywhereSurfaces, ou un à toi (Create > Pool > Play Anywhere Surface). Vaut pour les colliders de cet objet et de ses enfants, sauf ceux qui ont leur propre Surface.")]
        [SerializeField] private PlayAnywhereSurfaceType type;

        public PlayAnywhereSurfaceType Type => type;
    }
}
