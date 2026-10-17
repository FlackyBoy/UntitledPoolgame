using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Interaction
{
    // R&D animated punches (CombatSettings Use Animations, with Boxing
    // Punches): guard and punches are PROTOFACTOR clips on an upper-body
    // layer (set up by Tools > Pool > R&D > Préparer le joueur de R&D,
    // weight 0 unless driven here). Mocap gives the weight shift the IK
    // version couldn't. Hits are swept along the real hand bone during a
    // window of the clip; the rest is shared with the IK punches.
    public partial class LocalUnarmedMelee
    {
        private const string EmptyState = "Empty", GuardState = "Guard", PunchLeftState = "PunchL", PunchRightState = "PunchR";
        private const string PunchSpeedParameter = "PunchSpeed";

        private int combatLayer = -2;          // -2 = not looked up yet, -1 = missing
        private bool hasPunchSpeed;
        private bool animPunching;
        private float animElapsed;
        private int animStateHash;
        private Vector3 animLastHand;
        private bool animHasLastHand;
        private float animPeakSpeed, animPeakTime;
        // State the combat layer was last sent to while not punching (Empty,
        // Guard, Block, Blocked); null = to be decided again.
        private string layerState = EmptyState;
        private bool LayerBusy => layerState != EmptyState;
        private bool warnedNoLayer;

        // The clips keep their bladed stance, so a punch can leave at an angle
        // to the view. Measured on each punch and corrected by turning the
        // spine (Punch Aim Left/Right).
        private bool aimMeasuring;
        private Vector3 aimFrom, aimFarthest;
        private float aimFarthestDistance;
        private float aimWeight, aimAngle;
        // The view's pitch when the hit window opened (Debug Logs: compared
        // with the fist's own slope).
        private float aimViewPitch;

        // Animated punches in use: ticked in CombatSettings, and the layer
        // is there (otherwise the IK punches stay).
        private bool AnimatedPunches
        {
            get
            {
                if (!Boxing || !CombatSettings.Instance.useAnimations || bodyAnimator == null) return false;
                if (combatLayer == -2)
                {
                    combatLayer = bodyAnimator.GetLayerIndex(CombatSettings.Instance.animationLayer);
                    foreach (AnimatorControllerParameter p in bodyAnimator.parameters)
                        if (p.name == PunchSpeedParameter && p.type == AnimatorControllerParameterType.Float) hasPunchSpeed = true;
                }
                if (combatLayer < 0 && !warnedNoLayer)
                {
                    warnedNoLayer = true;
                    Debug.LogWarning($"[UnarmedMelee] {name} : calque « {CombatSettings.Instance.animationLayer} » absent du contrôleur d'animation — " +
                                     "lance Tools > Pool > R&D > Préparer le joueur de R&D. Coups procéduraux en attendant.", this);
                }
                return combatLayer >= 0;
            }
        }

        private Transform HandBone(bool right) =>
            bodyAnimator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

        private string PunchState(bool right) => right != CombatSettings.Instance.swapHands ? PunchRightState : PunchLeftState;

        private void StartAnimatedPunch(bool right)
        {
            CombatSettings cfg = CombatSettings.Instance;
            rightHand = right;
            move = Move.Punch;
            charged = false;
            charge = 0f;
            hitSomething = false;
            bufferedPunch = false;
            hitThisBlow.Clear();
            animPunching = true;
            animElapsed = 0f;
            animHasLastHand = false;
            animPeakSpeed = 0f;
            animPeakTime = 0f;
            aimMeasuring = false;
            aimFarthestDistance = 0f;
            aimAngle = right ? cfg.punchAimRight : cfg.punchAimLeft;
            StartCounterIfAny();
            SpendStamina(Move.Punch);

            string state = PunchState(right);
            layerState = state;
            animStateHash = Animator.StringToHash(state);
            if (hasPunchSpeed)
                bodyAnimator.SetFloat(PunchSpeedParameter, cfg.animationSpeed * (counterPunch ? cfg.counterSpeed : 1f) * (tiredPunch ? cfg.tiredSpeed : 1f));
            bodyAnimator.CrossFadeInFixedTime(state, cfg.animationFade, combatLayer, 0f);
            bodyAnimator.SetLayerWeight(combatLayer, 1f);

            if (locomotion != null)
                locomotion.KickView((right ? -1f : 1f) * cfg.throwViewRoll, cfg.throwViewPush, cfg.throwViewTime);
            if (debugLogs) Debug.Log($"[UnarmedMelee] {name}: animated punch ({(right ? "right" : "left")}) — {state}", this);
        }

        // Every frame while animated punches are in use: the layer's weight
        // and state, the punch's hit window, chaining, the way back to guard.
        private void UpdateAnimated(float dt)
        {
            CombatSettings cfg = CombatSettings.Instance;

            if (!animPunching)
            {
                // Blocking: block clip (or the blocked reaction right after a
                // blow); guard up after punching: guard clip; else nothing.
                string wanted = IsBlocking ? (Time.time < blockedReactUntil ? BlockedState : BlockState)
                    : guardWeight > 0f ? GuardState : EmptyState;
                if (wanted != layerState)
                {
                    bodyAnimator.CrossFadeInFixedTime(wanted, wanted == EmptyState ? 0f : cfg.animationFade, combatLayer);
                    layerState = wanted;
                }
                bodyAnimator.SetLayerWeight(combatLayer, IsBlocking ? 1f : guardWeight);
                return;
            }

            if (IsDown() || handController.HeldObject != null) { EndAnimatedPunch(false); return; }

            bodyAnimator.SetLayerWeight(combatLayer, 1f);
            animElapsed += dt;
            AnimatorStateInfo state = bodyAnimator.IsInTransition(combatLayer)
                ? bodyAnimator.GetNextAnimatorStateInfo(combatLayer)
                : bodyAnimator.GetCurrentAnimatorStateInfo(combatLayer);
            if (state.shortNameHash != animStateHash)
            {
                // The crossfade starts next frame; give up if it never does.
                if (animElapsed > 0.5f) EndAnimatedPunch(false);
                return;
            }
            float t = state.normalizedTime;

            // Hits: along the real hand, inside the window.
            Transform hand = HandBone(rightHand);
            if (hand != null)
            {
                Vector3 position = hand.position;
                if (animHasLastHand)
                {
                    float speed = (position - animLastHand).magnitude / Mathf.Max(1e-4f, dt);
                    if (speed > animPeakSpeed) { animPeakSpeed = speed; animPeakTime = t; }
                    if (t >= cfg.hitWindowStart && t <= cfg.hitWindowEnd)
                    {
                        DetectHits(animLastHand, position, FlatForward());
                        MeasureAim(position);
                    }
                }
                animLastHand = position;
                animHasLastHand = true;
            }

            // The other hand chains once the punch is far enough along.
            InputAction other = rightHand ? punchAction : punchRightAction;
            if (other.WasPressedThisFrame()) bufferedPunch = true;
            if (bufferedPunch && t >= cfg.chainFrom && CanStart(Move.Punch))
            {
                LogAnimatedPunch();
                StartAnimatedPunch(!rightHand);
                return;
            }

            if (t >= cfg.returnAt) EndAnimatedPunch(true);
        }

        private void EndAnimatedPunch(bool finished)
        {
            if (!animPunching) return;
            if (finished) LogAnimatedPunch();
            animPunching = false;
            bufferedPunch = false;
            cooldownLeft = cooldown;
            guardUntil = Time.time + CombatSettings.Instance.guardTime;
            layerState = null;   // UpdateAnimated fades to the guard (or to nothing)
            if (!finished && combatLayer >= 0)
            {
                bodyAnimator.CrossFadeInFixedTime(EmptyState, 0f, combatLayer);
                bodyAnimator.SetLayerWeight(combatLayer, 0f);
                layerState = EmptyState;
            }
        }

        // Debug Logs: when the hand moved fastest — where the hit window
        // belongs — and whether it touched anything.
        private void LogAnimatedPunch()
        {
            if (!debugLogs) return;
            CombatSettings cfg = CombatSettings.Instance;
            string aim = "";
            Vector3 travel = Vector3.ProjectOnPlane(aimFarthest - aimFrom, Vector3.up);
            if (aimMeasuring && travel.sqrMagnitude > 0.0025f)
            {
                // Positive = the fist went to the right of where the player looks.
                float error = Vector3.SignedAngle(FlatForward(), travel, Vector3.up);
                float current = rightHand ? cfg.punchAimRight : cfg.punchAimLeft;
                aim = $" — direction {error:+0;-0}° from the view ({(Mathf.Abs(error) < 5f ? "OK" : $"set Punch Aim {(rightHand ? "Right" : "Left")} to {Mathf.Clamp(current - error, -90f, 90f):0}")})";

                // Hit where you look: the fist's slope (positive = down)
                // against the view's, with Punch Pitch Follow as it is.
                Vector3 path = aimFarthest - aimFrom;
                float fistPitch = -Mathf.Atan2(path.y, travel.magnitude) * Mathf.Rad2Deg;
                aim += $"; slope {fistPitch:+0;-0}° vs view {aimViewPitch:+0;-0}° (Punch Pitch Follow {cfg.punchPitchFollow:F2}, {SlopeAdvice(fistPitch, aimViewPitch)})";
            }
            Debug.Log($"[UnarmedMelee] {name}: {(rightHand ? "right" : "left")} punch — hand fastest at {animPeakTime:F2} of the clip " +
                      $"({animPeakSpeed:F1} m/s), window {cfg.hitWindowStart:F2}–{cfg.hitWindowEnd:F2}" +
                      (hitSomething ? ", hit" : ", missed") + aim, this);
        }

        private static string SlopeAdvice(float fistPitch, float viewPitch)
        {
            if (Mathf.Abs(fistPitch - viewPitch) < 8f) return "OK";
            // Looking level: the follow does nothing, the gap is the clip's own.
            if (Mathf.Abs(viewPitch) < 5f) return "the clip itself slopes — nothing to set";
            bool tooLevel = Mathf.Abs(fistPitch) < Mathf.Abs(viewPitch) && Mathf.Sign(fistPitch) == Mathf.Sign(viewPitch);
            return tooLevel ? "fist stays too level: raise it" : "fist overshoots: lower it";
        }

        // From where the hand enters the hit window to where it gets
        // farthest from its shoulder: the way the fist actually goes.
        private void MeasureAim(Vector3 hand)
        {
            if (!aimMeasuring) { aimMeasuring = true; aimFrom = hand; aimFarthest = hand; aimViewPitch = ViewPitch(CombatSettings.Instance); }
            Transform shoulder = bodyAnimator.GetBoneTransform(rightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            float d = shoulder != null ? Vector3.Distance(shoulder.position, hand) : Vector3.Distance(aimFrom, hand);
            if (d > aimFarthestDistance) { aimFarthestDistance = d; aimFarthest = hand; }
        }

        // During a punch, eased in and out:
        //   - the spine turns by Punch Aim (the clip's bladed stance);
        //   - the clips strike at a fixed height, so the fist is tilted toward
        //     the view's pitch (Punch Pitch Follow), partly through the spine
        //     (Punch Pitch Spine Share), the rest at the shoulder.
        // Left/right needs nothing, the body already turns with the view.
        // Runs before Look At IK (AimBeforeLook), or before FBBIK without it.
        private void ApplyPunchAim(RootMotion.BipedReferences r)
        {
            punchAimFrame = Time.frameCount;
            aimWeight = Mathf.MoveTowards(aimWeight, animPunching ? 1f : 0f, Time.deltaTime / 0.08f);
            if (aimWeight <= 0f || r.spine == null || r.spine.Length == 0) return;

            float yaw = aimAngle * aimWeight;
            if (Mathf.Abs(yaw) >= 0.01f)
            {
                Quaternion perBone = Quaternion.AngleAxis(yaw / r.spine.Length, Vector3.up);
                foreach (Transform bone in r.spine)
                    if (bone != null) bone.rotation = perBone * bone.rotation;
            }

            CombatSettings cfg = CombatSettings.Instance;
            float pitch = ViewPitch(cfg) * cfg.punchPitchFollow * aimWeight;
            if (Mathf.Abs(pitch) < 0.01f) return;
            // Positive = down, as the view's pitch: about the body's right axis.
            Vector3 right = Vector3.Cross(Vector3.up, FlatForward());
            float spinePitch = pitch * cfg.punchPitchSpineShare;
            Quaternion spineStep = Quaternion.AngleAxis(spinePitch / r.spine.Length, right);
            foreach (Transform bone in r.spine)
                if (bone != null) bone.rotation = spineStep * bone.rotation;
            Transform upperArm = rightHand ? r.rightUpperArm : r.leftUpperArm;
            if (upperArm != null)
                upperArm.rotation = Quaternion.AngleAxis(pitch - spinePitch, right) * upperArm.rotation;
        }

        // The head keeps following the view during the guard and punches:
        //   - the clips turn the torso side-on, and Look At IK's Clamp Weight
        //     Head then all but cancels it; it's eased to Combat Head Clamp
        //     while the combat layer is in use;
        //   - the punch's spine turn runs in Look At IK's OnPreUpdate, before
        //     the head is aimed, so it doesn't carry the head away.
        private RootMotion.FinalIK.LookAtIK lookAtIK;
        private float lookAtClampRest = -1f;
        private float lookAtCombatWeight;
        private int punchAimFrame = -1;

        private void HookLookAt(bool on)
        {
            if (lookAtIK == null) lookAtIK = GetComponentInChildren<RootMotion.FinalIK.LookAtIK>(true);
            if (lookAtIK == null) return;
            lookAtIK.solver.OnPreUpdate -= AimBeforeLook;
            if (on)
            {
                lookAtIK.solver.OnPreUpdate += AimBeforeLook;
                return;
            }
            // Never leave the prefab's clamp changed behind.
            if (lookAtClampRest >= 0f) lookAtIK.solver.clampWeightHead = lookAtClampRest;
            lookAtCombatWeight = 0f;
        }

        private void AimBeforeLook()
        {
            if (fullBodyIK != null && AnimatedPunches) ApplyPunchAim(fullBodyIK.references);
        }

        // Head clamp eased toward Combat Head Clamp while the combat layer is
        // in use, back to the prefab's value otherwise.
        private void UpdateLookAtClamp()
        {
            if (lookAtIK == null) return;
            if (lookAtClampRest < 0f) lookAtClampRest = lookAtIK.solver.clampWeightHead;
            CombatSettings cfg = CombatSettings.Instance;
            bool combatPose = (animPunching || LayerBusy) && !IsDown();
            lookAtCombatWeight = Mathf.MoveTowards(lookAtCombatWeight, combatPose ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, cfg.animationFade + 0.05f));
            lookAtIK.solver.clampWeightHead = Mathf.Lerp(lookAtClampRest, cfg.combatHeadClamp, lookAtCombatWeight);
        }

        // The view's pitch in degrees, positive looking down, within the
        // limits of the punch's look-follow.
        private float ViewPitch(CombatSettings cfg)
        {
            float pitch = -Mathf.Asin(Mathf.Clamp(viewTransform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            return Mathf.Clamp(pitch, -cfg.punchPitchMaxUp, cfg.punchPitchMaxDown);
        }

        // Back to the IK punches, or disabled: the layer lets go.
        private void ReleaseAnimatedLayer()
        {
            animPunching = false;
            layerState = EmptyState;
            if (bodyAnimator != null && combatLayer >= 0)
            {
                bodyAnimator.CrossFadeInFixedTime(EmptyState, 0f, combatLayer);
                bodyAnimator.SetLayerWeight(combatLayer, 0f);
            }
        }
    }
}
