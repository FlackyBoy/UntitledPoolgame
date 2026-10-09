using System;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // How strong and how fast a text effect plays (TextFx).
    [Serializable]
    public class TextFxTuning
    {
        [Tooltip("Force de l'effet (1 = normal). Modifiable dans le texte : <wave a=2>.")]
        [Min(0f)] public float amplitude = 1f;
        [Tooltip("Vitesse de l'effet (1 = normal). Modifiable dans le texte : <wave s=2>.")]
        [Min(0f)] public float speed = 1f;
    }

    // Letter-by-letter text animation settings (TextFx, inspired by Febucci's
    // Text Animator): the persistent effects written as tags in any UI text
    // (<wave>, <shake>…), how letters appear (pop, drop…), the typewriter
    // and the way texts leave. Assets/Resources/TextFxSettings, created by
    // Tools > Pool > Ensure Config Assets Exist; read every frame, so changes
    // show at once in Play mode. Tooltips in French: read by the designer.
    [CreateAssetMenu(fileName = "TextFxSettings", menuName = "Pool/Text Fx Settings")]
    public class TextFxSettings : ScriptableObject
    {
        [Header("Effets permanents (balises dans les textes)")]
        [Tooltip("<wave> : les lettres ondulent de haut en bas.")]
        public TextFxTuning wave = new TextFxTuning();
        [Tooltip("<shake> : les lettres tremblent (colère, choc).")]
        public TextFxTuning shake = new TextFxTuning();
        [Tooltip("<wiggle> : chaque lettre gigote doucement de son côté.")]
        public TextFxTuning wiggle = new TextFxTuning();
        [Tooltip("<bounce> : les lettres rebondissent l'une après l'autre.")]
        public TextFxTuning bounce = new TextFxTuning();
        [Tooltip("<pulse> : les lettres grossissent et rapetissent.")]
        public TextFxTuning pulse = new TextFxTuning();
        [Tooltip("<swing> : les lettres se balancent (rotation).")]
        public TextFxTuning swing = new TextFxTuning();
        [Tooltip("<rainbow> : les couleurs défilent sur les lettres.")]
        public TextFxTuning rainbow = new TextFxTuning();

        [Header("Apparition des lettres")]
        [Tooltip("Apparition quand un texte arrive à l'écran sans balise d'apparition (pop, drop, fade, slide, grow, none).")]
        public TextFxEntrance defaultEntrance = TextFxEntrance.Pop;
        [Tooltip("Durée de l'apparition d'une lettre, en secondes.")]
        [Min(0.01f)] public float entranceDuration = 0.35f;
        [Tooltip("Décalage entre deux lettres quand un texte arrive d'un coup (pas en machine à écrire), en secondes.")]
        [Min(0f)] public float revealStagger = 0.03f;

        [Header("Machine à écrire")]
        [Tooltip("Lettres par seconde.")]
        [Min(1f)] public float lettersPerSecond = 40f;
        [Tooltip("Pause après . ! ? … en secondes.")]
        [Min(0f)] public float sentencePause = 0.3f;
        [Tooltip("Pause après , ; : en secondes.")]
        [Min(0f)] public float commaPause = 0.12f;

        [Header("Disparition")]
        [Tooltip("Durée de la disparition d'une lettre, en secondes.")]
        [Min(0.01f)] public float exitDuration = 0.25f;
        [Tooltip("Décalage entre deux lettres qui disparaissent, en secondes.")]
        [Min(0f)] public float exitStagger = 0.015f;

        private static TextFxSettings instance;
        public static TextFxSettings Instance =>
            instance != null ? instance : instance = Pool.PoolSettingsLoader.LoadOrDefault<TextFxSettings>("TextFxSettings");
    }

    public enum TextFxEntrance { None, Pop, Drop, Fade, Slide, Grow }
}
