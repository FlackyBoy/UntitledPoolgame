using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Marks the main menu scene (created by Tools > Pool > Create Main Menu
    // Scene): SyntheseMenu shows itself there, with no match and no players
    // yet — they're decided in the menu and joined by the level (LevelLoader).
    public class MainMenuScene : MonoBehaviour
    {
        private void Awake()
        {
            // Back from a level (pause > Menu principal): nothing carried over.
            GameSession.Clear();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
