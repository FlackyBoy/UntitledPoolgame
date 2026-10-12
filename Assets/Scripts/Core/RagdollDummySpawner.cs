using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Test-only cheat: press G to spawn an inert "dummy" copy of PlayerLocal
    // a few meters in front of the (first found) local player — lets the
    // ragdoll-on-impact feature be tested solo (throw the test object at the
    // dummy) without needing a real second local player joined through
    // PlayerInputManager/a second physical device. The dummy's PlayerInput
    // never gets paired to a device (spawned directly via Instantiate, not
    // PlayerInputManager.JoinPlayer), so it just stands there unresponsive
    // to input — its LocalPlayerRagdollController still reacts to collisions
    // normally, since OnCollisionEnter doesn't depend on input at all.
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

        // A dummy is a copy of the player: only its body stays live (it
        // still falls, reacts to hits and fills its hit gauge). Also used by
        // the R&D arena tool for the dummies placed in the scene.
        // - Spawned directly (not through PlayerInputManager.JoinPlayer),
        //   nothing stops its PlayerInput from auto-pairing to whatever
        //   device is free (typically the keyboard already driving player
        //   1): disabled outright.
        // - The controllers (move, R&D move, aim, hands) are off: it doesn't
        //   move or aim at all.
        // - Never joined, its Camera never gets a split-screen viewport rect
        //   (stays full-screen) — left on, it would draw over the real
        //   players' views; same for its AudioListener.
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
