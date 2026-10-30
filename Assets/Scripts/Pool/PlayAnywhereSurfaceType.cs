using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: how a ball rolls and bounces on one kind of ground
    // (parquet, carpet, counter...). Each décor collider gets one at the
    // start of the match, in this order: a PlayAnywhereSurface on the
    // object, one of the Materials below, a Keyword in the object's or its
    // material's name, then PlayAnywhereSettings' Default Surface. Every
    // type of Assets/Resources/PlayAnywhereSurfaces is looked in (the
    // defaults are created there by Ensure Config Assets Exist).
    [CreateAssetMenu(fileName = "Surface", menuName = "Pool/Play Anywhere Surface")]
    public class PlayAnywhereSurfaceType : ScriptableObject
    {
        [Tooltip("Ralentissement (m/s²) d'une bille qui roule sur cette surface. Le tapis du billard est à 0,5 (Friction de PoolPhysicsSettings). Plus haut = la bille s'arrête plus vite.")]
        [Min(0f)] public float rollingFriction = 0.5f;
        [Tooltip("Accroche (m/s²) d'une bille qui glisse encore (juste après le coup, avec de l'effet) : plus haut, elle se met à rouler plus vite. Tapis du billard : 2 (Slide Friction de PoolPhysicsSettings).")]
        [Min(0f)] public float slideFriction = 2f;
        [Tooltip("Rebond (0 à 1) d'une bille qui tape ou tombe sur cette surface : 0 = elle ne rebondit pas, 1 = elle repart à la même vitesse. Sous 0,3 m/s, Unity ne fait pas rebondir (Bounce Threshold du projet).")]
        [Range(0f, 1f)] public float bounciness = 0.4f;

        [Header("Reconnaissance automatique")]
        [Tooltip("Tout objet du décor qui utilise un de ces matériaux a cette surface, dans toutes les scènes, sans composant à poser. Glisse ici les matériaux de sol, de tapis, de comptoir...")]
        public Material[] materials = new Material[0];
        [Tooltip("Mots (sans tenir compte des majuscules) cherchés dans le nom de l'objet et de son matériau, si aucun matériau de la liste ne correspond. Ex. « rug », « carpet ».")]
        public string[] keywords = new string[0];

        // Made at runtime, one per type: Minimum beats the ball's Average
        // combine, so the bounce is exactly this type's.
        [System.NonSerialized] private PhysicsMaterial runtimeMaterial;

        // Tuned during play: the colliders already carry the material.
        private void OnValidate()
        {
            if (runtimeMaterial != null) runtimeMaterial.bounciness = bounciness;
        }

        public PhysicsMaterial RuntimeMaterial
        {
            get
            {
                if (runtimeMaterial == null)
                    runtimeMaterial = new PhysicsMaterial(name)
                    {
                        bounciness = bounciness,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        // Unity's own friction: the default material's. The
                        // ball's Minimum combine keeps it low anyway, PoolBall
                        // does the rolling.
                        dynamicFriction = 0.6f,
                        staticFriction = 0.6f,
                        frictionCombine = PhysicsMaterialCombine.Average,
                    };
                return runtimeMaterial;
            }
        }
    }
}
