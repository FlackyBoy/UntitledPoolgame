using System;
using MoreMountains.Feedbacks;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Feel feedback: a shake and/or a coloured flash on a player's camera,
    // through our own LocalPoolPowerEffectReceiver — Feel's camera shakes
    // would fight the FPS / aim / top-down cameras that already drive it.
    [AddComponentMenu("")]
    [Serializable]
    [FeedbackPath("Untitled Pool/Secousse et flash écran")]
    [FeedbackHelp("Secoue la caméra et/ou fait un flash de couleur, par notre propre système de caméra (celui des fautes et des pouvoirs). L'intensité de la séquence multiplie la force.")]
    public class MMF_PoolScreenJuice : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        [MMFInspectorGroup("Écran", true, 12)]
        [Tooltip("Sur l'écran de qui : le joueur concerné par le moment, celui dont c'est le tour, J1, J2 ou les deux.")]
        public FeelTarget target = FeelTarget.Everyone;
        [Tooltip("Force de la secousse, en mètres (0 = pas de secousse).")]
        [Min(0f)] public float shakeMagnitude = 0.03f;
        [Tooltip("Durée de la secousse, en secondes.")]
        [Min(0f)] public float shakeDuration = 0.2f;
        [Tooltip("Couleur du flash.")]
        public Color flashColor = new Color(1f, 0.2f, 0.15f);
        [Tooltip("Opacité maximale du flash (0 = pas de flash).")]
        [Range(0f, 1f)] public float flashAlpha = 0f;
        [Tooltip("Durée du flash, en secondes.")]
        [Min(0f)] public float flashDuration = 0.25f;

        public override float FeedbackDuration => Mathf.Max(shakeDuration, flashAlpha > 0f ? flashDuration : 0f);

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized) return;
            LocalPoolPowerEffectReceiver.PlayImpact(GameFeel.Resolve(target),
                shakeMagnitude * feedbacksIntensity, shakeDuration,
                flashColor, Mathf.Clamp01(flashAlpha * feedbacksIntensity), flashDuration);
        }
    }
}
