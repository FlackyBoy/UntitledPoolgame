using System;
using System.Collections;
using System.Collections.Generic;
using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;

namespace UntitledPoolGame.Interaction
{
    // Picking up, carrying and throwing ordinary objects (LocalGrabbable,
    // not the cue) with the hands, procedurally, just before FBBIK solves:
    //
    // - Pickup: the right hand reaches for the object (knees and hips bend
    //   for a low one), grabs it, and brings it up to the chest.
    // - Light object (below Heavy Mass and Heavy Size): held in the right
    //   hand — parented to the hand bone, so it follows every move of the
    //   hand. Throw: while charging, the arm cocks back behind the head and
    //   the torso turns away; on release the arm whips forward along a
    //   curve and the object leaves the hand partway through the swing.
    // - Heavy object: held in both hands in front of the chest (the object
    //   is placed, the hands follow grips on its sides). Throw: while
    //   charging it goes up behind the head; on release it's thrown forward
    //   from overhead, like a football throw-in.
    //
    // The throw's impulse (direction and power) comes from
    // LocalPlayerHandController, unchanged; this only decides when and from
    // where the object leaves the hands. Right after release the object
    // ignores the thrower's own colliders for a moment so it can't hit him
    // on the way out.
    //
    // Added to every player by LocalPlayerHandController; disabled, the old
    // "stuck in front of the player" carry is used instead.
    [RequireComponent(typeof(LocalPlayerHandController))]
    public class LocalObjectHands : MonoBehaviour
    {
        // Tooltips are in French: they are read by the designer in the
        // Inspector.
        [Header("References (auto)")]
        [Tooltip("Caméra du joueur (vide = la première caméra sous ce joueur) : donne la direction du bras au lancer.")]
        [SerializeField] private Transform viewTransform;
        [Tooltip("Full Body Biped IK du personnage (vide = le premier trouvé sous ce joueur).")]
        [SerializeField] private FullBodyBipedIK fullBodyIK;

        [Header("Light or heavy")]
        [Tooltip("Masse (kg) à partir de laquelle un objet se tient et se lance à deux mains.")]
        [SerializeField] private float heavyMass = 4f;
        [Tooltip("Taille (mètres, plus grande dimension) à partir de laquelle un objet se tient et se lance à deux mains.")]
        [SerializeField] private float heavySize = 0.5f;

        [Header("Poses (metres from the right shoulder, for a 0.6 m arm, scaled)")]
        [Tooltip("Longueur de bras pour laquelle les distances de cette section sont écrites ; mises à l'échelle du bras réel.")]
        [SerializeField] private float referenceArmLength = 0.6f;
        // Renamed from lightCarry/heavyCarry on 30/09 so the new defaults
        // (visible bottom-right; heavy object kept off the view, measured
        // from its near face) replace the ones saved on the component.
        [Tooltip("Objet léger porté : main droite (droite, haut, avant) par rapport à l'épaule. Par défaut en bas à droite de l'écran.")]
        [SerializeField] private Vector3 lightCarryHand = new Vector3(0.12f, -0.3f, 0.35f);
        [Tooltip("Lancer léger, armé : main derrière la tête (droite, haut, arrière négatif) ; à pleine charge, recul et hauteur en plus ci-dessous.")]
        [SerializeField] private Vector3 lightWindup = new Vector3(0.15f, 0.15f, -0.15f);
        [Tooltip("Lancer léger : recul (mètres) et hauteur supplémentaires de la main à pleine charge.")]
        [SerializeField] private Vector2 lightWindupCharged = new Vector2(0.2f, 0.05f);
        [Tooltip("Lancer léger : point où le bras est tendu vers l'avant (le long du regard), distance depuis l'épaule.")]
        [SerializeField] private float lightReleaseReach = 0.55f;
        [Tooltip("Lancer léger : fin du geste, bras redescendu en travers du corps (droite, haut, avant).")]
        [SerializeField] private Vector3 lightFollowThrough = new Vector3(-0.1f, -0.35f, 0.4f);
        [Tooltip("Objet lourd porté, par rapport au milieu des épaules : décalage à droite, hauteur du centre de l'objet (négatif = plus bas, pour ne pas masquer la vue ; la moitié de la hauteur de l'objet est retirée en plus), et écart entre la poitrine et la face avant de l'objet.")]
        [SerializeField] private Vector3 heavyCarryOffset = new Vector3(0f, -0.3f, 0.2f);
        [Tooltip("Objet lourd : hauteur des mains sur ses côtés, en part de sa demi-hauteur sous le centre (0 = au centre, 0,5 = un peu dessous pour le soutenir).")]
        [SerializeField, Range(0f, 1f)] private float heavyGripBelowCenter = 0.35f;
        // Renamed from heavyWindup on 01/10 (the box ended up resting on the
        // head: placed from the shoulders, not clear of the head).
        [Tooltip("Lancer lourd, armé : centre de l'objet (droite, haut, avant) par rapport au milieu des épaules ; il reste de toute façon au moins Head Clearance au-dessus de la tête. À pleine charge, recul supplémentaire ci-dessous.")]
        [SerializeField] private Vector3 heavyWindupCenter = new Vector3(0f, 0.3f, 0.1f);
        [Tooltip("Lancer lourd : marge (mètres) entre le dessus de la tête et le bas de l'objet armé.")]
        [SerializeField] private float headClearance = 0.12f;
        [Tooltip("Objet lourd : force avec laquelle les coudes sont écartés vers l'extérieur (sans ça ils rentrent dans le corps).")]
        [SerializeField, Range(0f, 1f)] private float heavyElbowOut = 0.7f;
        [Tooltip("Lancer lourd : recul supplémentaire (mètres) de l'objet à pleine charge.")]
        [SerializeField] private float heavyWindupCharged = 0.15f;
        [Tooltip("Lancer lourd : point de lâcher, distance devant (le long du regard) et au-dessus du milieu des épaules.")]
        [SerializeField] private Vector2 heavyRelease = new Vector2(0.5f, 0.25f);

