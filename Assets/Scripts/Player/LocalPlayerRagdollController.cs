using Unity.Cinemachine;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // Knockdown and get-up, on top of PuppetMaster's BehaviourPuppet (offline
    // only for now). BehaviourPuppet does the physics, the impact detection
    // and triggers the get-up clips; this class reacts to its onLoseBalance /
    // onRegainBalance events (wired in the Inspector) and handles what it
    // can't know about: our controllers, the held object, the camera and the
    // CharacterController root.
    //
    // The animated model (modelTransform) is a child of this root and
    // follows it through LocalCharacterModelFollow, as in RootMotion's demo.
    // The follow is off during the ragdoll; on recovery the root is moved to
    // where PuppetMaster left the model, then the follow comes back on.
    [RequireComponent(typeof(CharacterController))]
    public class LocalPlayerRagdollController : MonoBehaviour
    {
        [Header("Assigned by hand")]
        // Runs the knockdown and get-up; this script only reacts to it.
        [SerializeField] private BehaviourPuppet behaviourPuppet;
        // Locomotion parameters are zeroed on knockdown so a stale value
        // doesn't stay frozen in while the animation controller is off.
        [SerializeField] private Animator animator;
        // onRegainBalance fires before the get-up clip ends: control comes
        // back this long after it. Keep it close to the clip length: too short
        // and the model is read mid-clip, too long and the muscles have
        // already sagged.
        [SerializeField] private float getUpRecoveryDelay = 1f;
        // PuppetMaster's Animated Target, child of this object, with a
        // LocalCharacterModelFollow.
        [SerializeField] private Transform modelTransform;

        // A single camera for the fall: the FPS camera pulls back behind the
        // ragdoll, then returns to the eyes as the body gets up (a separate
        // fall camera made the return come from far away).
        [Header("Cameras (the FPS camera itself pulls back during the fall)")]
        [SerializeField] private CinemachineCamera fpsVirtualCamera;
        [Tooltip("Ancienne caméra de chute : n'est plus utilisée (gardée à la priorité inactive). Peut être retirée du prefab.")]
        [SerializeField] private CinemachineCamera ragdollVirtualCamera;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        [Header("Fall view (3rd person, behind/above the pelvis)")]
        [SerializeField] private float ragdollCameraDistance = 2.5f;
        [SerializeField] private float ragdollCameraHeight = 1.5f;
        [Tooltip("Durée (secondes) du recul de la caméra FPS derrière le corps quand le joueur tombe.")]
        [SerializeField] private float cameraPullBackTime = 0.35f;
        [Tooltip("Durée (secondes) du retour de la caméra dans les yeux, à partir du début du relevé.")]
        [SerializeField] private float cameraReturnTime = 0.6f;

        [Tooltip("Logs [CamReturn] au relevé et pendant les images qui suivent : position de la caméra FPS, de son point de repos et de l'image finale.")]
        [SerializeField] private bool traceCameraReturn = true;
        private int cameraReturnTraceFrames;
        private CinemachineBrain brain;

        // View yaw during the fall (where the player was looking). The root
        // takes it back on recovery so the view doesn't turn.
        private float ragdollCameraYaw;

        // 0 = camera in the eyes, 1 = full fall view.
        private float cameraPull;
        private bool cameraDetached;
        private bool hasCameraRest;
        private Vector3 cameraRestLocalPosition;
        private Quaternion cameraRestLocalRotation;
        // The eye pose relative to the root, for where the eyes will be once
        // the root is moved onto the body that got up.
        private Vector3 eyePositionInRoot;
        private Quaternion eyeRotationInRoot;

        // The IK would fight the ragdoll: off for the same time as the
        // controllers.
        [Header("FinalIK (disabled during ragdoll)")]
        [SerializeField] private IK[] ikComponents;

        private CharacterController characterController;
        private LocalFpsPlayerController fpsController;
        private LocalPoolAimController aimController;
        private LocalCharacterAnimationController animController;
        private LocalPlayerHandController handController;
        private LocalCharacterModelFollow modelFollow;

        private bool isRagdollCameraActive;

        // Others that move the camera leave it alone while this is on.
        public bool IsRagdollCameraActive => isRagdollCameraActive;

        // Called by RagdollHitRelay on a strong enough hit. BehaviourPuppet
        // only knocks the puppet down once SetHasCollided was called since it
        // last got up, and its own trigger for that proved unreliable.
        public void NotifyCollision()
        {
            if (behaviourPuppet != null)
                behaviourPuppet.SetHasCollided(true);
        }

        private static bool HasFloatParameter(Animator animator, string parameterName)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == parameterName)
                    return true;
            }
            return false;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            fpsController = GetComponent<LocalFpsPlayerController>();
            aimController = GetComponent<LocalPoolAimController>();
            animController = GetComponent<LocalCharacterAnimationController>();
            handController = GetComponent<LocalPlayerHandController>();
            if (modelTransform != null) modelFollow = modelTransform.GetComponent<LocalCharacterModelFollow>();

            if (fpsVirtualCamera != null) fpsVirtualCamera.Priority = activePriority;
            if (ragdollVirtualCamera != null) ragdollVirtualCamera.Priority = inactivePriority;
        }

        // Wired to BehaviourPuppet's onLoseBalance, which fires however the
        // puppet went down.
        public void OnKnockedDown()
        {
            // Knocked down again while getting up: the recovery planned by the
            // last OnRegainBalance mustn't run, or the character stands up
            // without its ragdoll.
            if (recoverRoutine != null)
            {
                StopCoroutine(recoverRoutine);
                recoverRoutine = null;
                Debug.Log($"[Ragdoll] {name}: knocked down again while getting up — pending recovery cancelled", this);
            }

            Debug.Log($"[Ragdoll] {name}: knocked down (ragdoll mode on)", this);

            if (handController != null && handController.HeldObject != null)
                handController.ForceDrop();

            // Whatever was on comes back on afterwards (a dummy keeps its
            // controllers off). Only on the first knockdown.
            if (!isRagdollCameraActive) RememberEnabled();

            characterController.enabled = false;
            if (fpsController != null) fpsController.enabled = false;
            if (aimController != null) aimController.enabled = false;
            if (animController != null) animController.enabled = false;
            // Lets PuppetMaster move the model while it's down.
            if (modelFollow != null) modelFollow.enabled = false;
            if (ikComponents != null)
                foreach (IK ik in ikComponents)
                    if (ik != null) ik.enabled = false;

            // Otherwise the last speed stays frozen in the Animator and sends
            // the get-up straight back into locomotion. Only the parameters
            // the controller has (names differ between controllers).
            if (animator != null)
            {
                foreach (string floatParameterName in new[] { "Speed", "MoveX", "MoveY", "Forward", "Right" })
                {
                    if (HasFloatParameter(animator, floatParameterName))
                        animator.SetFloat(floatParameterName, 0f);
                }
            }

            // The view pulls back from the eyes, keeping the direction the
            // player was looking.
            if (!isRagdollCameraActive)
            {
                ragdollCameraYaw = transform.eulerAngles.y;
                DetachCamera();
            }
            isRagdollCameraActive = true;
        }

        // What OnKnockedDown switched off, restored by OnRecovered. All on by
        // default (a player).
        private (bool characterController, bool fps, bool aim, bool anim, bool[] ik) wasEnabled = (true, true, true, true, null);

        private void RememberEnabled()
        {
            bool[] ik = null;
            if (ikComponents != null)
            {
                ik = new bool[ikComponents.Length];
                for (int i = 0; i < ik.Length; i++) ik[i] = ikComponents[i] != null && ikComponents[i].enabled;
            }
            // Aiming and the top-down view switch the FPS controller off for
            // their own duration, and the fall ends them: it counts as on, or
            // the player got up with no look or move.
            bool aimTookFps = aimController != null && (aimController.IsAiming || aimController.IsPlacementViewActive);
            wasEnabled = (characterController.enabled,
                fpsController != null && (fpsController.enabled || aimTookFps),
                aimController != null && aimController.enabled,
                animController != null && animController.enabled,
                ik);
        }

        private void DetachCamera()
        {
            if (fpsVirtualCamera == null) return;
            Transform cam = fpsVirtualCamera.transform;
            if (!hasCameraRest)
            {
                cameraRestLocalPosition = cam.localPosition;
                cameraRestLocalRotation = cam.localRotation;
                hasCameraRest = true;
            }
            // Eye pose from the rest pose (not the current one: knocked down
            // again while the camera is still coming back).
            Transform parent = cam.parent;
            Vector3 eyePosition = parent != null ? parent.TransformPoint(cameraRestLocalPosition) : cameraRestLocalPosition;
            Quaternion eyeRotation = (parent != null ? parent.rotation : Quaternion.identity) * cameraRestLocalRotation;
            eyePositionInRoot = transform.InverseTransformPoint(eyePosition);
            eyeRotationInRoot = Quaternion.Inverse(transform.rotation) * eyeRotation;
            cameraDetached = true;
        }

        // Wired to BehaviourPuppet's onRegainBalance; waits out Get Up
        // Recovery Delay before handing control back.
        public void OnRegainBalance()
        {
            if (recoverRoutine != null) StopCoroutine(recoverRoutine);
            recoverRoutine = StartCoroutine(RecoverAfterDelay());
        }

        private Coroutine recoverRoutine;

        private System.Collections.IEnumerator RecoverAfterDelay()
        {
            yield return new WaitForSeconds(getUpRecoveryDelay);
            recoverRoutine = null;

            // Only if the puppet really is back on its feet: if it lost its
            // balance again meanwhile, the next OnRegainBalance will recover.
            if (behaviourPuppet != null && behaviourPuppet.state != BehaviourPuppet.State.Puppet)
            {
                Debug.Log($"[Ragdoll] {name}: recovery skipped — puppet is {behaviourPuppet.state}, waiting for it to regain balance again", this);
                yield break;
            }
            OnRecovered();
        }

        private void OnRecovered()
        {
            // PuppetMaster left the model where it got up: the root goes to
            // the spot that puts the model there at its rest offset, which
            // brings the CharacterController along without touching a muscle.
            // Got up behind a wall (thrown through it): the puppet is brought
            // back to the fall spot instead and the root stays.
            RaycastHit wall = default;
            bool behindWall = modelTransform != null && Physics.Linecast(
                transform.position + Vector3.up, modelTransform.position + Vector3.up,
                out wall, lostRagdollBlockers, QueryTriggerInteraction.Ignore);
            if (traceCameraReturn)
                Debug.Log($"[CamReturn] {name} recovery branch: {(behindWall ? "behind a wall (root stays)" : modelTransform != null && modelFollow != null ? "follow the model" : "NO model / model follow — root stays at the fall spot")}", this);
            if (behindWall && behaviourPuppet != null && behaviourPuppet.puppetMaster != null)
            {
                Debug.LogWarning($"[Ragdoll] {name}: got up behind '{wall.collider.name}' ({Vector3.Distance(transform.position, modelTransform.position):F2} m from the fall spot) — brought back to the fall spot", this);
                behaviourPuppet.puppetMaster.Teleport(transform.position, transform.rotation, true);
                if (modelFollow != null)
                {
                    modelFollow.ResyncNow();
                    modelFollow.enabled = true;
                }
            }
            else if (modelTransform != null && modelFollow != null)
            {
                Vector3 currentModelWorldPosition = modelTransform.position;
                Quaternion currentModelWorldRotation = modelTransform.rotation;

                // The root stays level (yaw only).
                Quaternion fullRootRotation = currentModelWorldRotation * Quaternion.Inverse(modelFollow.LocalRestRotation);
                // The root faces where the view has looked all along, so
                // handing control back doesn't turn the view.
                float bodyYaw = fullRootRotation.eulerAngles.y;
                float viewYaw = fpsVirtualCamera != null ? ragdollCameraYaw : bodyYaw;
                Quaternion newRootRotation = Quaternion.Euler(0f, viewYaw, 0f);
                Vector3 newRootPosition = currentModelWorldPosition - newRootRotation * modelFollow.LocalRestPosition;

                if (traceCameraReturn)
                    Debug.Log($"[CamReturn] {name} recovery: root {transform.position:F2} → {newRootPosition:F2}, model {currentModelWorldPosition:F2}, " +
                              $"pelvis {(behaviourPuppet != null && behaviourPuppet.puppetMaster != null && behaviourPuppet.puppetMaster.muscles.Length > 0 ? behaviourPuppet.puppetMaster.muscles[0].rigidbody.position.ToString("F2") : "—")}, " +
                              $"body yaw {bodyYaw:F0}, view yaw {ragdollCameraYaw:F0} → root yaw {viewYaw:F0}, camera pull {cameraPull:F2}, " +
                              $"camera {(fpsVirtualCamera != null ? fpsVirtualCamera.transform.position.ToString("F2") : "—")}", this);

                characterController.enabled = false;
                transform.SetPositionAndRotation(newRootPosition, newRootRotation);

                // Back to the rest offset and fresh smoothing, so the follow
                // doesn't lerp in from the old position.
                modelFollow.ResyncNow();
                modelFollow.enabled = true;

                // The muscles are left alone: teleporting them only made
                // things worse. A short Get Up Recovery Delay is the fix.
            }

            Debug.Log($"[Ragdoll] {name}: recovered — control handed back{(behindWall ? " (at the fall spot)" : "")}", this);
            characterController.enabled = wasEnabled.characterController;
            if (fpsController != null) fpsController.enabled = wasEnabled.fps;
            if (aimController != null) aimController.enabled = wasEnabled.aim;
            if (animController != null) animController.enabled = wasEnabled.anim;
            if (ikComponents != null)
                for (int i = 0; i < ikComponents.Length; i++)
                    if (ikComponents[i] != null) ikComponents[i].enabled = wasEnabled.ik == null || i >= wasEnabled.ik.Length || wasEnabled.ik[i];

            // The camera keeps going (UpdateFallCamera): usually already back
            // in the eyes, otherwise it finishes its return from there.
            isRagdollCameraActive = false;
            if (traceCameraReturn) cameraReturnTraceFrames = 8;
        }

        // Diagnostic of the return to the FPS view: where each camera is and
        // looks, frame by frame, and how far the output camera is from each.
        private void TraceCameraReturn()
        {
            if (cameraReturnTraceFrames <= 0) return;
            cameraReturnTraceFrames--;
            if (brain == null) brain = GetComponentInChildren<CinemachineBrain>(true);
            string Describe(Transform t) => t == null ? "—" : $"{t.position:F2} fwd {t.forward:F2}";
            Transform output = brain != null ? brain.transform : null;
            Transform camParent = fpsVirtualCamera != null ? fpsVirtualCamera.transform.parent : null;
            Debug.Log($"[CamReturn] {name} frame {8 - cameraReturnTraceFrames}: root yaw {transform.eulerAngles.y:F0}, " +
                      $"FPS cam {Describe(fpsVirtualCamera != null ? fpsVirtualCamera.transform : null)}, " +
                      $"eyes {(camParent != null ? camParent.TransformPoint(cameraRestLocalPosition).ToString("F2") : "—")}, pull {cameraPull:F2}, detached {cameraDetached}, " +
                      $"output {Describe(output)}, blending {(brain != null && brain.IsBlending)}", this);
        }

        // Safety net: the player has control while the ragdoll lies on the
        // floor. Going through OnKnockedDown again lets it get up normally.
        [Tooltip("Délai (secondes) au bout duquel un ragdoll resté au sol alors que le joueur a le contrôle est remis en mode chute pour qu'il se relève normalement. 0 = désactivé.")]
        [SerializeField] private float desyncRecoveryDelay = 0.5f;
        private float desyncTime;

        // A ragdoll thrown hard can end up through a wall or under the floor.
        // While down, crossing geometry between two frames, falling well below
        // the knockdown spot or staying down too long teleports it back there.
        [Header("Lost ragdoll (through a wall, under the floor)")]
        [Tooltip("Couches du décor que le ragdoll ne doit pas traverser (murs, sol). Vide = tout sauf les couches du ragdoll et des joueurs.")]
        [SerializeField] private LayerMask lostRagdollBlockers;
        [Tooltip("Mètres sous la position du joueur à partir desquels le ragdoll est considéré comme tombé sous le sol.")]
        [SerializeField] private float lostBelowDistance = 2f;
        [Tooltip("Secondes au sol maximum avant que le ragdoll soit ramené quoi qu'il arrive. 0 = désactivé.")]
        [SerializeField] private float maxDownTime = 10f;
        private float downTime;
        private bool hasLastPelvis;
        private Vector3 lastPelvis;
        private float teleportCooldown;
        private float nextDownTrace;

        private void Start()
        {
            if (lostRagdollBlockers.value == 0)
                lostRagdollBlockers = ~LayerMask.GetMask("Ragdoll", "Player", "PlayerAnimated");

            // Continuous collision, or a fast ragdoll goes through thin walls.
            int continuous = 0;
            if (behaviourPuppet != null && behaviourPuppet.puppetMaster != null)
                foreach (Muscle muscle in behaviourPuppet.puppetMaster.muscles)
                    if (muscle.rigidbody != null)
                    {
                        muscle.rigidbody.collisionDetectionMode = muscle.rigidbody.isKinematic
                            ? CollisionDetectionMode.ContinuousSpeculative
                            : CollisionDetectionMode.ContinuousDynamic;
                        continuous++;
                    }
            Debug.Log($"[Ragdoll] {name}: {continuous} muscles set to continuous collision" +
                      (behaviourPuppet == null ? " — no Behaviour Puppet assigned, the lost-ragdoll checks can't run" : ""), this);
        }

        private void CheckLostRagdoll()
        {
            PuppetMaster puppet = behaviourPuppet != null ? behaviourPuppet.puppetMaster : null;
            if (puppet == null || puppet.muscles.Length == 0 || puppet.muscles[0].rigidbody == null) return;

            downTime += Time.deltaTime;

            // Diagnostic: once a second while down, where the ragdoll is
            // relative to the spot it fell from.
            if (Time.time >= nextDownTrace)
            {
                nextDownTrace = Time.time + 1f;
                Vector3 p = puppet.muscles[0].rigidbody.position;
                Vector3 flat = Vector3.ProjectOnPlane(p - transform.position, Vector3.up);
                Debug.Log($"[Ragdoll] {name}: down {downTime:F1} s | puppet {behaviourPuppet.state} | pelvis {flat.magnitude:F2} m from the fall spot, {p.y - transform.position.y:F2} m above it | " +
                          $"pelvis collision {puppet.muscles[0].rigidbody.collisionDetectionMode}, layer {LayerMask.LayerToName(puppet.muscles[0].rigidbody.gameObject.layer)}", this);
            }

            // PuppetMaster applies a teleport on its next read: the jump it
            // makes must not be mistaken for passing through a wall.
            if (teleportCooldown > 0f) { teleportCooldown -= Time.deltaTime; hasLastPelvis = false; return; }
            Vector3 pelvis = puppet.muscles[0].rigidbody.position;

            // Crossed a wall or the floor since last frame?
            RaycastHit crossed = default;
            bool through = hasLastPelvis && Physics.Linecast(lastPelvis, pelvis, out crossed, lostRagdollBlockers, QueryTriggerInteraction.Ignore);
            lastPelvis = pelvis;
            hasLastPelvis = true;
            bool below = pelvis.y < transform.position.y - lostBelowDistance;
            bool tooLong = maxDownTime > 0f && downTime > maxDownTime;

            if (!through && !below && !tooLong) return;

            string reason = through ? $"passed through '{crossed.collider.name}'" : below ? "under the floor" : $"down for {downTime:F0} s";
            Debug.LogWarning($"[Ragdoll] {name}: ragdoll lost ({reason}) — teleported back to where it was knocked down", this);
            puppet.Teleport(transform.position, transform.rotation, true);
            downTime = 0f;
            hasLastPelvis = false;
            teleportCooldown = 0.3f;
        }

        // A standing ragdoll held away from its animated body (stuck behind a
        // wall) for too long is snapped back onto it.
        [Tooltip("Écart maximal (mètres) entre le bassin du ragdoll debout et celui du personnage animé ; au-delà, pendant Stuck Ragdoll Time, le ragdoll est coincé (contre un mur…) et il est replacé sur le personnage. 0 = désactivé.")]
        [SerializeField] private float maxRagdollSeparation = 0.75f;
        [Tooltip("Secondes pendant lesquelles le ragdoll doit rester trop loin du personnage animé avant d'être replacé.")]
        [SerializeField] private float stuckRagdollTime = 0.5f;
        private float stuckTime;

        private void CheckStuckRagdoll()
        {
            PuppetMaster puppet = behaviourPuppet != null ? behaviourPuppet.puppetMaster : null;
            if (maxRagdollSeparation <= 0f || puppet == null || puppet.muscles.Length == 0 || behaviourPuppet.state != BehaviourPuppet.State.Puppet)
            {
                stuckTime = 0f;
                return;
            }
            Muscle pelvis = puppet.muscles[0];
            if (pelvis.rigidbody == null || pelvis.target == null) return;

            float gap = Vector3.Distance(pelvis.rigidbody.position, pelvis.target.position);
            stuckTime = gap > maxRagdollSeparation ? stuckTime + Time.deltaTime : 0f;
            if (stuckTime < stuckRagdollTime) return;

            stuckTime = 0f;
            Transform animatedRoot = puppet.targetRoot != null ? puppet.targetRoot : modelTransform;
            if (animatedRoot == null) return;
            bool wall = Physics.Linecast(pelvis.target.position, pelvis.rigidbody.position, out RaycastHit hit, lostRagdollBlockers, QueryTriggerInteraction.Ignore);
            Debug.LogWarning($"[Ragdoll] {name}: standing ragdoll stuck {gap:F2} m from its animated body{(wall ? $" (behind '{hit.collider.name}')" : "")} — snapped back onto it", this);
            puppet.Teleport(animatedRoot.position, animatedRoot.rotation, true);
        }

        private void Update()
        {
            CheckStuckRagdoll();
            if (isRagdollCameraActive) CheckLostRagdoll();
            else { downTime = 0f; hasLastPelvis = false; }

            if (desyncRecoveryDelay <= 0f || behaviourPuppet == null || isRagdollCameraActive || recoverRoutine != null)
            {
                desyncTime = 0f;
                return;
            }
            if (behaviourPuppet.state == BehaviourPuppet.State.Puppet) { desyncTime = 0f; return; }

            desyncTime += Time.deltaTime;
            if (desyncTime < desyncRecoveryDelay) return;
            desyncTime = 0f;
            Debug.LogWarning($"[Ragdoll] {name}: ragdoll is {behaviourPuppet.state} while the player has control — back to ragdoll mode so it can get up", this);
            OnKnockedDown();
        }

        // LateUpdate: other scripts that touch the camera are disabled during
        // the ragdoll, so this is its sole writer while detached.
        private void LateUpdate()
        {
            UpdateFallCamera();
            TraceCameraReturn();
        }

        private void UpdateFallCamera()
        {
            if (!cameraDetached || fpsVirtualCamera == null) return;
            Transform cam = fpsVirtualCamera.transform;

            // Pulled back only while actually down; the return starts with the
            // get-up, so the view is in the eyes by the time control is back.
            bool down = isRagdollCameraActive && (behaviourPuppet == null || behaviourPuppet.state == BehaviourPuppet.State.Unpinned);
            float target = down ? 1f : 0f;
            float duration = target > cameraPull ? cameraPullBackTime : cameraReturnTime;
            cameraPull = duration > 0f ? Mathf.MoveTowards(cameraPull, target, Time.deltaTime / duration) : target;

            // Back in the eyes after recovery: the camera's authored pose under
            // CameraPivot takes over again, exactly where it was being drawn.
            if (!isRagdollCameraActive && cameraPull <= 0f)
            {
                cam.localPosition = cameraRestLocalPosition;
                cam.localRotation = cameraRestLocalRotation;
                cameraDetached = false;
                return;
            }

            // Eyes: while down, where they'll be once the root is moved onto
            // the body (same math as OnRecovered); after recovery, the rest pose.
            Vector3 eyePosition;
            Quaternion eyeRotation;
            if (isRagdollCameraActive)
            {
                PredictedRoot(out Vector3 rootPosition, out Quaternion rootRotation);
                eyePosition = rootPosition + rootRotation * eyePositionInRoot;
                eyeRotation = rootRotation * eyeRotationInRoot;
            }
            else
            {
                Transform parent = cam.parent;
                eyePosition = parent != null ? parent.TransformPoint(cameraRestLocalPosition) : cameraRestLocalPosition;
                eyeRotation = (parent != null ? parent.rotation : Quaternion.identity) * cameraRestLocalRotation;
            }

            // Fall view: behind/above the pelvis (muscles[0], always the
            // hip), looking where the player was looking.
            Vector3 fallPosition = eyePosition;
            Quaternion fallRotation = eyeRotation;
            PuppetMaster puppet = behaviourPuppet != null ? behaviourPuppet.puppetMaster : null;
            if (puppet != null && puppet.muscles.Length > 0 && puppet.muscles[0].rigidbody != null)
            {
                Vector3 pelvis = puppet.muscles[0].rigidbody.position;
                fallPosition = pelvis + Quaternion.Euler(0f, ragdollCameraYaw, 0f) * new Vector3(0f, ragdollCameraHeight, -ragdollCameraDistance);
                fallRotation = Quaternion.LookRotation(pelvis + Vector3.up * 0.3f - fallPosition);
            }

            float w = Mathf.SmoothStep(0f, 1f, cameraPull);
            cam.SetPositionAndRotation(Vector3.Lerp(eyePosition, fallPosition, w), Quaternion.Slerp(eyeRotation, fallRotation, w));
        }

        // Where OnRecovered will put the root: on the animated body, facing
        // the view's yaw.
        private void PredictedRoot(out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(0f, ragdollCameraYaw, 0f);
            position = modelTransform != null && modelFollow != null
                ? modelTransform.position - rotation * modelFollow.LocalRestPosition
                : transform.position;
        }
    }
}
