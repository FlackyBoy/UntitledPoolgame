using System.Collections;
using System.Collections.Generic;
using RootMotion.Dynamics;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Core;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Player
{
    // The shrunk body of a player (Pocket Size): the full-size body is swapped
    // for a new instance of a prefab built at the small size (the Mini variant
    // of the player), keeping the player's index and devices, and swapped back
    // by Restore.
    //
    // A new instance rather than rescaling the live body: PuppetMaster takes
    // its joints from the scale it starts at. Rescaled at runtime, the limbs
    // ended up tens of centimetres apart on a 14 cm body and spun all over
    // (logs of 30/10), whatever was tried. Started at the small size, the
    // ragdoll holds and stays on.
    //
    // What doesn't follow the prefab's scale is set here: the controller's
    // skin and step (world units), the cameras' near clip, the movement
    // (LocalFpsPlayerController.SizeScale) and the ragdoll's distances. The
    // controller collides with the layers it's given (the balls), and a blow
    // knocks the ragdoll down (KnockDown).
    [DisallowMultipleComponent]
    public class LocalPlayerShrink : MonoBehaviour
    {
        private static readonly List<LocalPlayerShrink> shrunk = new List<LocalPlayerShrink>();

        private CharacterController controller;
        private LocalFpsPlayerController fps;
        private const float FallMappingSpeed = 10f;

        private BehaviourPuppet behaviourPuppet;
        private LocalPlayerRagdollController ragdoll;
        private PlayerInput input;
        private float fallDamper;
        // What the body bumps into beyond its own layers (Configure), and
        // whether the ragdoll does too at the moment (LateUpdate).
        private int blockedLayers;
        private bool ragdollBlocks;
        // The joints' torque cap while down (Throw), once given.
        private const float FallForceMlp = 100f;
        private bool forceRaised;

        // The body to come back to, and where it stood.
        private GameObject fullPrefab;
        private Vector3 homePosition;
        private Quaternion homeRotation;

        public PuppetMaster Puppet => behaviourPuppet != null ? behaviourPuppet.puppetMaster : null;

        // Knocked down, until control is handed back.
        public bool IsDown => ragdoll != null && ragdoll.IsRagdollCameraActive;

        // Where the body is: while it's down the root stays at the fall spot,
        // and the ragdoll may have been thrown far from it.
        public Vector3 BodyPosition
        {
            get
            {
                PuppetMaster puppet = behaviourPuppet != null ? behaviourPuppet.puppetMaster : null;
                bool hasPelvis = puppet != null && puppet.muscles.Length > 0 && puppet.muscles[0].rigidbody != null;
                return IsDown && hasPelvis ? puppet.muscles[0].rigidbody.position : transform.position;
            }
        }

        // The player's body swapped for an instance of tinyPrefab, moved to
        // feet. collideWith: layers the shrunk body bumps into on top of its
        // own (the balls, which normal bodies pass through: PhysicsLayerSetup).
        // nearClip: the cameras' near plane, in metres. jumpHeight: at normal
        // size, for a player without the R&D locomotion. fallStiffness: the
        // muscles' strength while down (Configure).
        public static LocalPlayerShrink Shrink(PlayerInput player, GameObject tinyPrefab, GameObject fullPrefab,
            Vector3 feet, Quaternion rotation, LayerMask collideWith, float nearClip, float jumpHeight, float fallStiffness, float fallGravity, float fallDamper)
        {
            Vector3 home = player.transform.position;
            Quaternion homeRotation = player.transform.rotation;
            // A cue held would go with the body it's parented to.
            if (player.TryGetComponent(out LocalPlayerHandController oldHands)) oldHands.ForceDrop();

            PlayerInput tiny = SwapBody(player, tinyPrefab);
            LocalPlayerShrink shrink = tiny.gameObject.AddComponent<LocalPlayerShrink>();
            shrink.fullPrefab = fullPrefab;
            shrink.homePosition = home;
            shrink.homeRotation = homeRotation;
            shrink.Configure(tinyPrefab.transform.localScale.y / fullPrefab.transform.localScale.y, collideWith, nearClip, jumpHeight, fallStiffness);
            if (shrink.ragdoll != null) shrink.ragdoll.FallGravityOverride = fallGravity;
            shrink.fallDamper = fallDamper;
            shrink.Teleport(feet, rotation);
            shrunk.Add(shrink);
            return shrink;
        }

        // Everyone back to full size where they stood (a new match, or the
        // mode ending).
        public static void RestoreAll()
        {
            for (int i = shrunk.Count - 1; i >= 0; i--)
                if (shrunk[i] != null) shrunk[i].Restore();
            shrunk.Clear();
        }

        // A new body in place of this one, for the same player: same index,
        // same devices. SetActive first: PlayerInput gives them up on disable,
        // and Destroy alone would only get there at the end of the frame.
        private static PlayerInput SwapBody(PlayerInput body, GameObject prefab)
        {
            int index = body.playerIndex;
            string scheme = body.currentControlScheme;
            InputDevice[] devices = body.devices.ToArray();
            // No device (the keyboard is with the other player, until Tab).
            // Instantiated with none it would have no InputUser, and could
            // never get a device again (SplitScreenCheatSpawner): the keyboard
            // for the time of the swap.
            bool noDevice = devices.Length == 0;
            if (noDevice)
            {
                scheme = PlayerDeviceRouter.KeyboardScheme;
                devices = PlayerDeviceRouter.KeyboardAndMouse();
            }

            body.gameObject.SetActive(false);
            Destroy(body.gameObject);
            PlayerInput next = PlayerInput.Instantiate(prefab, index, scheme, -1, devices);
            if (noDevice && next.user.valid) next.user.UnpairDevices();
            return next;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            fps = GetComponent<LocalFpsPlayerController>();
            behaviourPuppet = GetComponentInChildren<BehaviourPuppet>(true);
            ragdoll = GetComponent<LocalPlayerRagdollController>();
            input = GetComponent<PlayerInput>();
        }

        private void OnDestroy() => shrunk.Remove(this);

        // While down the controller is off, and it's the only thing that
        // stops a ball: the ragdoll's layer ignores them (PhysicsLayerSetup),
        // so a ball went straight through the body, and the touch itself
        // turns the controller off just before contact. Only while down:
        // standing, the capsule does it, and a pinned ragdoll shouldn't be
        // shoved around. LateUpdate: before the next physics step, however
        // the body went down.
        private void LateUpdate()
        {
            UpdateRagdollBlocks();
            // Getting up is the muscles' work: the cap back as they take over.
            if (!forceRaised || behaviourPuppet == null || behaviourPuppet.state == BehaviourPuppet.State.Unpinned) return;
            forceRaised = false;
            foreach (Muscle muscle in behaviourPuppet.puppetMaster.muscles)
                muscle.state.maxForceMlp = 1f;
        }

        private void UpdateRagdollBlocks()
        {
            bool down = IsDown;
            if (down == ragdollBlocks || behaviourPuppet == null) return;
            ragdollBlocks = down;
            foreach (Muscle muscle in behaviourPuppet.puppetMaster.muscles)
            {
                if (muscle.rigidbody == null) continue;
                muscle.rigidbody.includeLayers = down
                    ? muscle.rigidbody.includeLayers | blockedLayers
                    : muscle.rigidbody.includeLayers & ~blockedLayers;
            }
        }

        // size: this body against the full-size one.
        private void Configure(float size, LayerMask collideWith, float nearClip, float jumpHeight, float fallStiffness)
        {
            blockedLayers = collideWith;
            // As limp as a knocked-out player: its falls don't go through the
            // hit gauge, which sets that for a K.O.
            if (behaviourPuppet != null)
            {
                behaviourPuppet.unpinnedMuscleWeightMlp = fallStiffness;
                // The model joins the ragdoll in a second by default, and this
                // body has landed 0.3 s in: the model hung 11 cm from it, in
                // the air (log of 03/11).
                foreach (BehaviourBase behaviour in behaviourPuppet.puppetMaster.behaviours)
                    if (behaviour is BehaviourFall fall) fall.blendMappingSpeed = FallMappingSpeed;
            }
            // Nothing picked up at this size: the reach is in metres, the cues
            // lying around the table would still be within it. The powers have
            // their own component (LocalPoolPowerController).
            if (TryGetComponent(out LocalPlayerHandController hands)) hands.enabled = false;
            if (controller != null)
            {
                controller.skinWidth *= size;
                controller.stepOffset *= size;
                controller.minMoveDistance = 0f;
                controller.includeLayers |= collideWith;
            }
            foreach (CinemachineCamera cam in GetComponentsInChildren<CinemachineCamera>(true))
            {
                LensSettings lens = cam.Lens;
                lens.NearClipPlane = nearClip;
                cam.Lens = lens;
            }
            if (fps != null)
            {
                fps.SizeScale = size;
                fps.JumpHeight = jumpHeight;
            }
            if (ragdoll != null) ragdoll.SizeScale = size;
        }

        // Full size again, back where they stood before shrinking.
        public void Restore()
        {
            shrunk.Remove(this);
            PlayerInput full = SwapBody(input, fullPrefab);
            // The join moved it to the scene's spawn point.
            MoveBody(full.gameObject, homePosition, homeRotation);
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            if (fps != null) fps.SpeedMultiplier = multiplier;
        }

        // Frozen (Shooter): no input of any kind, the pose held. Through the
        // input rather than each component: the view, the shots, the kick and
        // the powers all stop together.
        public void SetFrozen(bool frozen)
        {
            if (TryGetComponent(out PlayerInput own))
            {
                if (frozen && own.inputIsActive) own.DeactivateInput();
                else if (!frozen && !own.inputIsActive) own.ActivateInput();
            }
            if (TryGetComponent(out LocalCharacterAnimationController anim) && anim.Animator != null)
                anim.Animator.speed = frozen ? 0f : 1f;
        }

        public void Teleport(Vector3 feet, Quaternion rotation) => MoveBody(gameObject, feet, rotation);

        // The CharacterController fights a jump this big: off for the move.
        // An active puppet follows at once (PuppetMaster otherwise drags its
        // ragdoll across the room to the new spot).
        private static void MoveBody(GameObject body, Vector3 feet, Quaternion rotation)
        {
            CharacterController bodyController = body.GetComponent<CharacterController>();
            bool wasEnabled = bodyController != null && bodyController.enabled;
            if (bodyController != null) bodyController.enabled = false;
            body.transform.SetPositionAndRotation(feet, rotation);
            if (bodyController != null) bodyController.enabled = wasEnabled;
            PuppetMaster bodyPuppet = body.GetComponentInChildren<PuppetMaster>(true);
            if (bodyPuppet != null && bodyPuppet.isActive) bodyPuppet.Teleport(feet, rotation, true);
        }

        // A blow: the ragdoll falls, thrown at velocity (m/s). It gets up by
        // itself (BehaviourPuppet, Get Up Delay on the prefab), and
        // LocalPlayerRagdollController hands control back.
        public void KnockDown(Vector3 velocity, bool trace, Rigidbody ball = null)
        {
            if (behaviourPuppet == null) return;
            behaviourPuppet.SetState(BehaviourPuppet.State.Unpinned);
            StartCoroutine(Throw(velocity));
            if (trace)
            {
                StartCoroutine(TraceFall());
                StartCoroutine(TraceFlight());
                if (ball != null) StartCoroutine(TraceBall(ball));
            }
        }

        // Diagnostic of the ball that hit, every 0.05 s for 0.5 s: stopped by
        // the body, its direction turns back (negative) instead of staying at
        // 1 while it runs past the pelvis.
        private IEnumerator TraceBall(Rigidbody ball)
        {
            Rigidbody pelvis = behaviourPuppet.puppetMaster.muscles[0].rigidbody;
            Vector3 heading = ball.linearVelocity.normalized;
            var line = new System.Text.StringBuilder(
                "[PocketSize] Bille : t | distance au bassin (cm) | vitesse (m/s) | sens par rapport au choc (1 = continue, -1 = repart)");
            float begin = Time.time;
            for (int sample = 0; sample <= 10 && ball != null && pelvis != null; sample++)
            {
                Vector3 v = ball.linearVelocity;
                line.Append($"\n  {Time.time - begin:0.00} | {Vector3.Distance(ball.position, pelvis.position) * 100f:0.0} | {v.magnitude:0.00} | {Vector3.Dot(v.normalized, heading):0.00}");
                float next = begin + (sample + 1) * 0.05f;
                while (Time.time < next) yield return null;
            }
            Debug.Log(line.ToString(), this);
        }

        // Diagnostic of what's shown during the fall, every 0.1 s for 2 s, read
        // at the end of the frame (the model is mapped by then): whether the
        // model shows the ragdoll at all, whether the ragdoll holds the
        // animation's pose, and whether the pose still changes.
        private IEnumerator TraceFlight()
        {
            PuppetMaster puppet = behaviourPuppet.puppetMaster;
            if (puppet == null || puppet.muscles.Length == 0) yield break;
            int count = puppet.muscles.Length;
            var lastShape = new float[count];
            Vector3 start = puppet.muscles[0].transform.position;
            var line = new System.Text.StringBuilder(
                "[PocketSize] Vol : t | état | hauteur du bassin (cm depuis le choc) | vitesse verticale (m/s) | mapping | muscle | pin | modèle-ragdoll (cm) | ragdoll-animation (cm) | changement de pose (cm) | FallBlend");
            float begin = Time.time;
            for (int sample = 0; sample <= 20; sample++)
            {
                yield return new WaitForEndOfFrame();
                if (puppet == null) yield break;
                Muscle pelvis = puppet.muscles[0];
                float modelGap = 0f, animationGap = 0f, poseChange = 0f;
                for (int i = 0; i < count; i++)
                {
                    Muscle muscle = puppet.muscles[i];
                    float shape = Vector3.Distance(muscle.transform.position, pelvis.transform.position);
                    float animated = Vector3.Distance(muscle.targetAnimatedPosition, pelvis.targetAnimatedPosition);
                    modelGap += Vector3.Distance(muscle.target.position, muscle.transform.position);
                    animationGap += Mathf.Abs(shape - animated);
                    if (sample > 0) poseChange += Mathf.Abs(shape - lastShape[i]);
                    lastShape[i] = shape;
                }
                Animator animator = puppet.targetAnimator;
                float fallBlend = animator != null && animator.isActiveAndEnabled ? animator.GetFloat("FallBlend") : -1f;
                line.Append($"\n  {Time.time - begin:0.00} | {behaviourPuppet.state} | {(pelvis.transform.position.y - start.y) * 100f:0.0} | {pelvis.rigidbody.linearVelocity.y:0.00} | {pelvis.state.mappingWeightMlp:0.00} | {pelvis.state.muscleWeightMlp:0.00} | {pelvis.state.pinWeightMlp:0.00} | {modelGap / count * 100f:0.00} | {animationGap / count * 100f:0.00} | {poseChange / count * 100f:0.00} | {fallBlend:0.00}");
                float next = begin + (sample + 1) * 0.1f;
                while (Time.time < next) yield return null;
            }
            Debug.Log(line.ToString(), this);
        }

        // Diagnostic of the pose on the ground, once settled: each limb's
        // distance from the pelvis against the animation's (the muscles
        // following the clip if they match), how far its joint has come
        // apart, and the strength it's really given.
        private IEnumerator TraceFall()
        {
            yield return new WaitForSeconds(1.5f);
            PuppetMaster puppet = behaviourPuppet.puppetMaster;
            if (puppet == null || puppet.muscles.Length == 0) yield break;
            Vector3 pelvis = puppet.muscles[0].transform.position;
            Vector3 targetPelvis = puppet.muscles[0].target.position;
            var line = new System.Text.StringBuilder(
                $"[PocketSize] Au sol : {behaviourPuppet.state}, corps de {controller.height * transform.lossyScale.y:0.000} m, écarts au bassin en cm (animation entre parenthèses), écart de l'articulation, ressort :");
            foreach (Muscle muscle in puppet.muscles)
            {
                ConfigurableJoint joint = muscle.joint;
                float gap = joint != null && joint.connectedBody != null
                    ? Vector3.Distance(joint.transform.TransformPoint(joint.anchor), joint.connectedBody.transform.TransformPoint(joint.connectedAnchor))
                    : 0f;
                line.Append($"\n  {muscle.transform.name} : {Vector3.Distance(muscle.transform.position, pelvis) * 100f:0.0} ({Vector3.Distance(muscle.target.position, targetPelvis) * 100f:0.0}), {gap * 100f:0.00}, {(joint != null ? joint.slerpDrive.positionSpring : 0f):0.#}");
            }
            Debug.Log(line.ToString(), this);
        }

        // One physics step after the unpin, as LocalHitGauge does: the balls
        // and the ragdoll ignore each other (PhysicsLayerSetup), so the blow
        // has to be given by hand, and BehaviourPuppet clamps the velocities
        // as it unpins.
        private IEnumerator Throw(Vector3 velocity)
        {
            yield return new WaitForFixedUpdate();
            forceRaised = true;
            foreach (Muscle muscle in behaviourPuppet.puppetMaster.muscles)
            {
                if (muscle.rigidbody == null || muscle.state.isDisconnected) continue;
                // Stiffness without a pose: a spring holds the fall animation's
                // pose (log of 03/11: within 0.3 cm of it, frozen once down),
                // a damper only resists moving. Per kilo: a uniform one barely
                // slowed the spine (100 times a hand's inertia). And the cap
                // raised: PuppetMaster limits a joint's torque to its spring or
                // damper value, a tenth of what gravity puts on the hips here,
                // so the joints folded freely whatever the damper. Both cleared
                // on getting up.
                muscle.state.muscleDamperAdd = fallDamper * muscle.rigidbody.mass;
                muscle.state.maxForceMlp = FallForceMlp;
                // The legs lag and the top whips: at one speed, nothing would
                // bend and the body would fly in the pose it was hit in.
                bool legs = muscle.props.group == Muscle.Group.Leg || muscle.props.group == Muscle.Group.Foot;
                bool hips = muscle.props.group == Muscle.Group.Hips;
                muscle.rigidbody.AddForce(velocity * (legs ? 0.8f : hips ? 1f : 1.1f), ForceMode.VelocityChange);
            }
        }

        // The controller's capsule in world space, for hit tests.
        public void GetCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            float s = transform.lossyScale.y;
            Vector3 center = transform.TransformPoint(controller.center);
            radius = controller.radius * s;
            float half = Mathf.Max(0f, controller.height * s * 0.5f - radius);
            bottom = center - Vector3.up * half;
            top = center + Vector3.up * half;
        }
    }
}