        [Header("Body")]
        [Tooltip("Rotation du buste (degrés) à pleine charge d'un lancer léger (épaule droite en arrière), puis dans le lancer.")]
        [SerializeField] private float throwTorsoTwist = 25f;
        [Tooltip("Part de la hauteur à descendre prise par les jambes pour ramasser un objet bas.")]
        [SerializeField, Range(0f, 1f)] private float crouchAmount = 0.85f;
        [Tooltip("Descente maximale du corps, en part de la hauteur des hanches.")]
        [SerializeField, Range(0f, 0.8f)] private float maxCrouchFraction = 0.45f;
        [Tooltip("Flexion maximale du buste (degrés) pour ramasser un objet bas.")]
        [SerializeField, Range(0f, 90f)] private float maxHipBend = 75f;

        [Header("Timing (seconds)")]
        [SerializeField, Tooltip("Durée du geste vers l'objet.")] private float reachTime = 0.35f;
        [SerializeField, Tooltip("Durée de la remontée de l'objet jusqu'à la poitrine.")] private float liftTime = 0.3f;
        [SerializeField, Tooltip("Temps pour armer le bras au début de la charge.")] private float windupTime = 0.15f;
        [SerializeField, Tooltip("Durée du geste de lancer, objet léger.")] private float lightSwingTime = 0.14f;
        [SerializeField, Tooltip("Durée du geste de lancer, objet lourd.")] private float heavySwingTime = 0.22f;
        [SerializeField, Range(0f, 1f), Tooltip("Moment du geste (en part de sa durée) où l'objet quitte la main.")] private float releaseAt = 0.55f;
        [SerializeField, Tooltip("Durée du retour des bras après un lancer ou un lâcher.")] private float recoverTime = 0.25f;
        [SerializeField, Tooltip("Temps (secondes) pendant lequel l'objet lancé ou posé ignore le corps du joueur.")] private float ignoreThrowerTime = 0.4f;
        [SerializeField, Tooltip("Vitesse (m/s) donnée vers l'avant à un objet posé (touche Interagir), pour qu'il tombe devant et pas sur le joueur.")] private float dropNudge = 1f;

        [Header("Debug")]
        [SerializeField, Tooltip("Écrit dans la console la prise (léger ou lourd), le lancer et le lâcher.")] private bool debugLogs = true;

        private enum Phase { Idle, Reaching, Lifting, Held, Throwing, Recovering }

        private Phase phase = Phase.Idle;
        private float phaseTime;
        private LocalGrabbable held;
        private Rigidbody heldBody;
        private bool heavy;
        private float bodyScale = 1f;

        // Pickup: grips in the object's own space (stay on it while it's
        // still lying), and the posture to reach them.
        private Vector3 rightGripLocal, leftGripLocal;
        private float crouch, hipBend;
        private Vector3 leftFootPin, rightFootPin;
        private float crouchNow;

        // Heavy objects are placed by us relative to the player's root;
        // their rotation there is kept from the grab.
        private Quaternion heavyRotationLocal;
        private Vector3 heavyGrabLocalPosition;
        private Vector3 liftFromLocal;    // light: hand position at the grab, in the root's space
        private Vector3 objectCenterLocal;  // object's centre in its own space
        private float objectHalfDepth, objectHalfHeight;   // along the player's forward / up at pickup

        // Hand orientation: the hands' own fingers/back directions (in each
        // hand bone's space, whatever the rig's axes), measured on the
        // animated pose, so a palm can be turned toward a direction.
        private Vector3 fingersInRight, backInRight, fingersInLeft, backInLeft;
        private bool handsMeasured;
        private Vector3 grabPalmLocal;      // light: palm direction at the grab, in the root's space
        private bool logHeldPose;

        // Throw.
        private float charge;             // 0..1, set by LocalPlayerHandController while Attack is held
        private float chargeHeld;         // how long a charge has been building (windup blend)
        private Vector3 throwImpulse;
        private bool released;
        private Action onReleased;
        private float twist;

        // Recover: hands ease off from where they were.
        private Vector3 recoverRight, recoverLeft;

