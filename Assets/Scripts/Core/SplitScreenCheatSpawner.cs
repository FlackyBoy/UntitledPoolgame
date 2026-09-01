using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Core
{
    // Test-only cheat: hold C+P together to force-join a second local player,
    // without needing to press a button on a second physical device. Prefers
    // pairing the new player with a connected gamepad (for a clean keyboard vs.
    // gamepad split-screen test); falls back to whatever PlayerInputManager
    // picks by default if no gamepad is present. Requires a PlayerInputManager
    // in the scene.
    public class SplitScreenCheatSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerInputManager playerInputManager;

        private bool comboWasActive;

        private void Awake()
        {
            if (playerInputManager == null)
                playerInputManager = FindFirstObjectByType<PlayerInputManager>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool comboActive = keyboard.cKey.isPressed && keyboard.pKey.isPressed;

            if (comboActive && !comboWasActive)
                SpawnSecondPlayer();

            comboWasActive = comboActive;
        }

        private void SpawnSecondPlayer()
        {
            if (playerInputManager == null)
            {
                Debug.LogWarning("[SplitScreenCheat] No PlayerInputManager found in the scene.");
                return;
            }

            if (playerInputManager.playerCount >= playerInputManager.maxPlayerCount)
            {
                Debug.LogWarning("[SplitScreenCheat] Already at max player count.");
                return;
            }

            if (Gamepad.current != null)
                playerInputManager.JoinPlayer(pairWithDevice: Gamepad.current);
            else
                playerInputManager.JoinPlayer();

            Debug.Log("[SplitScreenCheat] Joined a player via C+P.");
        }
    }
}
