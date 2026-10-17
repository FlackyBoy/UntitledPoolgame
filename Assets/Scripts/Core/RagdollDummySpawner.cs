using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Test cheat: G spawns an inert copy of the player a few metres in front,
    // to test knockdowns alone. It's never paired to a device, but still
    // reacts to hits.
    public class RagdollDummySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject dummyPrefab;
        [SerializeField] private float spawnDistance = 3f;

        private bool keyWasPressed;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool keyPressed = keyboard.gKey.isPressed;
            if (keyPressed && !keyWasPressed)
                SpawnDummy();
            keyWasPressed = keyPressed;
        }

        private void SpawnDummy()
        {
            if (dummyPrefab == null)
            {
                Debug.LogWarning("[RagdollDummySpawner] No Dummy Prefab assigned.");
                return;
            }

            LocalFpsPlayerController player = FindAnyObjectByType<LocalFpsPlayerController>();
            Transform origin = player != null ? player.transform : transform;

            Vector3 position = origin.position + origin.forward * spawnDistance;
            Quaternion rotation = Quaternion.LookRotation(-origin.forward, Vector3.up);

            GameObject dummy = Instantiate(dummyPrefab, position, rotation);
            MakeInert(dummy);
            Debug.Log("[RagdollDummySpawner] Spawned a dummy.");
        }

        // Only the body of a dummy stays live (falls, reacts, fills its gauge).
        // Also used by the R&D arena. Its PlayerInput would grab a free device,
        // its controllers would move it, and its camera and AudioListener
        // would draw over the players': all off.
        public static void MakeInert(GameObject dummy)
        {
            foreach (PlayerInput c in dummy.GetComponentsInChildren<PlayerInput>(true)) c.enabled = false;
            foreach (LocalFpsPlayerController c in dummy.GetComponentsInChildren<LocalFpsPlayerController>(true)) c.enabled = false;
            foreach (LocalPlayerLocomotion c in dummy.GetComponentsInChildren<LocalPlayerLocomotion>(true)) c.enabled = false;
            foreach (LocalPoolAimController c in dummy.GetComponentsInChildren<LocalPoolAimController>(true)) c.enabled = false;
            foreach (LocalPlayerHandController c in dummy.GetComponentsInChildren<LocalPlayerHandController>(true)) c.enabled = false;
            foreach (Camera c in dummy.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (AudioListener c in dummy.GetComponentsInChildren<AudioListener>(true)) c.enabled = false;
        }
    }
}