        public bool IsBusy => phase != Phase.Idle;
        public bool IsSettled => phase == Phase.Held;
        public bool IsThrowing => phase == Phase.Throwing;
        public bool Holds(LocalGrabbable g) => g != null && g == held && (phase == Phase.Reaching || phase == Phase.Lifting || phase == Phase.Held);

        private void Awake()
        {
            if (fullBodyIK == null) fullBodyIK = GetComponentInChildren<FullBodyBipedIK>(true);
            if (viewTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null) viewTransform = cam.transform;
            }
        }

        private void OnEnable()
        {
            if (fullBodyIK != null) fullBodyIK.solver.OnPreUpdate += ApplyIK;
        }

        private void OnDisable()
        {
            if (fullBodyIK != null) fullBodyIK.solver.OnPreUpdate -= ApplyIK;
            if (held != null) LetGo(Vector3.zero);
            ClearIK();
            phase = Phase.Idle;
        }

        // ---- Called by LocalPlayerHandController ----

        public bool TryStartPickup(LocalGrabbable grabbable)
        {
            if (phase != Phase.Idle || grabbable == null || grabbable.IsHeld || fullBodyIK == null || viewTransform == null) return false;
            BipedReferences r = fullBodyIK.references;
            if (r.rightUpperArm == null || r.rightHand == null) return false;

            held = grabbable;
            heldBody = grabbable.Body != null ? grabbable.Body : grabbable.GetComponent<Rigidbody>();
            Bounds b = ObjectBounds(grabbable);
            heavy = (heldBody != null && heldBody.mass >= heavyMass) || Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) >= heavySize;

