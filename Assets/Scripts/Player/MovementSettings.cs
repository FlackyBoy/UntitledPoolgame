using UnityEngine;

namespace UntitledPoolGame.Player
{
    // Movement feel of LocalPlayerLocomotion (R&D party-brawler
    // movement, kept hybrid: animation-driven body, physics-flavoured
    // motion): momentum, sprint, jump, ledge climbing, and how floppy
    // the arms and the body look while the hands are free. Shared by every
    // player. Assets/Resources/MovementSettings, created by Tools > Pool >
    // Ensure Config Assets Exist; read every frame, so it can be tuned while
    // playing. Tooltips in French: read by the designer in the Inspector.
    [CreateAssetMenu(fileName = "MovementSettings", menuName = "Pool/Movement Settings")]
    public class MovementSettings : ScriptableObject
    {
        [Header("Marche et course")]
        [Tooltip("Vitesse de marche, en m/s.")]
        [Min(0f)] public float walkSpeed = 4.5f;
        [Tooltip("Vitesse en sprint (Maj / L3 maintenu), en m/s.")]
        [Min(0f)] public float sprintSpeed = 7.5f;
        [Tooltip("Prise de vitesse, en m/s². Plus bas = plus d'élan à prendre.")]
        [Min(0.1f)] public float acceleration = 28f;
        [Tooltip("Freinage quand on relâche ou qu'on change de direction, en m/s². Plus bas = on glisse plus.")]
        [Min(0.1f)] public float deceleration = 16f;
        [Tooltip("Contrôle en l'air (0 = aucun, 1 = comme au sol).")]
        [Range(0f, 1f)] public float airControl = 0.35f;

        [Header("Saut")]
        [Tooltip("Hauteur du saut, en mètres.")]
        [Min(0f)] public float jumpHeight = 1.1f;
        [Tooltip("Gravité, en m/s² (négative).")]
        public float gravity = -22f;
        [Tooltip("Délai pendant lequel on peut encore sauter après avoir quitté un rebord, en secondes.")]
        [Min(0f)] public float coyoteTime = 0.12f;
        [Tooltip("Un appui sur Saut juste avant d'atterrir est gardé ce temps-là, en secondes.")]
        [Min(0f)] public float jumpBuffer = 0.12f;

        [Header("Escalade des rebords (Saut face à un rebord)")]
        [Tooltip("Hauteur minimale d'un rebord qu'on escalade (en dessous, on marche ou on saute dessus), en mètres depuis les pieds.")]
        [Min(0f)] public float ledgeMinHeight = 0.8f;
        [Tooltip("Hauteur maximale d'un rebord qu'on attrape, en mètres depuis les pieds.")]
        [Min(0f)] public float ledgeMaxHeight = 2.1f;
        [Tooltip("Distance devant le joueur où l'on cherche un rebord, en mètres.")]
        [Min(0.05f)] public float ledgeReach = 0.6f;
        [Tooltip("Durée de l'escalade, en secondes.")]
        [Min(0.1f)] public float climbDuration = 0.6f;

        [Header("Corps mou (mains libres seulement)")]
        [Tooltip("Force des muscles des bras et des mains quand les mains sont libres (1 = rigide comme l'animation, plus bas = les bras ballottent).")]
        [Range(0f, 1f)] public float relaxedArmStrength = 0.45f;
        [Tooltip("Maintien des bras et des mains sur l'animation quand les mains sont libres (1 = collés à l'animation).")]
        [Range(0f, 1f)] public float relaxedArmPin = 0.15f;
        [Tooltip("Force des muscles du buste et de la tête quand les mains sont libres.")]
        [Range(0f, 1f)] public float relaxedSpineStrength = 0.8f;
        [Tooltip("Vitesse à laquelle le corps redevient rigide (queue en main, visée, coup) ou se relâche, par seconde.")]
        [Min(0.1f)] public float stiffnessBlendSpeed = 6f;
        [Tooltip("Le buste se penche quand on accélère, freine ou tourne, en mètres de décalage du bassin pour 10 m/s² d'accélération.")]
        [Min(0f)] public float bodyLean = 0.08f;
        [Tooltip("Lissage du penché du buste (plus haut = plus réactif).")]
        [Min(0.1f)] public float bodyLeanSmoothing = 8f;

        [Header("Vue")]
        [Tooltip("Roulis de la vue quand on se déplace sur le côté, en degrés.")]
        [Min(0f)] public float strafeCameraRoll = 2.5f;
        [Tooltip("Balancement de la vue en marchant, en mètres (0 = aucun).")]
        [Min(0f)] public float headBob = 0.025f;
        [Tooltip("Pas par seconde à pleine vitesse de marche, pour le balancement.")]
        [Min(0f)] public float headBobFrequency = 1.9f;

        private static MovementSettings instance;
        public static MovementSettings Instance =>
            instance != null ? instance : instance = Pool.PoolSettingsLoader.LoadOrDefault<MovementSettings>("MovementSettings");
    }
}
