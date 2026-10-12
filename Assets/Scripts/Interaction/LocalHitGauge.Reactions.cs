using System.Collections;
using RootMotion.Dynamics;
using UnityEngine;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Interaction
{
    // Where a blow lands, from the muscle hit (PuppetMaster muscle group).
    public enum HitZone { Head, Body, Legs }

    // The visible side of the hit gauge (CombatSettings > Coups localisés,
    // Encaissement):
    // - zones: a blow fills the gauge by Head / Body / Leg Damage;
    // - blows taken: a GetHit clip from the side the blow came from, light,
    //   or heavy for the head or someone dazed, on the "R&D Coup reçu"
    //   layer — the PuppetMaster physics of the muscle hit stays on top;
    // - dazed: the Stunned loop on the "R&D Sonné" layers, upper body by the
    //   amount dazed, legs only while standing still (so walking keeps its
    //   legs);
    // - knock-out: the body goes limp (Knock Out Limp) and is pushed the way
    //   the last blow went, differently by zone — head: falls backwards;
    //   body: hips back and shoulders forward, it folds; legs: the legs go
    //   out from under it.
    // Layers built by Tools > Pool > R&D > Préparer le joueur de R&D;
    // without them, only the animations are skipped.
    public partial class LocalHitGauge
    {
        public const string DazedUpperLayer = "R&D Sonné (haut)";
        public const string DazedLegsLayer = "R&D Sonné (jambes)";
        public const string HitLayer = "R&D Coup reçu";
        private const string EmptyState = "Empty", StunnedState = "Stunned";

        private Animator animator;
        private CharacterController characterController;
        private int dazedUpperIndex = -2, dazedLegsIndex = -2, hitIndex = -2;
        private bool warnedNoLayers;
        private float dazedWeight;
        private bool stunnedPlaying;
        private bool hitPlaying;
        private bool hitLayerEmpty = true;
        private int hitStateHash;
        private float hitWeight, hitElapsed;

        private HitZone lastZone = HitZone.Body;
        private Vector3 lastDirection;
        private float baseUnpinnedMuscleWeight = -1f;

        public static HitZone ZoneOf(Muscle.Group group) => group switch
        {
            Muscle.Group.Head => HitZone.Head,
            Muscle.Group.Leg or Muscle.Group.Foot => HitZone.Legs,
            _ => HitZone.Body,
        };

        private static float ZoneDamage(HitZone zone, CombatSettings cfg) => zone switch
        {
            HitZone.Head => cfg.headDamage,
            HitZone.Legs => cfg.legDamage,
            _ => cfg.bodyDamage,
        };

        private static string ZoneName(HitZone zone) => zone switch
        {
            HitZone.Head => "tête",
            HitZone.Legs => "jambes",
            _ => "corps",
        };

        private void FindAnimator()
        {
            LocalCharacterAnimationController animControl = GetComponent<LocalCharacterAnimationController>();
            animator = animControl != null && animControl.Animator != null ? animControl.Animator : GetComponentInChildren<Animator>(true);
            characterController = GetComponent<CharacterController>();
        }

        // Looked up once, when the Animator is ready; -1 = missing.
        private bool LayersReady()
        {
            if (animator == null || !animator.isActiveAndEnabled) return false;
            if (hitIndex == -2)
            {
                dazedUpperIndex = animator.GetLayerIndex(DazedUpperLayer);
                dazedLegsIndex = animator.GetLayerIndex(DazedLegsLayer);
                hitIndex = animator.GetLayerIndex(HitLayer);
                if ((dazedUpperIndex < 0 || hitIndex < 0) && !warnedNoLayers)
                {
                    warnedNoLayers = true;
                    Debug.LogWarning($"[Encaissement] {name} : calques « {DazedUpperLayer} » / « {HitLayer} » absents du contrôleur d'animation — " +
                                     "lance Tools > Pool > R&D > Préparer le joueur de R&D. Pas d'animation de sonné ni de coup reçu en attendant.", this);
                }
            }
            return true;
        }

        // Called by TakeHit for a blow that doesn't knock out.
        private void PlayHitReaction(HitZone zone, Vector3 direction, CombatSettings cfg)
        {
            if (!cfg.hitReactions || !LayersReady() || hitIndex < 0) return;
            Vector3 local = transform.InverseTransformDirection(direction);
            // The blow pushes the body: pushed backwards = hit from the front.
            string side;
            if (Mathf.Abs(local.z) >= Mathf.Abs(local.x)) side = local.z <= 0f ? "Front" : "Back";
            else side = (local.x > 0f) != cfg.hitReactionSwapSides ? "Left" : "Right";
            bool heavy = zone == HitZone.Head || Dazed > 0f;
            string state = $"Hit{(heavy ? "Heavy" : "Light")}{side}";
            hitStateHash = Animator.StringToHash(state);
            animator.CrossFadeInFixedTime(hitStateHash, cfg.hitReactionFade, hitIndex, 0f);
            hitPlaying = true;
            hitLayerEmpty = false;
            hitElapsed = 0f;
            Log($"réaction {state} ({ZoneName(zone)})");
        }

        // Guard broken (LocalUnarmedMelee.BreakGuard): the heavy reaction
        // from the blow's side, so the break reads — the hit gauge itself
        // isn't touched.
        public void PlayGuardBreakReaction(Vector3 direction)
        {
            if (IsKnockedOut) return;
            PlayHitReaction(HitZone.Head, direction, CombatSettings.Instance);
        }

        // The guard goes up (LocalUnarmedMelee): a hit reaction still
        // playing would hide the block pose — its layer is above the combat
        // layer — so it fades out now.
        public void CancelHitReaction() => hitPlaying = false;

        // Every frame (Update): layer weights for the blow taken and the
        // stunned sway.
        private void UpdateAnimations(CombatSettings cfg)
        {
            if (!LayersReady()) return;
            float dt = Time.deltaTime;

            // Nothing to do while both layers rest (the usual case): no
            // Animator calls at all.
            if (hitIndex >= 0 && (hitPlaying || hitWeight > 0f || !hitLayerEmpty))
            {
                if (hitPlaying)
                {
                    hitElapsed += dt;
                    AnimatorStateInfo info = animator.IsInTransition(hitIndex) ? animator.GetNextAnimatorStateInfo(hitIndex) : animator.GetCurrentAnimatorStateInfo(hitIndex);
                    // Out near the end of the clip (they're set to loop), or
                    // if the state never started.
                    if ((info.shortNameHash == hitStateHash && info.normalizedTime >= 0.85f) || hitElapsed > 2.5f || IsKnockedOut) hitPlaying = false;
                }
                float fade = Mathf.Max(0.01f, cfg.hitReactionFade);
                hitWeight = IsKnockedOut ? 0f : Mathf.MoveTowards(hitWeight, hitPlaying ? 1f : 0f, dt / fade);
                animator.SetLayerWeight(hitIndex, hitWeight);
                // Back to Empty once, when the reaction has faded out (it was
                // sent every frame before).
                if (!hitPlaying && hitWeight <= 0f)
                {
                    animator.CrossFadeInFixedTime(EmptyState, 0f, hitIndex);
                    hitLayerEmpty = true;
                }
            }

            float target = cfg.dazedAnimation ? Dazed : 0f;
            if (dazedUpperIndex >= 0 && (target > 0f || dazedWeight > 0f || stunnedPlaying))
            {
                dazedWeight = IsKnockedOut ? 0f : Mathf.MoveTowards(dazedWeight, target, dt * 2f);
                bool want = dazedWeight > 0f;
                if (want != stunnedPlaying)
                {
                    stunnedPlaying = want;
                    string state = want ? StunnedState : EmptyState;
                    animator.CrossFadeInFixedTime(state, want ? 0.2f : 0f, dazedUpperIndex);
                    if (dazedLegsIndex >= 0) animator.CrossFadeInFixedTime(state, want ? 0.2f : 0f, dazedLegsIndex);
                }
                animator.SetLayerWeight(dazedUpperIndex, dazedWeight);
                if (dazedLegsIndex >= 0)
                {
                    // Legs only while standing still: walking keeps its own.
                    float speed = characterController != null && characterController.enabled
                        ? new Vector2(characterController.velocity.x, characterController.velocity.z).magnitude : 0f;
                    animator.SetLayerWeight(dazedLegsIndex, dazedWeight * (1f - Mathf.Clamp01(speed / 1.5f)));
                }
            }
        }

        // Knock-out: limp, then pushed along the last blow, by zone. Called
        // before SetState(Unpinned) so the limp muscles apply right away.
        private void PrepareKnockOut(CombatSettings cfg)
        {
            if (puppet == null) return;
            if (baseUnpinnedMuscleWeight < 0f) baseUnpinnedMuscleWeight = puppet.unpinnedMuscleWeightMlp;
            puppet.unpinnedMuscleWeightMlp = cfg.knockOutLimp;
            hitPlaying = false;
            if (cfg.knockOutPush > 0f) StartCoroutine(KnockOutFall(lastZone, lastDirection, cfg.knockOutPush));
        }

        private void RestoreAfterKnockOut()
        {
            if (puppet != null && baseUnpinnedMuscleWeight >= 0f) puppet.unpinnedMuscleWeightMlp = baseUnpinnedMuscleWeight;
            baseUnpinnedMuscleWeight = -1f;
        }

        // One physics step after the unpin, like the Spartan kick's launch
        // (BehaviourPuppet clamps the velocities as it unpins).
        private IEnumerator KnockOutFall(HitZone zone, Vector3 direction, float push)
        {
            yield return new WaitForFixedUpdate();
            PuppetMaster pm = puppet != null ? puppet.puppetMaster : null;
            if (pm == null) yield break;
            Vector3 dir = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = -transform.forward;
            dir.Normalize();

            foreach (Muscle m in pm.muscles)
            {
                if (m == null || m.rigidbody == null || m.state.isDisconnected) continue;
                bool top = m.props.group == Muscle.Group.Head || m.props.group == Muscle.Group.Spine
                           || m.props.group == Muscle.Group.Arm || m.props.group == Muscle.Group.Hand;
                bool legs = m.props.group == Muscle.Group.Leg || m.props.group == Muscle.Group.Foot;
                Vector3 v = zone switch
                {
                    // Lights out: the top goes back with the blow, the feet stay.
                    HitZone.Head => top ? dir * push : legs ? Vector3.zero : dir * (push * 0.5f),
                    // Doubled over: hips pushed back, shoulders forward and down.
                    HitZone.Body => top ? (-dir * 0.4f + Vector3.down * 0.4f) * push : dir * (push * 0.7f),
                    // Swept: the legs go with the blow, the top the other way.
                    _ => legs ? (dir * 1.2f + Vector3.up * 0.3f) * push : top ? -dir * (push * 0.4f) : Vector3.zero,
                };
                if (v != Vector3.zero) m.rigidbody.AddForce(v, ForceMode.VelocityChange);
            }
            Log($"chute « {ZoneName(zone)} » (poussée {push:0.#} m/s)");
        }
    }
}
