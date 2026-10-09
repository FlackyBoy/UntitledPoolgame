using System;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Feel feedback: a big word on the HUD (MatchHud interjection) with its
    // TextFx tags — "<shake>FAUTE !</shake>" — on the half of the player the
    // moment belongs to. Written in our scripts, Feel itself untouched.
    [AddComponentMenu("")]
    [Serializable]
    [FeedbackPath("Untitled Pool/Texte du HUD (TextFx)")]
    [FeedbackHelp("Affiche un gros mot sur le HUD, avec les balises de texte animé (<shake>, <wave>, <pop>…). Prévisualiser et écrire le texte : Tools > Pool > Text FX Studio.")]
    public class MMF_PoolHudText : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        [MMFInspectorGroup("Texte", true, 12)]
        [Tooltip("Sur l'écran de qui : le joueur concerné par le moment, celui dont c'est le tour, J1, J2 ou les deux.")]
        public FeelTarget target = FeelTarget.EventPlayer;
        [Tooltip("Le texte, avec ses balises (<shake>, <wave>, <pop>…).")]
        [TextArea(2, 4)] public string text = "<shake>FAUTE !</shake>";
        [Tooltip("Couleur choisie ici ; sinon le jaune du HUD.")]
        public bool customColor;
        public Color color = new Color(1f, 0.3f, 0.24f);
        [Tooltip("Rayons qui tournent derrière le mot.")]
        public bool burst;

        public override float FeedbackDuration => MatchHud.ShoutDuration;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized) return;
            MatchHud.ShowShout(GameFeel.Resolve(target), text, customColor ? color : (Color?)null, burst);
        }
    }
}
