using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // What the main menu decided, carried over to the level scene: the
    // level, the game type, and the device of each player (J1, J2…) as they
    // joined in the menu — the level then joins its players with exactly
    // those devices (LevelLoader). Static: survives the scene change.
    public static class GameSession
    {
        // True from the moment a level is launched from the menu until the
        // player goes back to the menu: the level scene then doesn't show the
        // menu itself.
        public static bool Active { get; private set; }
        public static LevelEntry Level { get; private set; }
        public static PoolGameMode Mode { get; private set; }
        public static PoolPartyMode Party { get; private set; }
        public static int TargetScore { get; private set; }
        public static readonly List<InputDevice> Devices = new List<InputDevice>();

        public static void Begin(LevelEntry level, PoolGameMode mode, PoolPartyMode party, int targetScore, IEnumerable<InputDevice> devices)
        {
            Active = true;
            Level = level;
            Mode = mode;
            Party = party;
            TargetScore = targetScore;
            Devices.Clear();
            foreach (InputDevice d in devices) if (d != null && !Devices.Contains(d)) Devices.Add(d);
        }

        public static void Clear()
        {
            Active = false;
            Level = null;
            Devices.Clear();
        }

        public static string ModeName(PoolGameMode mode) => mode switch
        {
            PoolGameMode.NineBall => "9-ball",
            PoolGameMode.FourteenOne => "14.1",
            PoolGameMode.Party => "Pouvoirs",
            _ => "Classique",
        };

        // Back to the main menu scene (from the pause); the current scene is
        // reloaded instead if the menu scene isn't in the build.
        public static void LoadMainMenu()
        {
            Clear();
            Time.timeScale = 1f;
            string menu = GameFlowSettings.Instance.menuScene;
            if (!string.IsNullOrEmpty(menu) && Application.CanStreamedLevelBeLoaded(menu))
                UnityEngine.SceneManagement.SceneManager.LoadScene(menu);
            else
            {
                Debug.LogWarning($"[Menu] Scène du menu « {menu} » absente du build : la scène actuelle est rechargée. Tools > Pool > Create Main Menu Scene.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
