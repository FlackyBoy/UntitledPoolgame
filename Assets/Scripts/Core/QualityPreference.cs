using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // The quality level the player picked in Réglages > Vidéo, kept in
    // PlayerPrefs (by name, so reordering the levels in Project Settings
    // doesn't swap them) and put back before the first scene loads. The
    // choices offered are the levels listed in RenderQualitySettings.
    public static class QualityPreference
    {
        private const string PrefKey = "pool.quality";

        // The project's level for this platform, before any saved choice.
        public static int Default { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Default = QualitySettings.GetQualityLevel();
            int saved = IndexOf(PlayerPrefs.GetString(PrefKey, ""));
            if (saved >= 0 && saved != Default) QualitySettings.SetQualityLevel(saved, true);
        }

        public static RenderQualitySettings.Level[] Choices =>
            PoolSettingsLoader.LoadOrDefault<RenderQualitySettings>("RenderQualitySettings").levels;

        public static string Current
        {
            get
            {
                int index = QualitySettings.GetQualityLevel();
                return index >= 0 && index < QualitySettings.names.Length ? QualitySettings.names[index] : "";
            }
        }

        // False when no quality level has that name.
        public static bool Set(string qualityName)
        {
            int index = IndexOf(qualityName);
            if (index < 0) return false;
            QualitySettings.SetQualityLevel(index, true);
            PlayerPrefs.SetString(PrefKey, QualitySettings.names[index]);
            PlayerPrefs.Save();
            return true;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            if (Default >= 0) QualitySettings.SetQualityLevel(Default, true);
        }

        public static bool IsCurrent(string qualityName) =>
            string.Equals(Current, qualityName, System.StringComparison.OrdinalIgnoreCase);

        private static int IndexOf(string qualityName)
        {
            if (string.IsNullOrEmpty(qualityName)) return -1;
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], qualityName, System.StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
