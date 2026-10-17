using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Core
{
    // Moves each joining player to the scene's spawn point for its index
    // (PlayerInputManager has no spawn points of its own).
    [RequireComponent(typeof(PlayerInputManager))]
    public class ScenePlayerSpawner : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;

        private PlayerInputManager playerInputManager;

        private void Awake()
        {
            playerInputManager = GetComponent<PlayerInputManager>();
        }

        private void OnEnable()
        {
            playerInputManager.onPlayerJoined += OnPlayerJoined;
        }

        private void OnDisable()
        {
            playerInputManager.onPlayerJoined -= OnPlayerJoined;
        }

        private void OnPlayerJoined(PlayerInput playerInput)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return;

            Transform spawnPoint = spawnPoints[playerInput.playerIndex % spawnPoints.Length];
            playerInput.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }
    }
}
