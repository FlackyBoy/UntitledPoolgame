using PhysicsCharacterController;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // Lives once per client on a scene object holding the (non-networked) Nappin
    // camera + input rig ([Cameras] / [InputSystem]). Networked player instances
    // look this up on spawn to bind themselves to it if they are the local player.
    public class LocalClientRig : MonoBehaviour
    {
        public static LocalClientRig Instance { get; private set; }

        [Header("Drag the components from the scene's [Cameras] / [InputSystem] objects")]
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private GameObject cameraFacingReference; // the object CharacterManager.characterCamera used to point to (e.g. Main Camera under [Cameras])

        public CameraManager CameraManager => cameraManager;
        public InputReader InputReader => inputReader;
        public GameObject CameraFacingReference => cameraFacingReference;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