            PlanGrips(b);
            handsMeasured = false;           // re-read on the next pre-solve pass (animated pose)
            held.MarkExternallyHeld(true);   // kinematic, solid collider off, IsHeld
            charge = 0f;
            chargeHeld = 0f;
            SetPhase(Phase.Reaching);
            if (debugLogs) Debug.Log($"[ObjectHands] {name}: picking up {held.name} ({(heavy ? "heavy — both hands" : "light — right hand")}, {(heldBody != null ? heldBody.mass : 0f):F1} kg, {Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)):F2} m, crouch {crouch:F2} m, hip bend {hipBend:F0}°)", this);
            return true;
        }

        // Interact while holding: let go where it is.
        public bool TryDrop(LocalGrabbable grabbable)
        {
            if (grabbable == null || grabbable != held) return false;
            if (phase == Phase.Throwing) return true;   // mid-throw: the throw finishes it
            if (debugLogs) Debug.Log($"[ObjectHands] {name}: drops {held.name}", this);

            // Set down in front, not let go where it is: held overhead (or
            // just above the body) it fell straight onto the player and
            // knocked him down. Moved out in front at chest height, it
            // ignores the player's body for a moment and gets a light nudge
            // forward.
            Vector3 f = FlatForward();
            if (fullBodyIK != null)
            {
                BipedReferences r = fullBodyIK.references;
                Vector3 front = ChestCenter(r) + f * (0.35f * bodyScale + objectHalfDepth) - Vector3.up * (0.3f * bodyScale);
                Vector3 centerNow = held.transform.TransformPoint(objectCenterLocal);
                held.transform.position += front - centerNow;
            }
            LocalGrabbable dropped = held;
            float mass = heldBody != null ? heldBody.mass : 1f;
            // Colliders are switched back on by the release: ignore after it
            // (an ignore set on a disabled collider may not hold).
            LetGo(f * (dropNudge * mass));
            IgnoreThrower(dropped);
            StartRecover();
            return true;
        }

        // Set every frame while Attack is held (0..1).
        public void SetCharge(float fraction)
        {
            if (phase != Phase.Held) return;
            if (chargeHeld <= 0f && debugLogs)
                Debug.Log($"[ObjectHands] {name}: throw charge starts (Attack held) — arm/object going into the windup", this);
            charge = Mathf.Clamp01(fraction);
            chargeHeld += Time.deltaTime;
        }

        public bool TryThrow(LocalGrabbable grabbable, Vector3 impulse, Action releasedCallback)
        {
            if (grabbable == null || grabbable != held || phase != Phase.Held) return false;
            throwImpulse = impulse;
            onReleased = releasedCallback;
            released = false;
            SetPhase(Phase.Throwing);
            if (debugLogs) Debug.Log($"[ObjectHands] {name}: throws {held.name} ({(heavy ? "two hands, from overhead" : "overarm")}, charge {charge:F2}, impulse {impulse.magnitude:F1})", this);
            return true;
        }

        // Knocked down, disabled...: let go at once.
        public void ReleaseNow()
        {
            if (held != null) LetGo(Vector3.zero);
            ClearIK();
            phase = Phase.Idle;
        }

        // ---- Planning ----

        private static Bounds ObjectBounds(LocalGrabbable g)
        {
            bool any = false;
            Bounds b = new Bounds(g.transform.position, Vector3.zero);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any)
                foreach (Renderer rend in g.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = rend.bounds; any = true; } else b.Encapsulate(rend.bounds);
                }
            return b;
        }

        // Nearest point of the object's solid colliders to `from` (convex
        // shapes exactly; a non-convex mesh collider only supports its
        // bounds). Called before the colliders are switched off for the hold.
        private static Vector3 SurfacePoint(LocalGrabbable g, Vector3 from)
        {
            Vector3 best = g.transform.position;
            float bestDistance = float.MaxValue;
            foreach (Collider c in g.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                Vector3 p = c is MeshCollider mc && !mc.convex ? c.ClosestPointOnBounds(from) : c.ClosestPoint(from);
                float d = (p - from).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return best;
        }

        private void PlanGrips(Bounds b)
        {
            BipedReferences r = fullBodyIK.references;
            float armLength = Vector3.Distance(r.rightUpperArm.position, r.rightForearm.position) + Vector3.Distance(r.rightForearm.position, r.rightHand.position);
            bodyScale = armLength / Mathf.Max(0.01f, referenceArmLength);

            Vector3 flatForward = FlatForward();
            Vector3 right = Vector3.Cross(Vector3.up, flatForward);
            objectCenterLocal = held.transform.InverseTransformPoint(b.center);
            objectHalfDepth = Mathf.Abs(b.extents.x * flatForward.x) + Mathf.Abs(b.extents.z * flatForward.z);
            objectHalfHeight = b.extents.y;
            if (heavy)
            {
                // Each side of the object's real surface (not its world-axis
                // box, larger than a turned object — hands ended up in the
                // air), a little below the centre to hold it up.
                float far = b.extents.magnitude + 1f;
                Vector3 side = b.center - Vector3.up * (objectHalfHeight * heavyGripBelowCenter);
                rightGripLocal = held.transform.InverseTransformPoint(SurfacePoint(held, side + right * far));
                leftGripLocal = held.transform.InverseTransformPoint(SurfacePoint(held, side - right * far));
            }
            else
            {
                // The point of the object's surface nearest the right shoulder.
                rightGripLocal = held.transform.InverseTransformPoint(SurfacePoint(held, r.rightUpperArm.position));
                leftGripLocal = rightGripLocal;
            }

            // Posture: same idea as LocalCueHolder — knees take most of the
            // height below a comfortable spot, the hips the rest.
            Vector3 shoulders = (r.leftUpperArm.position + r.rightUpperArm.position) * 0.5f;
            Vector3 comfort = shoulders + flatForward * (0.35f * bodyScale) - Vector3.up * (0.35f * bodyScale);
            float gripY = held.transform.TransformPoint(rightGripLocal).y;
            leftFootPin = r.leftFoot != null ? r.leftFoot.position : transform.position;
            rightFootPin = r.rightFoot != null ? r.rightFoot.position : transform.position;
            float hipHeight = r.leftThigh != null && r.rightThigh != null
                ? (r.leftThigh.position.y + r.rightThigh.position.y) * 0.5f - (leftFootPin.y + rightFootPin.y) * 0.5f
                : 0.9f * bodyScale;
            crouch = Mathf.Clamp((comfort.y - gripY) * crouchAmount, 0f, Mathf.Max(0f, hipHeight) * maxCrouchFraction);
            float torso = r.pelvis != null ? Mathf.Max(0.1f, shoulders.y - r.pelvis.position.y) : 0.5f * bodyScale;
            float remaining = Mathf.Max(0f, comfort.y - gripY - crouch);
            hipBend = Mathf.Min(maxHipBend, Mathf.Acos(Mathf.Clamp(1f - remaining / torso, -1f, 1f)) * Mathf.Rad2Deg);
        }

        // ---- Phases ----

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        private void Update()
        {
            if (phase == Phase.Idle) return;
            if ((phase == Phase.Reaching || phase == Phase.Lifting || phase == Phase.Held || phase == Phase.Throwing) && held == null)
            {
                ReleaseNow();
                return;
            }

            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case Phase.Reaching:
                    if (phaseTime >= reachTime) Grab();
                    break;
                case Phase.Lifting:
                    if (phaseTime >= liftTime)
                    {
                        SetPhase(Phase.Held);
                        logHeldPose = debugLogs;   // where it ends up, logged on the next pre-solve pass
                    }
                    break;
                case Phase.Held:
                    // Charge dropped back to nothing without a throw (not
                    // expected, but keeps the pose honest).
                    if (charge <= 0f) chargeHeld = 0f;
                    break;
                case Phase.Throwing:
                    float swing = heavy ? heavySwingTime : lightSwingTime;
                    if (!released && phaseTime >= swing * releaseAt) ReleaseThrow();
                    if (phaseTime >= swing) StartRecover();
                    break;
                case Phase.Recovering:
                    if (phaseTime >= recoverTime) { ClearIK(); phase = Phase.Idle; }
                    break;
            }
        }

        private void Grab()
        {
            if (heavy)
            {
                // Placed by us from now on, relative to the player's root.
                held.transform.SetParent(transform, worldPositionStays: true);
                heavyGrabLocalPosition = held.transform.localPosition;
                heavyRotationLocal = held.transform.localRotation;
            }
            else
            {
                // In the right hand: follows it, including the throw swing.
                Vector3 gripWorld = held.transform.TransformPoint(rightGripLocal);
                // The lift starts from where the hand closed on it, palm
                // toward the object's centre.
                liftFromLocal = transform.InverseTransformPoint(gripWorld);
                grabPalmLocal = transform.InverseTransformDirection((held.transform.TransformPoint(objectCenterLocal) - gripWorld).normalized);
                held.transform.SetParent(fullBodyIK.references.rightHand, worldPositionStays: true);
                // Bring the grip point onto the palm.
                held.transform.position += fullBodyIK.references.rightHand.position - gripWorld;
            }
            SetPhase(Phase.Lifting);
        }

        private void ReleaseThrow()
        {
            released = true;
            LocalGrabbable thrown = held;
            LetGo(throwImpulse);
            IgnoreThrower(thrown);   // after the release, colliders back on
            // A bit of spin, so it doesn't fly perfectly still.
            if (heldBodyForSpin != null && !heldBodyForSpin.isKinematic)
                heldBodyForSpin.AddTorque(UnityEngine.Random.insideUnitSphere * throwImpulse.magnitude * 0.05f, ForceMode.Impulse);
            onReleased?.Invoke();
            onReleased = null;
        }

        private Rigidbody heldBodyForSpin;

        private void LetGo(Vector3 impulse)
        {
            if (held == null) return;
            heldBodyForSpin = heldBody;
            held.ReleaseFromHand(impulse);
            held = null;
            heldBody = null;
        }

        private void StartRecover()
        {
            IKSolverFullBodyBiped s = fullBodyIK.solver;
            recoverRight = s.rightHandEffector.position;
            recoverLeft = s.leftHandEffector.position;
            SetPhase(Phase.Recovering);
        }

        private void ClearIK()
        {
            if (fullBodyIK == null) return;
            IKSolverFullBodyBiped s = fullBodyIK.solver;
            s.rightHandEffector.positionWeight = 0f;
            s.leftHandEffector.positionWeight = 0f;
            s.rightHandEffector.rotationWeight = 0f;
            s.leftHandEffector.rotationWeight = 0f;
            s.leftFootEffector.positionWeight = 0f;
            s.rightFootEffector.positionWeight = 0f;
            twist = 0f;
            RestoreElbows();
        }

        // The thrower's own colliders (body, ragdoll) ignore the object for
        // a moment: released from the hand, it would otherwise hit the arm
        // or chest on its way out (and RagdollHitRelay could knock the
        // thrower down).
        private void IgnoreThrower(LocalGrabbable thrown)
        {
            var objectColliders = thrown.GetComponentsInChildren<Collider>(true);
            var ownColliders = GetComponentsInChildren<Collider>(true);
            var pairs = new List<(Collider, Collider)>();
            foreach (Collider o in objectColliders)
                foreach (Collider mine in ownColliders)
                    if (o != null && mine != null && o != mine && !mine.transform.IsChildOf(thrown.transform))
                    {
                        Physics.IgnoreCollision(o, mine, true);
                        pairs.Add((o, mine));
                    }
            StartCoroutine(RestoreCollisions(pairs));
        }

        private IEnumerator RestoreCollisions(List<(Collider, Collider)> pairs)
        {
            yield return new WaitForSeconds(ignoreThrowerTime);
            foreach (var (a, b) in pairs)
                if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
        }

        // ---- IK (just before FBBIK solves) ----

        private Vector3 FlatForward()
        {
            Vector3 f = Vector3.ProjectOnPlane(viewTransform != null ? viewTransform.forward : transform.forward, Vector3.up);
            return f.sqrMagnitude > 1e-4f ? f.normalized : transform.forward;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void ApplyIK()
        {
            if (phase == Phase.Idle) return;
            BipedReferences r = fullBodyIK.references;
            IKSolverFullBodyBiped s = fullBodyIK.solver;
            Vector3 f = FlatForward(), u = Vector3.up, rt = Vector3.Cross(Vector3.up, f);
            if (!handsMeasured) MeasureHands(r);
            // Light object carried palm up and inward, as if resting in it.
            Vector3 carryPalm = (u - rt * 0.6f).normalized;

            // Torso twist first (throws), so the shoulder reads turned.
            if (twist != 0f && r.spine != null && r.spine.Length > 0)
            {
                Quaternion perBone = Quaternion.AngleAxis(twist / r.spine.Length, Vector3.up);
                foreach (Transform bone in r.spine)
                    if (bone != null) bone.rotation = perBone * bone.rotation;
            }

            switch (phase)
            {
                case Phase.Reaching:
                {
                    float w = Smooth(phaseTime / Mathf.Max(0.01f, reachTime));
                    Posture(w, f);
                    if (held == null) break;
                    Vector3 rightGrip = held.transform.TransformPoint(rightGripLocal);
                    Drive(s.rightHandEffector, rightGrip, w, r.rightUpperArm);
                    Drive(s.leftHandEffector, held.transform.TransformPoint(leftGripLocal), heavy ? w : 0f, r.leftUpperArm);
                    if (heavy)
                    {
                        // Palms facing each other, on the object's sides.
                        Palm(s.rightHandEffector, true, -rt, f, w);
                        Palm(s.leftHandEffector, false, rt, f, w);
                    }
                    else
                    {
                        // Palm toward the object's centre.
                        Palm(s.rightHandEffector, true, held.transform.TransformPoint(objectCenterLocal) - rightGrip, f, w);
                    }
                    break;
                }
                case Phase.Lifting:
                {
                    float e = Smooth(phaseTime / Mathf.Max(0.01f, liftTime));
                    Posture(1f - e, f);
                    if (held == null) break;
                    if (heavy)
                    {
                        Vector3 carry = HeavyCarryPoint(r, f, u, rt);
                        held.transform.position = Vector3.Lerp(transform.TransformPoint(heavyGrabLocalPosition), carry, e);
                        HeavyHands(s, r, rt, f);
                    }
                    else
                    {
                        Drive(s.rightHandEffector, Vector3.Lerp(transform.TransformPoint(liftFromLocal), LightCarryPoint(r, f, u, rt), e), 1f, r.rightUpperArm);
                        Palm(s.rightHandEffector, true, Vector3.Slerp(transform.TransformDirection(grabPalmLocal), carryPalm, e), f, 1f);
                    }
                    break;
                }
                case Phase.Held:
                {
                    if (held == null) break;
                    float wind = Smooth(chargeHeld / Mathf.Max(0.01f, windupTime)) * (charge > 0f || chargeHeld > 0f ? 1f : 0f);
                    if (logHeldPose)
                    {
                        logHeldPose = false;
                        Vector3 carryCenter = heavy ? HeavyCarryPoint(r, f, u, rt) + (held.transform.TransformPoint(objectCenterLocal) - held.transform.position) : LightCarryPoint(r, f, u, rt);
                        Debug.Log($"[ObjectHands] {name}: holding {held.name} ({(heavy ? "heavy" : "light")}) — carry {(heavy ? "object centre" : "hand")} {carryCenter.y - ChestCenter(r).y:F2} m from shoulder height, " +
                                  $"{Vector3.Dot(carryCenter - ChestCenter(r), f):F2} m in front; charge {charge:F2}, windup {wind:F2}; object {objectHalfHeight * 2f:F2} m high, {objectHalfDepth * 2f:F2} m deep", this);
                    }
                    if (heavy)
                    {
                        held.transform.position = Vector3.Lerp(HeavyCarryPoint(r, f, u, rt), HeavyWindupPoint(r, f, u, rt), wind);
                        held.transform.rotation = transform.rotation * heavyRotationLocal;
                        // Fingers up as the object goes overhead.
                        HeavyHands(s, r, rt, Vector3.Slerp(f, u, wind));
                        twist = 0f;
                    }
                    else
                    {
                        Drive(s.rightHandEffector, Vector3.Lerp(LightCarryPoint(r, f, u, rt), LightWindupPoint(r, f, u, rt), wind), 1f, r.rightUpperArm);
                        // Cocked back: palm turns to face the throw, fingers up.
                        Palm(s.rightHandEffector, true, Vector3.Slerp(carryPalm, f, wind), Vector3.Slerp(f, u, wind), 1f);
                        twist = -throwTorsoTwist * charge * wind;
                    }
                    break;
                }
                case Phase.Throwing:
                {
                    float swing = heavy ? heavySwingTime : lightSwingTime;
                    float t = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, swing));
                    if (heavy)
                    {
                        Vector3 from = HeavyWindupPoint(r, f, u, rt), to = HeavyReleasePoint(r, f, u);
                        Vector3 p = Vector3.Lerp(from, to, t * t);   // accelerates into the release
                        Vector3 fingers = Vector3.Slerp(u, f, t);
                        if (held != null)
                        {
                            held.transform.position = p;
                            held.transform.rotation = transform.rotation * heavyRotationLocal;
                            HeavyHands(s, r, rt, fingers);
                        }
                        else
                        {
                            Vector3 offset = rt * 0.2f * bodyScale;
                            Drive(s.rightHandEffector, p + offset, 1f, r.rightUpperArm);
                            Drive(s.leftHandEffector, p - offset, 1f, r.leftUpperArm);
                            Palm(s.rightHandEffector, true, -rt, fingers, 1f);
                            Palm(s.leftHandEffector, false, rt, fingers, 1f);
                        }
                    }
                    else
                    {
                        // Whip: windup → over the shoulder → arm out along the
                        // look → down across the body (follow-through).
                        Vector3 shoulder = r.rightUpperArm.position;
                        Vector3 windup = LightWindupPoint(r, f, u, rt);
                        Vector3 over = shoulder + (u * 0.25f + f * 0.1f + rt * 0.2f) * bodyScale;
                        Vector3 releasePoint = shoulder + viewTransform.forward * (lightReleaseReach * bodyScale) + rt * (0.05f * bodyScale);
                        Vector3 follow = shoulder + (rt * lightFollowThrough.x + u * lightFollowThrough.y + f * lightFollowThrough.z) * bodyScale;
                        Vector3 p;
                        if (t < releaseAt)
                        {
                            float a = Smooth(t / releaseAt), k = 1f - a;
                            p = k * k * windup + 2f * k * a * over + a * a * releasePoint;
                        }
                        else p = Vector3.Lerp(releasePoint, follow, Smooth((t - releaseAt) / (1f - releaseAt)));
                        Drive(s.rightHandEffector, p, 1f, r.rightUpperArm);
                        // Palm pushes along the throw, fingers up then forward.
                        Palm(s.rightHandEffector, true, viewTransform.forward, Vector3.Slerp(u, f, Smooth(t)), 1f);
                        twist = Mathf.Lerp(-throwTorsoTwist * charge, throwTorsoTwist * 0.8f, Smooth(t));
                    }
                    break;
                }
                case Phase.Recovering:
                {
                    float w = 1f - Smooth(phaseTime / Mathf.Max(0.01f, recoverTime));
                    s.rightHandEffector.position = recoverRight;
                    s.rightHandEffector.positionWeight = w;
                    s.rightHandEffector.rotationWeight = w;
                    s.leftHandEffector.position = recoverLeft;
                    s.leftHandEffector.positionWeight = heavy ? w : 0f;
                    s.leftHandEffector.rotationWeight = heavy ? w : 0f;
                    foreach (var pair in savedBend) pair.Key.bendConstraint.weight = heavyElbowOut * w;
                    twist *= w;
                    break;
                }
            }
        }

        private Vector3 LightCarryPoint(BipedReferences r, Vector3 f, Vector3 u, Vector3 rt) =>
            r.rightUpperArm.position + (rt * lightCarryHand.x + u * lightCarryHand.y + f * lightCarryHand.z) * bodyScale;

        private Vector3 LightWindupPoint(BipedReferences r, Vector3 f, Vector3 u, Vector3 rt) =>
            r.rightUpperArm.position + (rt * lightWindup.x
                                        + u * (lightWindup.y + lightWindupCharged.y * charge)
                                        + f * (lightWindup.z - lightWindupCharged.x * charge)) * bodyScale;

        private Vector3 ChestCenter(BipedReferences r) => (r.leftUpperArm.position + r.rightUpperArm.position) * 0.5f;

        // Where the heavy object's pivot goes: its centre a set gap in front
        // of the chest from its near face (so a big object isn't pushed into
        // the body), and low enough — half its height lower still — not to
        // cover the view.
        private Vector3 HeavyCarryPoint(BipedReferences r, Vector3 f, Vector3 u, Vector3 rt)
        {
            Vector3 center = ChestCenter(r)
                + rt * (heavyCarryOffset.x * bodyScale)
                + u * (heavyCarryOffset.y * bodyScale - objectHalfHeight * 0.5f)
                + f * (heavyCarryOffset.z * bodyScale + objectHalfDepth);
            return PivotForCenter(center);
        }

        // The object is placed by its pivot; its centre sits objectCenterLocal
        // away (rotated with the object).
        private Vector3 PivotForCenter(Vector3 center) =>
            center - (transform.rotation * heavyRotationLocal) * Vector3.Scale(held != null ? held.transform.lossyScale : Vector3.one, objectCenterLocal);

        // Overhead, with its bottom at least Head Clearance above the top of
        // the head (head bone + about 0.25 m, scaled), so it isn't laid on
        // the head.
        private Vector3 HeavyWindupPoint(BipedReferences r, Vector3 f, Vector3 u, Vector3 rt)
        {
            Vector3 center = ChestCenter(r) + (rt * heavyWindupCenter.x + u * heavyWindupCenter.y + f * (heavyWindupCenter.z - heavyWindupCharged * charge)) * bodyScale;
            if (r.head != null)
            {
                float headTop = r.head.position.y + 0.25f * bodyScale;
                center.y = Mathf.Max(center.y, headTop + headClearance + objectHalfHeight);
            }
            return PivotForCenter(center);
        }

        private Vector3 HeavyReleasePoint(BipedReferences r, Vector3 f, Vector3 u) =>
            PivotForCenter(ChestCenter(r) + (viewTransform.forward * heavyRelease.x + u * heavyRelease.y) * bodyScale
                           + viewTransform.forward * objectHalfDepth);

        // Both hands on the heavy object's side grips (the object is placed
        // first), palms facing each other.
        private void HeavyHands(IKSolverFullBodyBiped s, BipedReferences r, Vector3 rt, Vector3 fingers)
        {
            Drive(s.rightHandEffector, held.transform.TransformPoint(rightGripLocal), 1f, r.rightUpperArm);
            Drive(s.leftHandEffector, held.transform.TransformPoint(leftGripLocal), 1f, r.leftUpperArm);
            Palm(s.rightHandEffector, true, -rt, fingers, 1f);
            Palm(s.leftHandEffector, false, rt, fingers, 1f);
            GuideElbow(s.rightArmChain, ref rightElbowGoal, "ObjectElbowGoal_R", r.rightUpperArm, s.rightHandEffector.position, rt, 1f);
            GuideElbow(s.leftArmChain, ref leftElbowGoal, "ObjectElbowGoal_L", r.leftUpperArm, s.leftHandEffector.position, rt, -1f);
        }

        // Elbows out to the side (and a little down) while holding a heavy
        // object in both hands — without a guide they followed the
        // animation and went into the body. The chain's own bend goal (the
        // aiming one) is put back by RestoreElbows.
        private Transform rightElbowGoal, leftElbowGoal;
        private readonly Dictionary<FBIKChain, (Transform goal, float weight)> savedBend = new Dictionary<FBIKChain, (Transform, float)>();

        private void GuideElbow(FBIKChain chain, ref Transform goal, string goalName, Transform shoulder, Vector3 hand, Vector3 rt, float side)
        {
            if (shoulder == null || heavyElbowOut <= 0f) return;
            if (goal == null)
            {
                goal = new GameObject(goalName).transform;
                goal.SetParent(transform, false);
            }
            if (!savedBend.ContainsKey(chain)) savedBend[chain] = (chain.bendConstraint.bendGoal, chain.bendConstraint.weight);
            goal.position = (shoulder.position + hand) * 0.5f + rt * (side * 0.3f * bodyScale) - Vector3.up * (0.1f * bodyScale);
            chain.bendConstraint.bendGoal = goal;
            chain.bendConstraint.weight = heavyElbowOut;
        }

        private void RestoreElbows()
        {
            foreach (var pair in savedBend)
            {
                pair.Key.bendConstraint.bendGoal = pair.Value.goal;
                pair.Key.bendConstraint.weight = pair.Value.weight;
            }
            savedBend.Clear();
        }

        // The hands' fingers (continuing the forearm) and back (facing out
        // from the body: hanging arms have the palms toward the thighs), in
        // each hand bone's own space, read on the animated pose.
        private void MeasureHands(BipedReferences r)
        {
            handsMeasured = true;
            if (r.rightHand != null && r.rightForearm != null)
            {
                fingersInRight = r.rightHand.InverseTransformDirection((r.rightHand.position - r.rightForearm.position).normalized);
                backInRight = r.rightHand.InverseTransformDirection(transform.right);
            }
            if (r.leftHand != null && r.leftForearm != null)
            {
                fingersInLeft = r.leftHand.InverseTransformDirection((r.leftHand.position - r.leftForearm.position).normalized);
                backInLeft = r.leftHand.InverseTransformDirection(-transform.right);
            }
        }

        // Turns a hand so its palm faces `palm` with the fingers as close to
        // `fingersHint` as that allows (weight `weight`).
        private void Palm(IKEffector effector, bool rightHand, Vector3 palm, Vector3 fingersHint, float weight)
        {
            Vector3 fingersIn = rightHand ? fingersInRight : fingersInLeft;
            Vector3 backIn = rightHand ? backInRight : backInLeft;
            if (fingersIn == Vector3.zero || backIn == Vector3.zero || palm.sqrMagnitude < 1e-6f) return;
            palm.Normalize();
            Vector3 fingers = Vector3.ProjectOnPlane(fingersHint, palm);
            if (fingers.sqrMagnitude < 1e-6f) fingers = Vector3.ProjectOnPlane(Vector3.up, palm);
            if (fingers.sqrMagnitude < 1e-6f) return;
            effector.rotation = Quaternion.LookRotation(fingers.normalized, -palm) * Quaternion.Inverse(Quaternion.LookRotation(fingersIn, backIn));
            effector.rotationWeight = weight;
        }

        // Hand effector toward a point, kept within the arm's reach of the
        // shoulder (crouch included) so it never drags the body.
        private void Drive(IKEffector effector, Vector3 position, float weight, Transform shoulder)
        {
            if (shoulder != null)
            {
                Vector3 from = shoulder.position + Vector3.down * crouchNow;
                Vector3 offset = position - from;
                float max = referenceArmLength * bodyScale * 0.97f;
                if (offset.magnitude > max) position = from + offset.normalized * max;
            }
            effector.position = position;
            effector.positionWeight = weight;
        }

        // Knees bend (feet pinned), hips bend, by `w` of the pickup posture.
        private void Posture(float w, Vector3 flatForward)
        {
            BipedReferences r = fullBodyIK.references;
            IKSolverFullBodyBiped s = fullBodyIK.solver;
            crouchNow = crouch * w;
            if (hipBend > 0f && r.spine != null && r.spine.Length > 0)
            {
                Quaternion perBone = Quaternion.AngleAxis(hipBend * w / r.spine.Length, Vector3.Cross(Vector3.up, flatForward));
                foreach (Transform bone in r.spine)
                    if (bone != null) bone.rotation = perBone * bone.rotation;
            }
            if (crouch <= 0f)
            {
                s.leftFootEffector.positionWeight = 0f;
                s.rightFootEffector.positionWeight = 0f;
                return;
            }
            s.bodyEffector.positionOffset += Vector3.down * crouchNow;
            s.leftFootEffector.position = leftFootPin;
            s.rightFootEffector.position = rightFootPin;
            s.leftFootEffector.positionWeight = w;
            s.rightFootEffector.positionWeight = w;
        }
    }
}
