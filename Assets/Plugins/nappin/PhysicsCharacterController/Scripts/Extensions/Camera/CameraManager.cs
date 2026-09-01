using UnityEngine;
using Unity.Cinemachine;
using System.Collections;


namespace PhysicsCharacterController
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera properties")]
        public CinemachineCamera firstPersonCamera;
        public CinemachineCamera thirdPersonCamera;
        public CharacterManager characterManager;

        [Space(10)]
        public bool isThirdPersonDefault = true;

        [Header("Mask first person")]
        public LayerMask firstPersonMask;
        public float fpMaskChangeDelay = 0.1f;
        public float fpHeightOnTransition = 0f;

        [Header("Mask third person")]
        public LayerMask thirdPersonMask;
        public float tpMaskChangeDelay = 0.1f;
        public float tpHeightOnTransition = 0.5f;

        [Header("References")]
        public InputReader inputReader;


        /**/


        private void Start()
        {
            SetCamera();
        }


        public void ToggleCamera()
        {
            isThirdPersonDefault = !isThirdPersonDefault;
            SetCamera();
        }


        public void SetCamera()
        {
            if (isThirdPersonDefault)
            {
                characterManager.SetLockToCamera(false);

                firstPersonCamera.gameObject.SetActive(false);
                thirdPersonCamera.gameObject.SetActive(true);

                StartCoroutine(UpdateMask(tpMaskChangeDelay, thirdPersonMask));
            }
            else
            {
                characterManager.SetLockToCamera(true);

                firstPersonCamera.gameObject.SetActive(true);
                thirdPersonCamera.gameObject.SetActive(false);

                StartCoroutine(UpdateMask(fpMaskChangeDelay, firstPersonMask));
            }
        }


        private IEnumerator UpdateMask(float duration, LayerMask mask)
        {
            yield return new WaitForSeconds(duration);
            Camera.main.cullingMask = mask;
        }
    }
}