using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Interaction
{
    // R&D guard (CombatSettings > Garde), boxing punches only: hold Block
    // (left Ctrl / LT).
    // - Held: no punch or kick, slower, no sprint; IdleBlockHigh clip.
    // - Blows from the front to the head or body are blocked: BlockedHigh
    //   clip, reduced gauge damage and force. Legs, sides and back aren't
    //   covered.
    // - Each blocked blow costs block meter; empty, or a Spartan kick, breaks
    //   the guard for Guard Break Time.
    // - Counter: a punch within Counter Window of a block is faster and harder.
    // The attacker calls TryBlock from TryHit.
    public partial class LocalUnarmedMelee
    {
        [Tooltip("Action de la garde (Ctrl gauche / LT, maintenir), avec les poings de boxe.")]
        [SerializeField] private string blockActionName = "Block";

        private const string BlockState = "Block", BlockedState = "Blocked";

        private InputAction blockAction;
        private float blockMeter = 1f;
        private float lastBlockedTime = -10f;
        private float guardBrokenUntil = -10f;
        private float counterUntil = -10f;
        private float blockedReactUntil = -10f;
        private bool counterPunch;       // the current punch is a counter

        public bool IsBlocking { get; private set; }
        public float BlockMeter => blockMeter;
        public bool IsGuardBroken => Time.time < guardBrokenUntil;

        // The player whose guard broke (-1 = not a joined player) and where:
        // GameFeel plays its "garde brisée" moment.
        public static event System.Action<int, Vector3> GuardBroken;

        private LocalHitGauge ownGauge;
        private float blockCheckAt = -1f;
        private bool wasRefused;

        private void FindBlockAction(InputActionMap map) => blockAction = map.FindAction(blockActionName);

        // Why the guard can't go up right now (Debug Logs).
        private string BlockRefusal()
        {
            if (IsDown()) return "à terre";
            if (IsGuardBroken) return $"garde brisée ({guardBrokenUntil - Time.time:0.0} s)";
            if (animPunching || phase != Phase.Idle) return "coup en cours (la garde se lève à la fin)";
            if (handController.HeldObject != null) return "objet en main";
            if (handController.CueBusy || handController.HandsMoving) return "queue en main ou mains occupées";
            if (aimController != null && (aimController.IsAiming || aimController.IsPlacementViewActive)) return "en visée";
            if (cueMelee != null && cueMelee.IsSwinging) return "coup de queue";
            return "?";
        }

        // 0.3 s after raising the guard: logs what hides the block pose, if so.
        private void DiagnoseBlockPose()
        {
            if (!debugLogs) return;
            if (!AnimatedPunches)
            {
                Debug.Log($"[UnarmedMelee] {name}: guard up — IK guard (animated punches off or layer missing)", this);
                return;
            }
            AnimatorStateInfo info = bodyAnimator.IsInTransition(combatLayer) ? bodyAnimator.GetNextAnimatorStateInfo(combatLayer) : bodyAnimator.GetCurrentAnimatorStateInfo(combatLayer);
            bool blockState = info.shortNameHash == Animator.StringToHash(BlockState) || info.shortNameHash == Animator.StringToHash(BlockedState);
            float weight = bodyAnimator.GetLayerWeight(combatLayer);
            int hitLayer = bodyAnimator.GetLayerIndex(LocalHitGauge.HitLayer);
            float hitWeight = hitLayer >= 0 ? bodyAnimator.GetLayerWeight(hitLayer) : 0f;
            string stateName = info.shortNameHash == Animator.StringToHash(GuardState) ? GuardState
                : info.shortNameHash == Animator.StringToHash(EmptyState) ? EmptyState
                : info.shortNameHash == Animator.StringToHash(PunchLeftState) ? PunchLeftState
                : info.shortNameHash == Animator.StringToHash(PunchRightState) ? PunchRightState
                : blockState ? "Block/Blocked" : $"inconnu ({info.shortNameHash})";
            bool ok = blockState && weight > 0.99f && hitWeight < 0.01f;
            string message = $"[UnarmedMelee] {name}: guard pose {(ok ? "OK" : "NOT SHOWING")} — combat layer state {stateName}, weight {weight:0.00}, " +
                             $"layerState {layerState ?? "null"}, hit reaction layer weight {hitWeight:0.00}";
            if (ok) Debug.Log(message, this);
            else Debug.LogWarning(message, this);
        }

        // Every frame, before anything starts: is the guard held?
        private void UpdateBlock(float dt)
        {
            CombatSettings cfg = CombatSettings.Instance;
            bool want = Boxing && cfg.blockEnabled && blockAction != null && blockAction.IsPressed();
            bool allowed = !IsGuardBroken && phase == Phase.Idle && !animPunching && HandsFreeForFists();
            bool was = IsBlocking;
            IsBlocking = want && allowed;
            if (IsBlocking != was && debugLogs)
                Debug.Log($"[UnarmedMelee] {name}: guard {(IsBlocking ? "up" : "down")} (block meter {blockMeter:P0})" +
                          (want && !allowed ? $" — refused: {BlockRefusal()}" : ""), this);
            else if (want && !allowed && !wasRefused && debugLogs)
                Debug.Log($"[UnarmedMelee] {name}: guard held but refused — {BlockRefusal()}", this);
            wasRefused = want && !allowed;

            if (IsBlocking && !was)
            {
                // A hit reaction still playing sits above the combat layer
                // and would hide the block pose.
                if (ownGauge == null) ownGauge = GetComponent<LocalHitGauge>();
                if (ownGauge != null) ownGauge.CancelHitReaction();
                blockCheckAt = Time.time + 0.3f;
            }
            if (IsBlocking && blockCheckAt > 0f && Time.time >= blockCheckAt)
            {
                blockCheckAt = -1f;
                DiagnoseBlockPose();
            }

            if (!IsGuardBroken && Time.time - lastBlockedTime >= cfg.blockRegenDelay)
                blockMeter = Mathf.Min(1f, blockMeter + cfg.blockRegenRate * dt);
        }

        // Asked by the attacker before a blow lands on this player. True if
        // the blow is blocked (reduced); brokeGuard if the guard broke on it
        // (a Spartan kick breaks it and isn't blocked).
        public bool TryBlock(Vector3 forceDirection, HitZone zone, bool kick, bool spartan, out bool brokeGuard)
        {
            brokeGuard = false;
            if (!IsBlocking || zone == HitZone.Legs) return false;
            CombatSettings cfg = CombatSettings.Instance;

            // Covered only if the blow comes from in front of the view.
            Vector3 toAttacker = Vector3.ProjectOnPlane(-forceDirection, Vector3.up);
            if (toAttacker.sqrMagnitude < 1e-4f || Vector3.Angle(FlatForward(), toAttacker) > cfg.blockAngle) return false;

            // A Spartan kick breaks the guard (taken like a blocked blow), or
            // goes straight through with Spartan Breaks Guard Only off.
            if (spartan)
            {
                BreakGuard(cfg, "coup de pied spartiate");
                brokeGuard = true;
                return cfg.spartanBreaksGuardOnly;
            }

            blockMeter -= kick ? cfg.blockCostKick : cfg.blockCostPunch;
            lastBlockedTime = Time.time;
            counterUntil = Time.time + cfg.counterWindow;
            blockedReactUntil = Time.time + 0.25f;
            if (AnimatedPunches)
            {
                bodyAnimator.CrossFadeInFixedTime(BlockedState, 0.05f, combatLayer, 0f);
                layerState = BlockedState;
            }
            if (debugLogs) Debug.Log($"[UnarmedMelee] {name}: BLOCKED a {(kick ? "kick" : "punch")} ({zone}) — block meter {Mathf.Max(0f, blockMeter):P0}", this);

            if (blockMeter <= 0f)
            {
                BreakGuard(cfg, "jauge de garde vide");
                brokeGuard = true;
            }
            return true;
        }

        private void BreakGuard(CombatSettings cfg, string why)
        {
            blockMeter = 0f;
            guardBrokenUntil = Time.time + cfg.guardBreakTime;
            lastBlockedTime = Time.time;
            counterUntil = -10f;
            IsBlocking = false;
            if (debugLogs) Debug.Log($"[UnarmedMelee] {name}: GUARD BROKEN ({why}) — no guard for {cfg.guardBreakTime:0.#} s", this);
            GuardBroken?.Invoke(OwnIndex(), transform.position);
        }

        // A punch starting now: a counter if a blow was blocked just before.
        private void StartCounterIfAny()
        {
            counterPunch = move == Move.Punch && Boxing && Time.time < counterUntil;
            if (counterPunch)
            {
                counterUntil = -10f;
                if (debugLogs) Debug.Log($"[UnarmedMelee] {name}: COUNTER punch", this);
            }
        }

        // Provisional: the block meter under the crosshair of this player's
        // half of the screen, while it matters.
        private void DrawBlockMeter()
        {
            if (!Boxing || (!IsBlocking && blockMeter >= 1f && !IsGuardBroken)) return;
            DrawBar(0.28f, 120f, 6f, blockMeter,
                IsGuardBroken ? new Color(1f, .3f, .24f) : new Color(.35f, .65f, 1f),
                IsGuardBroken ? "GARDE BRISÉE" : IsBlocking ? "GARDE" : null);
        }
    }
}
