using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Loads a config asset from Assets/Resources by name, falling back to a
    // fresh default instance (with a warning) when it hasn't been created —
    // so a missing asset degrades to default values instead of null refs.
    public static class PoolSettingsLoader
    {
        public static T LoadOrDefault<T>(string resourceName) where T : ScriptableObject
        {
            T settings = Resources.Load<T>(resourceName);
            if (settings != null) return settings;

            Debug.LogWarning($"{resourceName} asset not found in Assets/Resources — using fallback defaults. Run Tools > Pool > Ensure Config Assets Exist to create it.");
            return ScriptableObject.CreateInstance<T>();
        }
    }
}
