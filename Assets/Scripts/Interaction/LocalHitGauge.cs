using RootMotion.Dynamics;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Interaction
{
    // R&D hit gauge (CombatSettings > Encaissement): punches and kicks fill
    // it instead of knocking down on physics alone.
    // - Not full: the blow staggers (the puppet tolerates a larger drift
    //   for Stagger Time), it shakes without falling.
    // - Past Dazed From: dazed (LocalPlayerLocomotion slows and sways).
    // - Full, or a Spartan kick: knocked out for Knock Out Time, then the
    //   usual get-up.
    // - Back up: empty, and Wake Protection seconds where blows don't count.
    // It drains after Drain Delay without a hit.
    //
    // On the victim's root; added on the first blow to anyone without it
    // (For). BehaviourPuppet's values are restored when it lets go. Zones,
    // reactions and the K.O. fall: LocalHitGauge.Reactions.cs.
    public partial class LocalHitGauge : MonoBehaviour
    {
        [Tooltip("Écrit dans la console les changements d'état (titube, sonné, K.O., réveil).")]
        [SerializeField] private bool debugLogs = true;

        private BehaviourPuppet puppet;
        private PlayerInput playerInput;
        private BehaviourPuppet.MuscleProps baseDefaults;
        private BehaviourPuppet.MusclePropsGroup[] baseGroups;
        private float baseGetUpDelay;
        private bool staggering;
        private float staggerUntil;
        private float lastHitTime = -10f;
        private float protectedUntil = -10f;
        private bool sawDown;
        private bool wasDazed;

        // 0..1. Read by the attacker's provisional display.
        public float Fill { get; private set; }
        public bool IsKnockedOut { get; private set; }

        // How dazed, 0..1 (0 below Dazed From, 1 at a full gauge); 0 while
        // knocked out (the ragdoll has the body then).
        public float Dazed
        {
            get
            {
                CombatSettings cfg = CombatSettings.Instance;
                if (IsKnockedOut || !cfg.useHitGauge || Fill <= cfg.dazedFrom) return 0f;
                return Mathf.Clamp01((Fill - cfg.dazedFrom) / Mathf.Max(0.01f, 1f - cfg.dazedFrom));
            }
        }

        public bool IsProtected => Time.time < protectedUntil;

        // Victim index (-1 = not a joined player, e.g. a dummy) and where:
        // GameFeel plays its "K.O." moment.
        public static event System.Action<int, Vector3> KnockedOut;

        // The gauge of whoever owns this ragdoll, added if missing.
        public static LocalHitGauge For(PuppetMaster target)
        {
            if (target == null) return null;
            LocalPlayerRagdollController owner = target.GetComponentInParent<LocalPlayerRagdollController>();
            GameObject root = owner != null ? owner.gameObject : target.transform.root.gameObject;
            LocalHitGauge gauge = root.GetComponent<LocalHitGauge>();
            return gauge != null ? gauge : root.AddComponent<LocalHitGauge>();
        }

        private void Awake()
        {
            puppet = GetComponentInChildren<BehaviourPuppet>(true);
            playerInput = GetComponent<PlayerInput>();
            if (puppet != null) baseGetUpDelay = puppet.getUpDelay;
            FindAnimator();
        }

        private void OnDisable()
        {
            EndStagger();
            if (puppet != null && IsKnockedOut) puppet.getUpDelay = baseGetUpDelay;
            RestoreAfterKnockOut();
            IsKnockedOut = false;
        }

        // A blow: amount before the zone multiplier, zone, force direction.
        // Call before applying the force. True if it knocks out. blocked: the
        // guard took it, the block clip plays instead.
        public bool TakeHit(float amount, bool forceKnockOut, HitZone zone, Vector3 direction, bool blocked = false)
        {
            CombatSettings cfg = CombatSettings.Instance;
            if (IsKnockedOut) return false;   // already down: the blow just pushes the ragdoll
            lastHitTime = Time.time;
            lastZone = zone;
            lastDirection = direction;
            if (!forceKnockOut && IsProtected)
            {
                Stagger(cfg);
                if (!blocked) PlayHitReaction(zone, direction, cfg);
                Log($"coup ({ZoneName(zone)}) encaissé sans remplir la jauge (réveil protégé), {Fill:P0}");
                return false;
            }
            Fill = Mathf.Min(1f, Fill + amount * ZoneDamage(zone, cfg));
            if (forceKnockOut || Fill >= 1f)
            {
                KnockOut(cfg);
                return true;
            }
            Stagger(cfg);
            if (!blocked) PlayHitReaction(zone, direction, cfg);
            bool dazed = Dazed > 0f;
            Log($"{(blocked ? "bloqué" : dazed ? "SONNÉ" : "titube")} — coup {ZoneName(zone)}, jauge {Fill:P0}");
            wasDazed = dazed;
            return false;
        }

        // BehaviourPuppet (UnPinMuscle, OnFixedUpdate): a hit removes
        // unpin / (collisionResistance x Collision Resistance) from the
        // muscle's pin, and a muscle at or under Pin Weight Threshold knocks
        // out once it drifts past knockOutDistance x sqrt(Knock Out Distance).
        // The player's props have both at 0, so any hit knocks out. While
        // staggering every group gets at least Stagger Resistance and Stagger
        // Drift.
        //
        // Arms and hands are ignored while staggering: they're loose and swing
        // after a hit, which says nothing about balance. Their override goes
        // first (GetProps takes the first match). The puppet's override array
        // is swapped, not edited, and put back afterwards.
        private void Stagger(CombatSettings cfg)
        {
            if (puppet == null || cfg.staggerTime <= 0f) return;
            if (!staggering)
            {
                baseDefaults = puppet.defaults;
                baseGroups = puppet.groupOverrides;
                BehaviourPuppet.MuscleProps steady = Steady(baseDefaults, cfg);
                puppet.defaults = steady;
                int n = baseGroups != null ? baseGroups.Length : 0;
                var groups = new BehaviourPuppet.MusclePropsGroup[n + 1];
                BehaviourPuppet.MuscleProps limbs = steady;
                limbs.knockOutDistance = 10000f;   // (100 m)²: never
                groups[0] = new BehaviourPuppet.MusclePropsGroup { name = "Stagger: arms", groups = new[] { Muscle.Group.Arm, Muscle.Group.Hand }, props = limbs };
                for (int i = 0; i < n; i++)
                {
                    groups[i + 1] = baseGroups[i];
                    groups[i + 1].props = Steady(baseGroups[i].props, cfg);
                }
                puppet.groupOverrides = groups;
                staggering = true;
                if (debugLogs)
                    Debug.Log($"[Encaissement] {name} : équilibre renforcé — résistance {baseDefaults.collisionResistance:0.##} → {puppet.defaults.collisionResistance:0.##}, " +
                              $"distance {baseDefaults.knockOutDistance:0.##} → {puppet.defaults.knockOutDistance:0.##}, bras et mains ignorés ({n} groupe(s) du prefab en plus)", this);
            }
            staggerUntil = Time.time + cfg.staggerTime;
        }

        private static BehaviourPuppet.MuscleProps Steady(BehaviourPuppet.MuscleProps p, CombatSettings cfg)
        {
            p.collisionResistance = Mathf.Max(p.collisionResistance, cfg.staggerResistance);
            p.knockOutDistance = Mathf.Max(p.knockOutDistance, cfg.staggerDrift * cfg.staggerDrift);
            return p;
        }

        // Restored only once the hit muscles are pinned again, or they'd
        // knock out at once with the prefab's zero thresholds.
        private bool PinsRecovered()
        {
            PuppetMaster pm = puppet != null ? puppet.puppetMaster : null;
            if (pm == null) return true;
            foreach (Muscle m in pm.muscles)
                if (m != null && !m.state.isDisconnected && m.state.pinWeightMlp < 0.95f) return false;
            return true;
        }

        private void EndStagger()
        {
            if (!staggering) return;
            staggering = false;
            if (puppet == null) return;
            puppet.defaults = baseDefaults;
            puppet.groupOverrides = baseGroups;
            // BehaviourPuppet knocks out within 3 s of a collision, and the
            // loose arms always drift a bit: closing that window means only
            // a new blow can knock the player down.
            if (!IsKnockedOut) puppet.SetHasCollided(false);
        }

        private void KnockOut(CombatSettings cfg)
        {
            EndStagger();
            Fill = 1f;
            IsKnockedOut = true;
            sawDown = false;
            if (puppet != null)
            {
                puppet.getUpDelay = cfg.knockOutTime;
                PrepareKnockOut(cfg);
                puppet.SetState(BehaviourPuppet.State.Unpinned);
            }
            Log($"K.O. ! (dernier coup : {ZoneName(lastZone)})");
            KnockedOut?.Invoke(playerInput != null && playerInput.enabled ? playerInput.playerIndex : -1,
                puppet != null && puppet.puppetMaster != null && puppet.puppetMaster.muscles.Length > 0 && puppet.puppetMaster.muscles[0].rigidbody != null
                    ? puppet.puppetMaster.muscles[0].rigidbody.position : transform.position);
        }

        private void Update()
        {
            CombatSettings cfg = CombatSettings.Instance;
            // Ends once Stagger Time is over and the pins are back (at most
            // 3 s later, so a body that can't recover isn't held up forever).
            if (staggering && Time.time >= staggerUntil && (PinsRecovered() || Time.time >= staggerUntil + 3f)) EndStagger();
            UpdateAnimations(cfg);

            if (IsKnockedOut)
            {
                // Back on its feet (Unpinned → GetUp → Puppet): awake.
                if (puppet == null) { WakeUp(cfg); return; }
                if (puppet.state != BehaviourPuppet.State.Puppet) sawDown = true;
                else if (sawDown) WakeUp(cfg);
                return;
            }

            if (Fill > 0f && Time.time - lastHitTime >= cfg.drainDelay)
            {
                Fill = Mathf.Max(0f, Fill - cfg.drainRate * Time.deltaTime);
                bool dazed = Dazed > 0f;
                if (wasDazed && !dazed) Log("n'est plus sonné");
                wasDazed = dazed;
            }
        }

        // ---- Diagnostic: a fall the gauge didn't decide ----
        // The muscle closest to the knock-out condition each step, logged
        // if the puppet falls without a K.O.
        private Muscle worstMuscle;
        private float worstOffset, worstThreshold, worstPin, worstRatio;
        private bool wasStanding = true;

        private void FixedUpdate()
        {
            if (!debugLogs || puppet == null || puppet.puppetMaster == null) return;
            PuppetMaster pm = puppet.puppetMaster;

            bool standing = puppet.state != BehaviourPuppet.State.Unpinned;
            if (wasStanding && !standing && !IsKnockedOut)
                Debug.LogWarning($"[Encaissement] {name} : TOMBÉ SANS K.O. de la jauge — jauge {Fill:P0}, titube {(staggering ? "actif" : "fini")}, " +
                                 $"dernier coup il y a {Time.time - lastHitTime:0.00} s. Muscle le plus près de la chute au pas précédent : " +
                                 $"{(worstMuscle != null ? $"{worstMuscle.name} ({worstMuscle.props.group})" : "—")} " +
                                 $"écart {worstOffset:0.00} m pour un seuil de {worstThreshold:0.00} m, pin {worstPin:0.00} (seuil {puppet.pinWeightThreshold:0.##}), " +
                                 $"{(worstRatio > 1f ? "au-delà du seuil" : "sous le seuil (autre cause ?)")}", this);
            wasStanding = standing;
            worstMuscle = null;
            // BehaviourPuppet only knocks out within 3 s of a collision: no
            // need to scan the muscles otherwise.
            if (!standing || Time.time - lastHitTime > 3.5f) return;

            worstRatio = 0f;
            float global = Mathf.Sqrt(puppet.knockOutDistance);
            foreach (Muscle m in pm.muscles)
            {
                if (m == null || m.state.isDisconnected) continue;
                float pinW = m.state.pinWeightMlp * m.props.pinWeight * pm.pinWeight;
                if (pinW > puppet.pinWeightThreshold) continue;   // can't knock out
                float limitSqr = PropsOf(m.props.group).knockOutDistance * global;
                float offsetSqr = puppet.unpinnedMuscleKnockout ? m.positionOffset.sqrMagnitude : m.positionOffset.sqrMagnitude * m.props.pinWeight * pm.pinWeight;
                float ratio = limitSqr > 0f ? offsetSqr / limitSqr : (offsetSqr > 0f ? float.PositiveInfinity : 0f);
                if (worstMuscle != null && ratio <= worstRatio) continue;
                worstMuscle = m;
                worstRatio = ratio;
                worstOffset = Mathf.Sqrt(offsetSqr);
                worstThreshold = Mathf.Sqrt(Mathf.Max(0f, limitSqr));
                worstPin = pinW;
            }
        }

        // Same lookup as BehaviourPuppet.GetProps (private).
        private BehaviourPuppet.MuscleProps PropsOf(Muscle.Group group)
        {
            if (puppet.groupOverrides != null)
                foreach (BehaviourPuppet.MusclePropsGroup g in puppet.groupOverrides)
                    if (g.groups != null)
                        foreach (Muscle.Group g2 in g.groups)
                            if (g2 == group) return g.props;
            return puppet.defaults;
        }

        private void WakeUp(CombatSettings cfg)
        {
            IsKnockedOut = false;
            Fill = 0f;
            wasDazed = false;
            if (puppet != null) puppet.getUpDelay = baseGetUpDelay;
            RestoreAfterKnockOut();
            protectedUntil = Time.time + cfg.wakeProtection;
            Log($"réveil (protégé {cfg.wakeProtection:0.#} s)");
        }

        private void Log(string message)
        {
            if (debugLogs) Debug.Log($"[Encaissement] {name} : {message}", this);
        }
    }
}
