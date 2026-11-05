using System.Collections;
using RootMotion;
using RootMotion.FinalIK;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // The cue held as a rifle (Shooter miniature), added to a shrunk body by
    // ShooterMiniRuleSet: Attack fires small physical balls at what the
    // crosshair is on. The rule set asks for the hits (FindHit) and says when
    // firing is allowed (CanFire). The body's kick stays on for the balls and
    // the other player (LocalUnarmedMelee.SetKickOnly). The animations are in
    // LocalShooterWeapon.Animation.cs.
    //
    // After everything else (PuppetMaster runs at 10100): the cue goes where
    // the right hand really ended up this frame.
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public partial class LocalShooterWeapon : MonoBehaviour
    {
        private const int ShotCount = 16;
        private const float CrosshairLength = 10f;
        private const float CrosshairThickness = 2f;
        private const float ArmedCrosshairScale = 1.8f;
        private const float ArmedLabelWidth = 240f;
        private const float ArmedLabelHeight = 24f;
        private const float PalmAlongHand = 0.7f;
        private const float TraceInterval = 2f;
        private const float GrappleArrival = 0.02f;
        private const float GrappleStuckShare = 0.1f;
        private const float GrappleLineWidth = 0.003f;
        private static readonly Color IceColor = new Color(0.55f, 0.85f, 1f);

        private ShooterMiniRuleSet rules;
        private ShooterMiniSettings cfg;
        private int player;
        private Transform aim;
        private Camera view;
        private InputAction fireAction;
        private float nextShot;
        private int nextSlot;
        private int aimMask;
        private readonly ShooterMiniProjectile[] shots = new ShooterMiniProjectile[ShotCount];
        private Material material, iceMaterial;
        private PhysicsMaterial bounce;
        // Recoil: thrown at this velocity, dying out by pushUntil.
        private Vector3 pushVelocity;
        private float pushUntil, pushTime;
        private Coroutine grapple;
        private LineRenderer grappleLine;
        // Can the power waiting for the shot go off? Read once a frame.
        private bool armedReady = true;

        // The cue: a mesh copy of the Cue prefab with no physics or scripts.
        // Points and rotations in its own space, in metres.
        private Transform cue;
        private Vector3 cueTip, cueAxis, cueRightGrip;
        // Where the cue is, in the right hand: what Aim IK points. Made once the
        // hold is playing (Calibrate).
        private Transform cueInHand;
        private Transform rightHand, leftHand;
        // The left hand as the clip holds it: against the right one, and its
        // palm from its wrist in its own space (world lengths).
        private Quaternion leftFromRight;
        private Vector3 leftPalmInHand;
        private Vector3 leftWristWanted;
        private float nextTrace;
        private float size = 1f;
        // Not an AimIK component: PuppetMaster takes over the ones it finds
        // under its target, in their own order, and this one has to run just
        // before FBBIK reads the pose.
        private readonly IKSolverAim aimSolver = new IKSolverAim();
        private float holdWeight;
        private FullBodyBipedIK fullBodyIK;

        public static LocalShooterWeapon Equip(LocalPlayerShrink body, int player, ShooterMiniRuleSet rules, ShooterMiniSettings cfg)
        {
            LocalShooterWeapon weapon = body.gameObject.AddComponent<LocalShooterWeapon>();
            weapon.Init(player, rules, cfg);
            return weapon;
        }

        private void Init(int player, ShooterMiniRuleSet rules, ShooterMiniSettings cfg)
        {
            this.player = player;
            this.rules = rules;
            this.cfg = cfg;

            // Punches share the shot's button, the kick doesn't.
            if (TryGetComponent(out LocalUnarmedMelee melee)) melee.SetKickOnly(cfg.ballKickSpeed, cfg.spartanBallKickSpeed, cfg.spartanLaunchScale);

            PlayerInput input = GetComponent<PlayerInput>();
            fireAction = input.actions.FindActionMap("Player", throwIfNotFound: true).FindAction("Attack", throwIfNotFound: true);

            view = input.camera != null ? input.camera : GetComponentInChildren<Camera>(true);
            LocalFpsPlayerController fps = GetComponent<LocalFpsPlayerController>();
            aim = view != null ? view.transform : fps != null && fps.CameraPivot != null ? fps.CameraPivot : transform;
            aimMask = ~LayerMask.GetMask("Ragdoll");

            Color color = cfg.playerColors != null && player < cfg.playerColors.Length ? cfg.playerColors[player] : Color.white;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ShooterMini Shot" };
            material.SetColor("_BaseColor", color);
            iceMaterial = ShooterPowerFx.Solid(IceColor, "ShooterMini Ice Shot");
            bounce = new PhysicsMaterial("ShooterMini Shot")
            {
                bounciness = cfg.shotBounciness,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            Collider owner = GetComponent<CharacterController>();
            for (int i = 0; i < ShotCount; i++)
                shots[i] = ShooterMiniProjectile.Create(cfg, material, bounce, owner, player);

            InitAnimation();
            // Its thumbs up takes the right hand, which holds the cue here.
            if (TryGetComponent(out LocalPowerGesture gesture)) gesture.enabled = false;
            fullBodyIK = GetComponentInChildren<FullBodyBipedIK>(true);
            size = fps != null ? fps.SizeScale : 1f;
            foreach (CinemachineCamera cam in GetComponentsInChildren<CinemachineCamera>(true))
                if (cam.name.Contains("FPS"))
                    cam.transform.position += cam.transform.forward * (cfg.cameraForward * size);
            BuildCue();
        }

        // A copy of the Cue prefab's meshes at this body's size, not the prefab
        // itself: it would come with its rigidbody and grabbing scripts. Its
        // axis comes from its own carry grips (left towards the tip).
        //
        // Only the fingers come from the prefab's grips. Their wrists are for
        // carrying: pointing the cue with them, Aim IK swung the arms behind the
        // back (04/11).
        private void BuildCue()
        {
            GameObject prefab = cfg.cuePrefab;
            rightHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            leftHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (fullBodyIK == null || rightHand == null || leftHand == null) return;
            if (prefab == null)
            {
                Debug.LogWarning("[ShooterMini] Pas de queue à tenir : Cue Prefab est vide dans ShooterMiniSettings (Ensure Config Assets Exist le remplit).", this);
                return;
            }
            InteractionTarget prefabLeft = null, prefabRight = null;
            foreach (InteractionTarget grip in prefab.GetComponentsInChildren<InteractionTarget>(true))
            {
                if (grip.effectorType == FullBodyBipedEffector.LeftHand) prefabLeft = grip;
                else if (grip.effectorType == FullBodyBipedEffector.RightHand) prefabRight = grip;
            }
            if (prefabLeft == null || prefabRight == null)
            {
                Debug.LogWarning($"[ShooterMini] {prefab.name} n'a pas ses deux prises de main : pas de queue à tenir.", this);
                return;
            }

            Transform root = prefab.transform;
            // The prefab's own scale counts (the cue was sized through it), not
            // where it stands.
            Matrix4x4 toCue = Matrix4x4.Scale(root.localScale * size) * root.worldToLocalMatrix;
            Vector3 rightPoint = toCue.MultiplyPoint3x4(prefabRight.transform.position);
            Vector3 axis = (toCue.MultiplyPoint3x4(prefabLeft.transform.position) - rightPoint).normalized;
            cue = new GameObject("ShooterMini Cue").transform;
            float tip = float.MinValue, butt = float.MaxValue;
            Vector3 centerSum = Vector3.zero;
            int parts = 0;
            foreach (MeshRenderer source in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Matrix4x4 m = toCue * source.transform.localToWorldMatrix;

                GameObject part = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.layer = source.gameObject.layer;
                part.transform.SetParent(cue, false);
                part.transform.SetLocalPositionAndRotation(m.GetPosition(), m.rotation);
                part.transform.localScale = m.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer copy = part.GetComponent<MeshRenderer>();
                copy.sharedMaterials = source.sharedMaterials;
                copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Bounds b = filter.sharedMesh.bounds;
                centerSum += m.MultiplyPoint3x4(b.center);
                parts++;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = b.center + Vector3.Scale(b.extents, new Vector3((corner & 1) * 2 - 1, (corner & 2) - 1, (corner & 4) / 2 - 1));
                    float along = Vector3.Dot(m.MultiplyPoint3x4(local) - rightPoint, axis);
                    tip = Mathf.Max(tip, along);
                    butt = Mathf.Min(butt, along);
                }
            }
            // On the cue's middle line, the grips being off it by a wrist's
            // width. Held near the butt: what's behind the hand goes into the
            // body at the hip.
            Vector3 center = parts > 0 ? centerSum / parts : rightPoint;
            Vector3 onLine = center + axis * Vector3.Dot(rightPoint - center, axis);
            cueAxis = axis;
            cueRightGrip = onLine + axis * (butt + cfg.cueButtMargin * size);
            cueTip = onLine + axis * Mathf.Max(tip, 0f);
            cue.gameObject.SetActive(false);

            fullBodyIK.solver.OnPreUpdate += HoldCue;
            // Before the cue goes into the hand: the Poser maps the hand's
            // children, one more and it gives up.
            PoseHand(fullBodyIK.references.leftHand, prefabLeft);
            PoseHand(fullBodyIK.references.rightHand, prefabRight);
        }

        private bool HoldIsPlaying =>
            fireLayer < 0 || (upper == Upper.Hold && !animator.IsInTransition(fireLayer) && animator.GetLayerWeight(fireLayer) >= 0.99f);

        // Where the fist closes on this pose, cue-wide inside the palm.
        private Vector3 PalmOf(Transform hand, bool right)
        {
            HandPose.Measure(animator, right, out Vector3 along, out Vector3 into);
            Transform finger = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Vector3 knuckles = finger != null ? finger.position : hand.position + along * (0.1f * size);
            return Vector3.Lerp(hand.position, knuckles, PalmAlongHand) + into * (cfg.cuePalmOffset * size);
        }

        // On the hold as it plays on this body, whatever it became on its short
        // arms: the cue goes through the right fist, straight ahead, and stays
        // there in the hand. Aim IK only has the difference to make up for, as
        // the plugin's aiming expects of a gun fitted on its aiming pose.
        private void Calibrate()
        {
            Quaternion rotation = Quaternion.FromToRotation(cueAxis, transform.forward);
            cueInHand = new GameObject("ShooterMini Cue In Hand").transform;
            cueInHand.SetParent(rightHand, false);
            cueInHand.SetPositionAndRotation(PalmOf(rightHand, true) - rotation * cueRightGrip, rotation);
            leftFromRight = Quaternion.Inverse(rightHand.rotation) * leftHand.rotation;
            leftPalmInHand = Quaternion.Inverse(leftHand.rotation) * (PalmOf(leftHand, false) - leftHand.position);
            SetUpAim();
            cue.gameObject.SetActive(true);
        }

        // The spine and the right arm, as in the plugin's SecondHandOnGun demo.
        // The solver's weight also grows along the chain: the arm does most of
        // it, and the hand finishes the aim.
        private void SetUpAim()
        {
            (HumanBodyBones bone, float weight)[] chain =
            {
                (HumanBodyBones.Spine, cfg.aimSpineWeight),
                (HumanBodyBones.Chest, cfg.aimSpineWeight),
                (HumanBodyBones.UpperChest, cfg.aimSpineWeight),
                (HumanBodyBones.RightUpperArm, cfg.aimArmWeight),
                (HumanBodyBones.RightLowerArm, cfg.aimArmWeight),
                (HumanBodyBones.RightHand, 1f),
            };
            var bones = new System.Collections.Generic.List<Transform>();
            var weights = new System.Collections.Generic.List<float>();
            foreach ((HumanBodyBones bone, float weight) in chain)
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                bones.Add(t);
                weights.Add(weight);
            }
            aimSolver.transform = cueInHand;
            aimSolver.axis = cueAxis;
            // No clamp: the body turns with the view, the crosshair is never behind.
            aimSolver.clampWeight = 0f;
            aimSolver.SetChain(bones.ToArray(), fullBodyIK.transform);
            for (int i = 0; i < weights.Count; i++) aimSolver.bones[i].weight = weights[i];
        }

        // The fingers around the cue, as LocalCueHolder does.
        private static void PoseHand(Transform hand, InteractionTarget grip)
        {
            if (hand == null || !hand.TryGetComponent(out Poser poser)) return;
            poser.SetPoseRoot(grip.transform, grip.bones != null ? grip.bones : new Transform[0], 4f);
            poser.AutoMapping();
            poser.weight = 1f;
        }

        private void OnDestroy()
        {
            if (fullBodyIK != null) fullBodyIK.solver.OnPreUpdate -= HoldCue;
            foreach (ShooterMiniProjectile shot in shots)
                if (shot != null) Destroy(shot.gameObject);
            if (cue != null) Destroy(cue.gameObject);
            if (material != null) Destroy(material);
            if (iceMaterial != null) Destroy(iceMaterial);
            EndGrappleLine();
            if (bounce != null) Destroy(bounce);
        }

        private void Update()
        {
            UpdateAnimation(Time.deltaTime);
            holdWeight = Mathf.MoveTowards(holdWeight, IsDown ? 0f : 1f, Time.deltaTime / Mathf.Max(cfg.animationFade, 0.01f));
            UpdateRecoil();
            if (rules == null || !rules.CanFire(player)) return;
            armedReady = rules.ArmedPowerReady(player);
            if (armedReady && fireAction.IsPressed() && Time.time >= nextShot) Fire();
        }

        // Each frame on the animated pose, just before FBBIK reads it: Aim IK
        // points the cue at the crosshair, then FBBIK keeps the right hand where
        // that left it, moved clear of the belly (the clip's hand is level with
        // the hip, 15 cm behind the root on this body: log of 04/11), and brings
        // the left one onto the cue. Settings read every frame: they can be tuned
        // during play. Knocked down, FBBIK is off and this doesn't run.
        private void HoldCue()
        {
            if (cueInHand == null)
            {
                if (!HoldIsPlaying) return;
                Calibrate();
            }
            aimSolver.IKPosition = aim.position + aim.forward * cfg.cueAimDistance;
            aimSolver.IKPositionWeight = holdWeight;
            aimSolver.Update();

            IKEffector right = fullBodyIK.solver.rightHandEffector;
            Vector3 clear = transform.rotation * (cfg.cueHandOffset * size);
            right.position = rightHand.position + clear;
            right.rotation = rightHand.rotation;
            right.positionWeight = right.rotationWeight = holdWeight;

            IKEffector left = fullBodyIK.solver.leftHandEffector;
            left.rotation = rightHand.rotation * leftFromRight;
            Vector3 leftGrip = cueRightGrip + cueAxis * (cfg.cueHandSpan * size);
            leftWristWanted = cueInHand.position + clear + cueInHand.rotation * leftGrip - left.rotation * leftPalmInHand;
            left.position = leftWristWanted;
            left.positionWeight = left.rotationWeight = holdWeight;
        }

        // After everything else, where the right hand really ended up this
        // frame. Not a child of the hand: its meshes are already at this body's
        // size, and the bones' scale would shrink them again.
        private void LateUpdate()
        {
            if (cue == null || cueInHand == null) return;
            cue.SetPositionAndRotation(cueInHand.position, cueInHand.rotation);
            if (cfg.debugLogs && Time.time >= nextTrace && !IsDown)
            {
                nextTrace = Time.time + TraceInterval;
                TraceHold();
            }
        }

        // Diagnostic: is the cue really on the crosshair, and the left hand on
        // the cue, once everything has run? Lengths at full size.
        private void TraceHold()
        {
            Vector3 wanted = aim.position + aim.forward * cfg.cueAimDistance - cueInHand.position;
            float off = Vector3.Angle(cueInHand.rotation * cueAxis, wanted);
            Vector3 hand = transform.InverseTransformPoint(rightHand.position);
            Debug.Log($"[ShooterMini] Queue de J{player + 1} : {off:0.#}° du viseur, main gauche à {Vector3.Distance(leftHand.position, leftWristWanted) / size * 100f:0} cm de sa place, main droite à droite {hand.x:0.00} m, haut {hand.y:0.00} m, devant {hand.z:0.00} m, buste {Vector3.Angle(transform.up, animator.GetBoneTransform(HumanBodyBones.Spine).up):0}° de la verticale.", this);
        }

        // From the tip, and onto what the crosshair is on: the view and the cue
        // are offset, a shot along the view wouldn't land where it's aimed.
        private void Fire()
        {
            nextShot = Time.time + cfg.fireInterval;
            if (!rules.FireArmedPower(player))
            {
                Vector3 muzzle = Muzzle;
                NextShot().Launch(muzzle, (AimedPoint(cfg.cueAimDistance) - muzzle).normalized * cfg.shotSpeed, cfg.shotLifetime, cfg.shotBounces, material);
            }
            PlayShot();
        }

        private ShooterMiniProjectile NextShot()
        {
            ShooterMiniProjectile shot = shots[nextSlot];
            nextSlot = (nextSlot + 1) % ShotCount;
            return shot;
        }

        private Vector3 Muzzle => cue != null && cueInHand != null ? cue.TransformPoint(cueTip) : aim.position + aim.forward * cfg.shotRadius * 4f;

        public Vector3 AimOrigin => aim.position;
        public Vector3 AimDirection => aim.forward;

        // What the crosshair is on, within range, else the point at that range.
        public Vector3 AimedPoint(float range) =>
            Physics.Raycast(aim.position, aim.forward, out RaycastHit hit, range, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : aim.position + aim.forward * range;

        // ---------- Powers (ShooterPower) ----------

        public void FireFreezeShot(float playerTime, float ballTime)
        {
            Vector3 muzzle = Muzzle;
            NextShot().Launch(muzzle, (AimedPoint(cfg.cueAimDistance) - muzzle).normalized * cfg.shotSpeed, cfg.shotLifetime, cfg.shotBounces, iceMaterial, playerTime, ballTime);
        }

        // A fan around the crosshair, level with it, and the shooter thrown back.
        public void FireSpread(int pellets, float spread, float recoilSpeed, float recoilTime)
        {
            Vector3 muzzle = Muzzle;
            Vector3 middle = (AimedPoint(cfg.cueAimDistance) - muzzle).normalized;
            pellets = Mathf.Min(pellets, ShotCount);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(Mathf.Lerp(-spread, spread, i / (pellets - 1f)), Vector3.up) * middle;
                NextShot().Launch(muzzle, direction * cfg.shotSpeed, cfg.shotLifetime, cfg.shotBounces, material);
            }
            Vector3 back = -middle;
            back.y = 0f;
            pushVelocity = back.normalized * recoilSpeed;
            pushTime = recoilTime;
            pushUntil = Time.time + recoilTime;
        }

        private void UpdateRecoil()
        {
            if (Time.time >= pushUntil || !controller.enabled || IsDown) return;
            controller.Move(pushVelocity * ((pushUntil - Time.time) / pushTime * Time.deltaTime));
        }

        // Frozen: neither the recoil nor the line goes on carrying them.
        public void StopMoves()
        {
            pushUntil = 0f;
            if (grapple != null) StopCoroutine(grapple);
            grapple = null;
            EndGrappleLine();
        }

        public bool HasGrappleTarget(float range) =>
            Physics.Raycast(aim.position, aim.forward, range, aimMask, QueryTriggerInteraction.Ignore);

        public void FireGrapple(float range, float pullSpeed, float maxTime, float opponentDamage, float opponentKnockSpeed, float ballHitSpeed)
        {
            StopMoves();
            if (!Physics.Raycast(aim.position, aim.forward, out RaycastHit hit, range, aimMask, QueryTriggerInteraction.Ignore)) return;
            grapple = StartCoroutine(Grapple(hit, pullSpeed, maxTime, opponentDamage, opponentKnockSpeed, ballHitSpeed));
        }

        // Pulled along the line to the point hit, carried with it if it moves
        // (a ball, a player). Ends on arrival, against something that blocks
        // the way, when knocked down, or at Max Time. Next to the other player
        // at the end, it knocks them down; next to the ball it caught, it
        // strikes it as a kick does.
        private IEnumerator Grapple(RaycastHit hit, float pullSpeed, float maxTime, float opponentDamage, float opponentKnockSpeed, float ballHitSpeed)
        {
            grappleLine = new GameObject("ShooterMini Grapple").AddComponent<LineRenderer>();
            grappleLine.sharedMaterial = material;
            grappleLine.startWidth = grappleLine.endWidth = GrappleLineWidth;
            grappleLine.positionCount = 2;
            grappleLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            LocalPlayerShrink caught = hit.collider.GetComponentInParent<LocalPlayerShrink>();
            if (!rules.IsOpponent(player, caught)) caught = null;
            PoolBall caughtBall = hit.collider.GetComponentInParent<PoolBall>();
            Transform anchor = hit.transform;
            Vector3 anchorLocal = anchor.InverseTransformPoint(hit.point);
            for (float end = Time.time + maxTime; Time.time < end && !IsDown && anchor != null; )
            {
                Vector3 target = anchor.TransformPoint(anchorLocal);
                Vector3 to = target - transform.TransformPoint(controller.center);
                if (to.magnitude < GrappleArrival) break;
                float step = pullSpeed * Time.deltaTime;
                Vector3 before = transform.position;
                controller.Move(to.normalized * step);
                DrawGrappleLine(target);
                if (Vector3.Distance(before, transform.position) < step * GrappleStuckShare) break;
                yield return null;
            }
            EndGrappleLine();
            grapple = null;
            if (IsDown) yield break;
            // Against each other, their centres never meet.
            body.GetCapsule(out _, out _, out float ours);
            if (caughtBall != null) StrikeBall(caughtBall, ours, ballHitSpeed);
            if (caught == null) yield break;

            caught.GetCapsule(out _, out _, out float theirs);
            Vector3 across = caught.transform.position - transform.position;
            across.y = 0f;
            float reach = theirs + ours + GrappleArrival;
            if (across.sqrMagnitude <= reach * reach)
                rules.GrappleLanded(player, across, opponentDamage, opponentKnockSpeed, "le grappin");
        }

        // Set like a kick's (LocalUnarmedMelee.KickBalls): where the player
        // looks, the same on a ball of any mass.
        private void StrikeBall(PoolBall ball, float ourRadius, float speed)
        {
            Vector3 across = ball.transform.position - transform.position;
            across.y = 0f;
            float reach = ourRadius + ball.Radius + GrappleArrival;
            if (ball.Rigidbody.isKinematic || across.sqrMagnitude > reach * reach) return;
            Vector3 look = aim.forward;
            look.y = 0f;
            ball.Rigidbody.linearVelocity = look.normalized * speed;
            ball.MarkHitBy(player);
            if (cfg.debugLogs) Debug.Log($"[ShooterMini] J{player + 1} frappe la bille {ball.Number} au grappin à {speed:0.##} m/s.", ball);
        }

        private void DrawGrappleLine(Vector3 end)
        {
            grappleLine.SetPosition(0, Muzzle);
            grappleLine.SetPosition(1, end);
        }

        private void EndGrappleLine()
        {
            if (grappleLine != null) Destroy(grappleLine.gameObject);
            grappleLine = null;
        }

        // A shot that went through target's capsule since the last test, used
        // up by the hit. Tested along its path: at this speed it covers several
        // body widths between two frames.
        public bool FindHit(LocalPlayerShrink target, out Vector3 point, out Vector3 direction, out float freeze)
        {
            point = direction = Vector3.zero;
            freeze = 0f;
            target.GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            float reach = radius + cfg.shotRadius + cfg.shotHitMargin;
            foreach (ShooterMiniProjectile shot in shots)
            {
                if (shot == null || !shot.gameObject.activeSelf) continue;
                Vector3 now = shot.transform.position;
                Vector3 before = shot.LastTested;
                shot.LastTested = now;
                if (!SegmentsWithin(before, now, bottom, top, reach, out point)) continue;
                direction = (now - before).normalized;
                freeze = shot.FreezePlayerTime;
                shot.gameObject.SetActive(false);
                return true;
            }
            return false;
        }

        // Closest points of segments ab and cd (Ericson, Real-Time Collision
        // Detection, 5.1.9). point: the one on ab.
        private static bool SegmentsWithin(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float reach, out Vector3 point)
        {
            Vector3 ab = b - a, cd = d - c, ca = a - c;
            float abSqr = Vector3.Dot(ab, ab), cdSqr = Vector3.Dot(cd, cd), f = Vector3.Dot(cd, ca);
            float s, t;
            if (abSqr < 1e-10f)
            {
                s = 0f;
                t = cdSqr < 1e-10f ? 0f : Mathf.Clamp01(f / cdSqr);
            }
            else
            {
                float e = Vector3.Dot(ab, ca);
                if (cdSqr < 1e-10f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-e / abSqr);
                }
                else
                {
                    float m = Vector3.Dot(ab, cd);
                    float denominator = abSqr * cdSqr - m * m;
                    s = denominator > 1e-10f ? Mathf.Clamp01((m * f - e * cdSqr) / denominator) : 0f;
                    t = (m * s + f) / cdSqr;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-e / abSqr);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((m - e) / abSqr);
                    }
                }
            }
            point = a + ab * s;
            return (point - (c + cd * t)).sqrMagnitude <= reach * reach;
        }

        private static GUIStyle armedLabelStyle;
        private static GUIStyle ArmedLabelStyle => armedLabelStyle ?? (armedLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold });

        // In the middle of this player's part of the screen.
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || view == null || rules == null || !rules.CanFire(player)) return;
            Rect r = view.rect;
            float x = (r.x + r.width * 0.5f) * Screen.width;
            float y = (1f - r.y - r.height * 0.5f) * Screen.height;
            // A power waiting for the shot: bigger, in colour, with its name.
            ShooterPower armed = rules.ArmedPower(player);
            float length = armed != null ? CrosshairLength * ArmedCrosshairScale : CrosshairLength;
            Color before = GUI.color;
            if (armed != null)
            {
                GUI.color = armedReady ? cfg.armedCrosshairColor : cfg.armedNoTargetColor;
                GUI.Label(new Rect(x - ArmedLabelWidth * 0.5f, y + length, ArmedLabelWidth, ArmedLabelHeight), armed.PowerName, ArmedLabelStyle);
            }
            GUI.DrawTexture(new Rect(x - length * 0.5f, y - CrosshairThickness * 0.5f, length, CrosshairThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - CrosshairThickness * 0.5f, y - length * 0.5f, CrosshairThickness, length), Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
