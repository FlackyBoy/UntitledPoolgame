using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Put on a player prefab placed by hand in a scene, to test its body
    // (falls, hits, scale) next to the real players: otherwise its
    // PlayerInput grabs the same keyboard and both move together.
    // Runs before PlayerInput enables, so it never pairs a device.
    [DefaultExecutionOrder(-10000)]
    public class InertDummy : MonoBehaviour
    {
        private void Awake() => RagdollDummySpawner.MakeInert(gameObject);
    }
}
