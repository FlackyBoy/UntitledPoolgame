using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Core
{
    // PlayerInputManager doesn't have a built-in spawn point concept — it
    // just instantiates the Player Prefab at whatever position is baked
    // into the prefab itself. This repositions each joining player to a
    // scene-specific spawn point Transform instead, indexed by join order
    // (player 0 -> spawnPoints[0], player 1 -> spawnPoints[1], etc.).
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
