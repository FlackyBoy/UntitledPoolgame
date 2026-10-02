using Unity.Cinemachine;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using UnityEngine;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Player
{
    // Ragdoll on impact from a thrown Grabbable, then a get-up animation
    // blend back to normal control — offline/split-screen only for now,
    // same exception already taken for LocalPoolPowerEffectReceiver etc.
    // (no stable network player identity / networked ragdoll physics yet).
    //
    // Built on RootMotion's PuppetMaster/BehaviourPuppet (replaces an
    // earlier attempt on top of Feel's MMRagdoller — see TODO.md for why).
    // BehaviourPuppet already owns the whole Puppet/Unpinned/GetUp state
    // machine, the impact detection that knocks a muscle loose
    // (Collision Layers/Threshold/Resistance on the muscles' own
    // MuscleCollisionBroadcasters — see the demo scenes under
    // PuppetMaster/_DEMOS, e.g. Melee.unity), AND the get-up clip trigger
    // itself (onGetUpProne/onGetUpSupine, wired in the Inspector straight
    // to Animator.Play — nothing routes through this script). This class
    // wires up onLoseBalance/onRegainBalance (also Inspector-facing
    // PuppetEvents, same idiom) to the game-specific stuff PuppetMaster has
    // no way to know about: this project's other player controllers, the
    // held object, the split-screen cameras, and the CharacterController
    // root that lives outside of anything PuppetMaster touches — see
    // OnRecovered for how that root gets back in sync.
    //
    // Architecture note (RootMotion's own demo confirmed, by actually
    // reading CharacterAnimationBase.cs, not guessed): PuppetMaster's
    // Animated Target (modelTransform below) is a CHILD of this script's
    // own root, exactly like ours — the demo does NOT put everything on
    // one object (an earlier revision of this file assumed it did after
    // misreading a callback, and briefly restructured this project's
    // player prefab to match — that made things worse and was reverted).
    // The demo's real fix is LocalCharacterModelFollow (see that script):
    // the child is FORCED to continuously follow this root at a fixed
    // authored offset, every frame, EXCEPT while PuppetMaster owns it
    // during the ragdoll — this script disables that follow on knockdown
    // and, on recovery, moves the ROOT to match wherever PuppetMaster's own
    // native repositioning already (correctly) left the child, before
    // re-enabling the follow.
    [RequireComponent(typeof(CharacterController))]
    public class LocalPlayerRagdollController : MonoBehaviour
    {
        [Header("Assigned by hand")]
        // The ragdoll's own state machine (Puppet/Unpinned/GetUp) — owns
        // the actual knockdown/get-up physics and animation, INCLUDING
        // repositioning modelTransform natively while getting up. This
        // script reacts to it (OnKnockedDown/OnRegainBalance, wired in the
        // Inspector to its own onLoseBalance/onRegainBalance PuppetEvents)
        // rather than driving it directly.
        [SerializeField] private BehaviourPuppet behaviourPuppet;
        // poolPlayer's Animator — used only to zero out
        // Speed/MoveX/MoveY/Forward/Right at the moment of knockdown (see
        // OnKnockedDown), so LocalCharacterAnimationController doesn't
        // leave a stale value frozen in while disabled (see the comment on
        // that call site for the loop bug that caused).
        [SerializeField] private Animator animator;
        // How long BehaviourPuppet's onGetUpProne/onGetUpSupine clip
        // actually takes to play, roughly — onRegainBalance (wired below,
        // see OnRegainBalance) already fires based on BehaviourPuppet's OWN
        // internal timing (blendToAnimationTime/minGetUpDuration),
        // independent of and usually shorter than that clip, so control
        // isn't handed back until this long AFTER that event fires.
        //
        // Keep this SHORT (close to the real clip length, not much more) —
        // too short and OnRecovered samples modelTransform mid-clip (e.g.
        // still crouched); too long and the opposite happens: PuppetMaster
        // only holds a correct standing pose natively for a while after
        // getting up, not indefinitely, and logged testing showed the
        // ragdoll's own muscles had already sagged back down on their own
        // well before a several-second delay elapsed, well past the point
        // where anything here could still catch it looking right.
        [SerializeField] private float getUpRecoveryDelay = 1f;
        // PuppetMaster's Animated Target (poolPlayer, a child of this
        // GameObject) — must also carry LocalCharacterModelFollow (fetched
        // automatically in Awake). See the class comment and that script
        // for the full reasoning.
        [SerializeField] private Transform modelTransform;

        // Two separate CinemachineCameras (not the real output Camera —
        // that one just needs a CinemachineBrain and is never touched here)
        // instead of one repositioned back and forth: the FPS one is never
        // touched by this script at all (it just sits under CameraPivot,
        // following the normal FPS look/pitch on its own), so there's
        // nothing to save/restore and nothing that can ever drift. Blending
        // between the two on knockdown/recovery is handled by Cinemachine
        // itself (whichever has the higher Priority is "live", blended
        // automatically per CinemachineBrain's default blend) — we only
        // ever flip Priority, never touch a Camera's own enabled state.
        [Header("Cameras (Cinemachine — Priority swapped, brain blends)")]
        [SerializeField] private CinemachineCamera fpsVirtualCamera;
        [SerializeField] private CinemachineCamera ragdollVirtualCamera;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        [Header("Ragdoll camera (3rd person, behind/above the pelvis)")]
        [SerializeField] private float ragdollCameraDistance = 2.5f;
        [SerializeField] private float ragdollCameraHeight = 1.5f;

        // FBBIK, Look At IK, etc. — solving toward their targets
        // while PuppetMaster physically owns the bones would just fight the
        // ragdoll, so these are disabled for the same window as the other
        // controllers above and re-enabled once control is handed back.
        [Header("FinalIK (disabled during ragdoll)")]
        [SerializeField] private IK[] ikComponents;

        private CharacterController characterController;
        private LocalFpsPlayerController fpsController;
        private LocalPoolAimController aimController;
        private LocalCharacterAnimationController animController;
        private LocalPlayerHandController handController;
        private LocalCharacterModelFollow modelFollow;

        private bool isRagdollCameraActive;

        // Read by LocalPoolPowerEffectReceiver (and anything else that
        // touches the camera) to no-op while this owns it — same idiom as
        // LocalPoolAimController.IsAiming/IsPlacementViewActive.
        public bool IsRagdollCameraActive => isRagdollCameraActive;

        // Called by RagdollHitRelay (one per ragdoll bone) on a qualifying
        // hit. PuppetMaster's own native knockdown detection (Collision
        // Layers/Threshold/Resistance, plus a continuous check of how far
        // each muscle has physically drifted from its animated target pose)
        // already handles the actual physics reaction on its own — but
        // BehaviourPuppet gates that check behind an additional internal
        // flag (hasCollidedSinceGetUp), only ever set true automatically
        // when puppetMaster.pinWeight happens to dip below 1, which turned
        // out too unreliable in testing (fell once, then never again after
        // getting up). BehaviourPuppet.SetHasCollided's own doc comment
        // spells out exactly this: "Puppets do not get unpinned if they
        // haven't collided with anything on the Collision Layers since the
        // last time they got up" — it's meant to be driven by game code,
        // not just left to the automatic path.
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

        // Wired in the Inspector to BehaviourPuppet's own onLoseBalance/
        // onRegainBalance PuppetEvents (Puppet Behaviours > Events, same
        // idiom as onGetUpProne/onGetUpSupine -> Animator.Play) rather than
        // polled here — onLoseBalance already fires "doesn't matter from
        // which state", covering both a native collision-driven Unpin and
        // any other Behaviour that knocks the puppet down, without this
        // script needing to know how it happened or guard against a
        // not-yet-reliable state on the very first frame.
        public void OnKnockedDown()
        {
            // Knocked down again while still getting up (hit again, or
            // bumping into something during the get-up): the recovery
            // scheduled by the previous OnRegainBalance must not run —
            // handing control back then left the ragdoll lying on the floor
            // while the character stood up, detached from it.
            if (recoverRoutine != null)
            {
                StopCoroutine(recoverRoutine);
                recoverRoutine = null;
                Debug.Log($"[Ragdoll] {name}: knocked down again while getting up — pending recovery cancelled", this);
            }

            Debug.Log($"[Ragdoll] {name}: knocked down (ragdoll mode on)", this);

            if (handController != null && handController.HeldObject != null)
                handController.ForceDrop();

            characterController.enabled = false;
            if (fpsController != null) fpsController.enabled = false;
            if (aimController != null) aimController.enabled = false;
            if (animController != null) animController.enabled = false;
            // Lets PuppetMaster freely reposition modelTransform natively
            // while the ragdoll is down/getting up, instead of
            // LocalCharacterModelFollow fighting it back toward this
            // root's own (currently stale) position every frame.
            if (modelFollow != null) modelFollow.enabled = false;
            if (ikComponents != null)
                foreach (IK ik in ikComponents)
                    if (ik != null) ik.enabled = false;

            // LocalCharacterAnimationController stops updating its
            // locomotion float parameters the instant it's disabled above —
            // whatever value they last had (e.g. still mid-sprint at the
            // moment of impact) otherwise sits there frozen for the whole
            // ragdoll. The exit transition out of the get-up state goes
            // straight to Idle with no condition, but Idle's OWN "Speed >
            // 0.1"-equivalent transition to Locomotion would then fire on
            // that same stale leftover value instantly — passing through
            // Idle for a single frame (or less) and straight into
            // Locomotion, looping there forever since nothing updates it
            // back down while still disabled. Checked against
            // animator.parameters first — different Animator Controllers
            // used across this project's characters name these differently
            // (Speed/MoveX/MoveY vs. Forward/Right), and Animator.SetFloat
            // logs a warning for a name that doesn't exist on the assigned
            // controller.
            if (animator != null)
            {
                foreach (string floatParameterName in new[] { "Speed", "MoveX", "MoveY", "Forward", "Right" })
                {
                    if (HasFloatParameter(animator, floatParameterName))
                        animator.SetFloat(floatParameterName, 0f);
                }
            }

            isRagdollCameraActive = true;
            if (ragdollVirtualCamera != null) ragdollVirtualCamera.Priority = activePriority;
            if (fpsVirtualCamera != null) fpsVirtualCamera.Priority = inactivePriority;
        }

        // Wired in the Inspector to BehaviourPuppet's onRegainBalance
        // PuppetEvent ("Called when the character has fully recovered and
        // switched to the Puppet state"). Fires on BehaviourPuppet's own
        // internal timing, shorter than the actual get-up clip (see the
        // Get Up Recovery Delay field comment) — waits out the rest of the
        // delay before actually handing control back.
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
            // PuppetMaster has already (correctly) repositioned
            // modelTransform natively while getting up — read wherever
            // that landed it (trusted, this is the one value nothing here
            // overrides), then solve for the ROOT transform that would
            // produce that exact same world pose for modelTransform if its
            // local offset were reset back to its authored rest value
            // (LocalCharacterModelFollow.LocalRestPosition/Rotation,
            // cached once in its own Awake). Moving the root there absorbs
            // the same motion for the CharacterController, without
            // touching any ragdoll muscle directly.
            // Got up on the other side of a wall from where it fell (thrown
            // through it): don't follow it there. The puppet is brought back
            // to the fall spot instead (teleported onto its animated pose,
            // the only muscle-touching exception in this script) and the root
            // stays put — the model follow snaps the animated body back onto
            // it below.
            RaycastHit wall = default;
            bool behindWall = modelTransform != null && Physics.Linecast(
                transform.position + Vector3.up, modelTransform.position + Vector3.up,
                out wall, lostRagdollBlockers, QueryTriggerInteraction.Ignore);
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

                // Yaw only for the root — this FPS character's root must
                // stay level (movement/look math assumes no pitch/roll),
                // even if the model's own settled rotation has a slight
                // residual tilt right after getting up.
                Quaternion fullRootRotation = currentModelWorldRotation * Quaternion.Inverse(modelFollow.LocalRestRotation);
                Quaternion newRootRotation = Quaternion.Euler(0f, fullRootRotation.eulerAngles.y, 0f);
                Vector3 newRootPosition = currentModelWorldPosition - newRootRotation * modelFollow.LocalRestPosition;

                characterController.enabled = false;
                transform.SetPositionAndRotation(newRootPosition, newRootRotation);

                // Snaps modelTransform back to its rest local offset (now
                // world-equivalent to where it already was) and resets
                // LocalCharacterModelFollow's own smoothing cache, so
                // re-enabling it below doesn't lerp in from a stale
                // pre-ragdoll position.
                modelFollow.ResyncNow();
                modelFollow.enabled = true;

                // Deliberately NOT touching the ragdoll's muscle
                // Rigidbodies at all — every variant tried
                // (PuppetMaster.Teleport with moveToTarget true or false)
                // made things worse in a different way each time (see
                // RAGDOLL_DEBUGGING_LOG.md). The actual root cause turned
                // out to be Get Up Recovery Delay being far too long: logs
                // showed the muscles were ALREADY sagging back down on
                // their own, well before this method ever ran — PuppetMaster
                // holds the correct standing pose natively for a while
                // after getting up, but not indefinitely, and we were
                // waiting long past that window before doing anything. Keep
                // Get Up Recovery Delay short (tuned to just past the real
                // get-up clip length) instead of trying to force a
                // late correction.
            }

            Debug.Log($"[Ragdoll] {name}: recovered — control handed back{(behindWall ? " (at the fall spot)" : "")}", this);
            characterController.enabled = true;
            if (fpsController != null) fpsController.enabled = true;
            if (aimController != null) aimController.enabled = true;
            if (animController != null) animController.enabled = true;
            if (ikComponents != null)
                foreach (IK ik in ikComponents)
                    if (ik != null) ik.enabled = true;

            isRagdollCameraActive = false;
            if (fpsVirtualCamera != null) fpsVirtualCamera.Priority = activePriority;
            if (ragdollVirtualCamera != null) ragdollVirtualCamera.Priority = inactivePriority;
        }

        // LateUpdate — same reasoning as LocalPoolPowerEffectReceiver: runs
        // after whatever else might have touched the camera this frame.
        // Everything else (LocalFpsPlayerController, LocalPoolAimController) is
        // already disabled for the whole ragdoll duration, so this is the
        // sole writer of the ragdoll camera's transform while active.
        // Safety net: the player has control (not in ragdoll mode) while the
        // puppet isn't standing — its ragdoll lying on the floor, detached
        // from a character that stood back up. Whatever led there, going
        // back through OnKnockedDown lets BehaviourPuppet get up normally
        // (the model follow no longer pins the animated body to the root),
        // and OnRegainBalance then hands control back as usual.
        [Tooltip("Délai (secondes) au bout duquel un ragdoll resté au sol alors que le joueur a le contrôle est remis en mode chute pour qu'il se relève normalement. 0 = désactivé.")]
        [SerializeField] private float desyncRecoveryDelay = 0.5f;
        private float desyncTime;

        // A ragdoll thrown hard can end up where it can never get up: through
        // a wall (seen with the Spartan kick) or under the floor. While down:
        // its pelvis crossing level geometry between two frames (it passed
        // through), falling well below the spot it was knocked down at (the
        // root, which doesn't move during the ragdoll), or staying down too
        // long gets it teleported back to that spot (PuppetMaster.Teleport,
        // onto its animated pose) so it can get up in the room.
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

            // Continuous collision on every muscle: a ragdoll thrown at a few
            // metres per second (Spartan kick, heavy hits) otherwise passes
            // through thin walls between two physics steps.
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

        // A standing (pinned) ragdoll must sit on its animated body. Seen:
        // the ragdoll thrown through a wall, getting up there, and then
        // pinned toward the animated body standing on this side — held back
        // by the wall, for good. Too far from its animated pelvis for too
        // long, it's snapped back onto the animated pose (PuppetMaster.
        // Teleport at the animated root, moveToTarget).
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

        private void LateUpdate()
        {
            if (!isRagdollCameraActive || ragdollVirtualCamera == null || behaviourPuppet == null) return;
            if (behaviourPuppet.puppetMaster == null || behaviourPuppet.puppetMaster.muscles.Length == 0) return;

            // muscles[0] is always the hip/root muscle.
            Vector3 pelvis = behaviourPuppet.puppetMaster.muscles[0].rigidbody.position;
            Vector3 lookTarget = pelvis + Vector3.up * 0.3f;

            // No attempt to track a stable facing direction — a tumbling
            // ragdoll doesn't have one worth following. A fixed world-space
            // back-and-up offset from the pelvis keeps the shot readable.
            Vector3 offset = new Vector3(0f, ragdollCameraHeight, -ragdollCameraDistance);
            Transform ragdollCamTransform = ragdollVirtualCamera.transform;
            ragdollCamTransform.position = pelvis + offset;
            ragdollCamTransform.LookAt(lookTarget);
        }
    }
}
