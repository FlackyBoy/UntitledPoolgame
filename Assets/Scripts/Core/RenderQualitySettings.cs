using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UntitledPoolGame.Core
{
    // What each quality level does on the players' cameras. Shadows, SSAO,
    // render scale live in the URP asset of the level (Assets/Settings/
    // PC_*_RPAsset); anti-aliasing and post-processing are per camera in URP,
    // so they are here and applied by CameraQuality.
    [CreateAssetMenu(fileName = "RenderQualitySettings", menuName = "Pool/Render Quality Settings")]
    public class RenderQualitySettings : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            [Tooltip("Nom du niveau de qualité (Project Settings > Quality), tel qu'il est écrit.")]
            public string qualityName = "Moyen";
            [Tooltip("Post-process sur la caméra du joueur : étalonnage, Bloom des néons, vignette, et les Volumes des ambiances. Sans lui, l'anticrénelage et la mise à l'échelle FSR ne s'appliquent pas non plus.")]
            public bool postProcessing = true;
            [Tooltip("Anticrénelage de la caméra. FXAA : le moins cher, un peu flou. SMAA : plus net, un peu plus cher.")]
            public AntialiasingMode antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            [Tooltip("Qualité du SMAA (sans effet pour les autres modes).")]
            public AntialiasingQuality smaaQuality = AntialiasingQuality.Medium;
        }

        [Tooltip("Un réglage par niveau de qualité. Un niveau absent de la liste prend « Par défaut ».")]
        public Level[] levels =
        {
            new Level { qualityName = "Bas", postProcessing = true, antialiasing = AntialiasingMode.None },
            new Level { qualityName = "Moyen", postProcessing = true, antialiasing = AntialiasingMode.FastApproximateAntialiasing },
            new Level { qualityName = "Haut", postProcessing = true, antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing, smaaQuality = AntialiasingQuality.High },
        };

        [Tooltip("Pour un niveau qui n'est pas dans la liste.")]
        public Level fallback = new Level { qualityName = "Par défaut" };

        public Level ForCurrentQuality()
        {
            int index = QualitySettings.GetQualityLevel();
            string current = index >= 0 && index < QualitySettings.names.Length ? QualitySettings.names[index] : null;
            foreach (Level level in levels)
                if (level != null && string.Equals(level.qualityName, current, StringComparison.OrdinalIgnoreCase)) return level;
            return fallback;
        }
    }
}
