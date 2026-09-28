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

            LocalFpsPlayerController player = FindFirstObjectByType<LocalFpsPlayerController>();
            Transform origin = player != null ? player.transform : transform;

            Vector3 position = origin.position + origin.forward * spawnDistance;
            Quaternion rotation = Quaternion.LookRotation(-origin.forward, Vector3.up);

            GameObject dummy = Instantiate(dummyPrefab, position, rotation);

            // Spawned directly (not through PlayerInputManager.JoinPlayer),
            // so nothing stops its PlayerInput from auto-pairing to whatever
            // device is free (typically the keyboard already driving player
            // 1) — disabling it outright is the simplest way to guarantee
            // the dummy stays inert regardless of that. It doesn't need to
            // move or aim at all, just stand there and react to collisions
            // (LocalPlayerRagdollController doesn't depend on input).
            if (dummy.TryGetComponent(out PlayerInput dummyInput)) dummyInput.enabled = false;
            if (dummy.TryGetComponent(out LocalFpsPlayerController dummyFps)) dummyFps.enabled = false;
            if (dummy.TryGetComponent(out LocalPoolAimController dummyAim)) dummyAim.enabled = false;
            if (dummy.TryGetComponent(out LocalPlayerHandController dummyHand)) dummyHand.enabled = false;

            // Never joined through PlayerInputManager, so its Camera never
            // gets assigned a split-screen viewport rect (stays full-screen
            // by default) — left enabled, it renders on top of/instead of
            // the real players' own split views. Not needed for a dummy
            // that only exists to be shot at.
            Camera dummyCamera = dummy.GetComponentInChildren<Camera>(true);
            if (dummyCamera != null) dummyCamera.enabled = false;
            AudioListener dummyListener = dummy.GetComponentInChildren<AudioListener>(true);
            if (dummyListener != null) dummyListener.enabled = false;

            Debug.Log("[RagdollDummySpawner] Spawned a dummy.");
        }
    }
}
