using PhysicsCharacterController;
using Unity.Netcode;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.Player
{
    // Add to the networked player prefab alongside NetworkObject/NetworkTransform.
    // On spawn: the owning client binds its local camera/input rig to this instance;
    // every other (remote) instance just gets driven by NetworkTransform, so its
    // CharacterManager is disabled to avoid it simulating movement locally too.
    [RequireComponent(typeof(CharacterManager))]
    public class NetworkPlayerRigLink : NetworkBehaviour
    {
        private CharacterManager characterManager;

        private void Awake()
        {
            characterManager = GetComponent<CharacterManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                characterManager.enabled = false;
                return;
            }

            LocalClientRig rig = LocalClientRig.Instance;
            if (rig == null)
            {
                Debug.LogError("No LocalClientRig found in the scene — cannot bind camera/input to the local player.");
                return;
            }

            characterManager.input = rig.InputReader;
            characterManager.characterCamera = rig.CameraFacingReference;

            rig.CameraManager.characterManager = characterManager;
            rig.CameraManager.inputReader = rig.InputReader;

            // The Cinemachine cameras need to be told which transform to actually
            // follow — in the non-networked demo this was wired once in the scene
            // to the single placed player; here it must be re-pointed at whichever
            // networked instance belongs to the local owner.
            rig.CameraManager.firstPersonCamera.Follow = characterManager.headPoint;
            if (characterManager.characterModel != null)
                rig.CameraManager.thirdPersonCamera.Follow = characterManager.characterModel.transform;

            rig.CameraManager.SetCamera();

            // Force true FPS behaviour for the local player: body always faces
            // the camera, Q/D strafe sideways instead of turning the character
            // toward the movement direction. Set explicitly (rather than relying
            // solely on CameraManager's own isThirdPersonDefault bookkeeping) so
            // it's guaranteed regardless of camera toggle state.
            characterManager.SetLockToCamera(true);
        }
    }
}
