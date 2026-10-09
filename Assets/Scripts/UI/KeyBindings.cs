using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Core
{
    // Player key bindings chosen in the settings screen (KeyBindingsPanel):
    // stored as Input System binding overrides (JSON) in PlayerPrefs, and
    // applied to every player's own copy of the actions — PlayerInput clones
    // the actions asset per player, so a player who joins later (P2) gets
    // them too (KeyBindingsApplier watches PlayerInput.all).
    public static class KeyBindings
    {
        private const string PrefKey = "UntitledPoolGame.KeyBindings";

        public static string Json
        {
            get => PlayerPrefs.GetString(PrefKey, "");
            private set { PlayerPrefs.SetString(PrefKey, value); PlayerPrefs.Save(); }
        }

        public static void ApplyTo(InputActionAsset actions)
        {
            if (actions == null) return;
            actions.RemoveAllBindingOverrides();
            string json = Json;
            if (!string.IsNullOrEmpty(json)) actions.LoadBindingOverridesFromJson(json);
        }

        // After a change made on one player's actions: saved, then copied to
        // every other player.
        public static void SaveFrom(InputActionAsset actions)
        {
            if (actions == null) return;
            Json = actions.SaveBindingOverridesAsJson();
            foreach (PlayerInput player in PlayerInput.all)
                if (player.actions != actions) ApplyTo(player.actions);
        }

        public static void ResetAll()
        {
            Json = "";
            foreach (PlayerInput player in PlayerInput.all) ApplyTo(player.actions);
        }
    }

    // Applies the saved bindings to each player as they appear (scene start,
    // P2 joining). Created once, kept across scenes.
    public class KeyBindingsApplier : MonoBehaviour
    {
        private readonly HashSet<PlayerInput> applied = new HashSet<PlayerInput>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<KeyBindingsApplier>() != null) return;
            var go = new GameObject("Key bindings") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<KeyBindingsApplier>();
        }

        private void Update()
        {
            applied.RemoveWhere(p => p == null);
            foreach (PlayerInput player in PlayerInput.all)
                if (applied.Add(player)) KeyBindings.ApplyTo(player.actions);
        }
    }
}
