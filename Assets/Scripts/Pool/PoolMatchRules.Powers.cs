using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // The powers in stock, their effects and the melee window (BrawlPower).
    public partial class PoolMatchRules
    {
        // A power was added to a player's stock or went up a level (not when
        // the pickup is lost): shake, flash, Feel.
        public static event Action<int> PowerGranted;

        // What a pickup did — for the HUD (sponsor card, "Niveau 2 !",
        // "lost"): player, power, outcome, its level now.
        public enum PowerPickup { Added, LevelUp, Lost }
        public static event Action<int, PoolPower, PowerPickup, int> PowerPickedUp;

        // Same power again: one level up (lost beyond MaxPowerLevel). A new one
        // takes a free slot, and the selection if the selected slot was empty.
        // Both slots taken by other powers: lost. The crate or ball is used
        // up either way.
        public void GrantPower(int player, PoolPower power)
        {
            if (power == null) return;
            for (int s = 0; s < PowerSlots; s++)
            {
                if (heldPowers[player, s] != power) continue;
                if (heldLevels[player, s] >= MaxPowerLevel)
                {
                    PowerPickedUp?.Invoke(player, power, PowerPickup.Lost, heldLevels[player, s]);
                    return;
                }
                heldLevels[player, s]++;
                PowerGranted?.Invoke(player);
                PowerPickedUp?.Invoke(player, power, PowerPickup.LevelUp, heldLevels[player, s]);
                return;
            }
            for (int s = 0; s < PowerSlots; s++)
            {
                if (heldPowers[player, s] != null) continue;
                heldPowers[player, s] = power;
                heldLevels[player, s] = 1;
                // Selected when the selection pointed at an empty slot.
                if (heldPowers[player, selectedSlot[player]] == null) SelectSlot(player, s);
                PowerGranted?.Invoke(player);
                PowerPickedUp?.Invoke(player, power, PowerPickup.Added, 1);
                return;
            }
            PowerPickedUp?.Invoke(player, power, PowerPickup.Lost, 1);
        }

        // The selected power, the one the button launches.
        public PoolPower GetHeldPower(int player) => heldPowers[player, selectedSlot[player]];

        // 1 normally, 2 after picking up the same power again (stronger
        // version, read by the power in Activate).
        public int GetHeldPowerLevel(int player) => heldLevels[player, selectedSlot[player]];
        public int ActivatingPowerLevel { get; private set; } = 1;

        public int SelectedPowerSlot(int player) => selectedSlot[player];

        // "Trou N2 · [Aimant]" — the selected one in brackets (debug texts).
        public string DescribeStock(int player)
        {
            var parts = new System.Text.StringBuilder();
            for (int s = 0; s < PowerSlots; s++)
            {
                PoolPower power = heldPowers[player, s];
                string name = power == null ? "—" : power.PowerName + (heldLevels[player, s] > 1 ? $" N{heldLevels[player, s]}" : "");
                if (s > 0) parts.Append(" · ");
                parts.Append(s == selectedSlot[player] ? $"[{name}]" : name);
            }
            return parts.ToString();
        }
        public PoolPower GetPowerInSlot(int player, int slot) => heldPowers[player, slot];
        public int GetPowerLevelInSlot(int player, int slot) => heldLevels[player, slot];

        // Selects the next slot holding a power (direction ±1; with two
        // slots, the other one). False when there's nothing else to select.
        public bool CycleSelectedPower(int player, int direction)
        {
            for (int k = 1; k < PowerSlots; k++)
            {
                int s = ((selectedSlot[player] + direction * k) % PowerSlots + PowerSlots) % PowerSlots;
                if (heldPowers[player, s] == null) continue;
                SelectSlot(player, s);
                return true;
            }
            return false;
        }

        private void SelectSlot(int player, int slot) => selectedSlot[player] = slot;

        // A power was launched: player, power, and whether its effect waits
        // for the opponent's next turn. For the HUD and GameFeel.
        public static event Action<int, PoolPower, bool> PowerActivated;

        // A press of the power button that launched nothing, and why
        // (LocalPoolPowerController raises it through NotifyPowerRefused).
        public static event Action<int, PowerRefusal> PowerRefused;

        public static void NotifyPowerRefused(int player, PowerRefusal reason) => PowerRefused?.Invoke(player, reason);

        // Whether the power in hand can be launched now (PoolPower's turn and
        // rolling-balls rules). In hot-seat solo the turn rules are skipped,
        // or a power refused during one's own turn could never be used. Speed
        // Pool skips both: there are no turns and the table is never still.
        public PowerRefusal CheckPowerUse(int player)
        {
            PoolPower power = GetHeldPower(player);
            if (power == null) return PowerRefusal.NoPower;
            // The mode's own rule: the Shooter's one armed power, the seeker's Sonar.
            if (ruleSet != null && !ruleSet.AllowsPower(player)) return PowerRefusal.CannotAct;
            if (IsSimultaneous) return PowerRefusal.None;
            if (PlayerInput.all.Count > 1)
            {
                bool ownTurn = CurrentPlayer == player;
                if (power.RequiresOwnTurn && !ownTurn) return PowerRefusal.OwnTurnOnly;
                if (!power.RequiresOwnTurn && !power.AllowDuringOwnTurn && ownTurn) return PowerRefusal.OpponentTurnOnly;
            }
            if (!power.AllowWhileBallsMoving && (shotInProgress || PoolBall.AnyMoving())) return PowerRefusal.BallsMoving;
            return PowerRefusal.None;
        }

        // target: the pocket picked for a PocketTargetPower (random open
        // pocket if none). False if nothing was held.
        public bool TryActivatePower(int player, PoolPocket target = null) => ActivateHeld(player, target, null);

        // Same for a TablePointPower, with the point of the table picked.
        public bool TryActivatePower(int player, Vector3 tablePoint) => ActivateHeld(player, null, tablePoint);

        private bool ActivateHeld(int player, PoolPocket pocket, Vector3? tablePoint)
        {
            int slot = selectedSlot[player];
            PoolPower power = heldPowers[player, slot];
            if (power == null) return false;

            heldPowers[player, slot] = null;
            ActivatingPowerLevel = heldLevels[player, slot];
            heldLevels[player, slot] = 1;
            // The other power, if any, is up next.
            CycleSelectedPower(player, 1);
            // Read before Activate: an off-turn activation can't switch the
            // turn, but keep it independent of what the power does.
            bool waitsForOpponentTurn = power.TargetsOpponent && CurrentPlayer == player && !IsSimultaneous;
            if (pocket != null && power is PocketTargetPower targeted)
                targeted.Activate(this, player, pocket);
            else if (tablePoint.HasValue && power is TablePointPower pointed)
                pointed.Activate(this, player, tablePoint.Value);
            else
                power.Activate(this, player);
            ActivatingPowerLevel = 1;
            PowerActivated?.Invoke(player, power, waitsForOpponentTurn);
            return true;
        }

        public void SetShotPowerMultiplier(int player, float multiplier) => shotPowerMultiplier[player] = multiplier;

        // ---------- Melee (MeleeHit.Allowed) ----------

        // Until when each player may hit outside the modes that allow it (BrawlPower).
        private readonly float[] meleeUntil = new float[2];

        public bool CanHit(int player) => CombatSettings.Instance.AllowsMelee(Mode) || Time.time < meleeUntil[player];

        public void AllowMelee(int player, float duration)
        {
            meleeUntil[player] = Time.time + duration;
            if (traceShots) Debug.Log($"[Match] J{player + 1} peut frapper pendant {duration:0.#} s.", this);
        }

        // Both stocks emptied (Pocket Size: powers belong to a role, the
        // roles swap between rounds).
        public void ClearHeldPowers()
        {
            for (int p = 0; p < 2; p++)
            {
                for (int s = 0; s < PowerSlots; s++) { heldPowers[p, s] = null; heldLevels[p, s] = 1; }
                selectedSlot[p] = 0;
            }
        }

        // Called by the aim controllers when computing a shot's impulse —
        // reads and resets to 1 in the same call so it only ever applies once.
        public float ConsumeShotPowerMultiplier(int player)
        {
            float multiplier = shotPowerMultiplier[player];
            shotPowerMultiplier[player] = 1f;
            return multiplier;
        }

        // Vision Impair (Attack): overlay and lower look sensitivity on the
        // target. It starts when they enter aim and ends with their shot, so
        // it lasts exactly one of their shots. SwitchTurn clears it in case.
        private readonly bool[] visionImpaired = new bool[2];
        private readonly float[] visionImpairedSensitivity = new float[2];

        // Activated but waiting for the target's turn.
        private readonly bool[] visionImpairPending = new bool[2];
        private readonly float[] pendingVisionImpairSensitivity = new float[2];

        // Queued until the target enters aim on their turn. If it's already
        // their turn (power launched off-turn, or Speed Pool), it applies
        // right away; it ends with their shot (LocalPoolAimController).
        public void QueueVisionImpair(int player, float sensitivityMultiplier)
        {
            if (IsSimultaneous || CurrentPlayer == player)
            {
                visionImpaired[player] = true;
                visionImpairedSensitivity[player] = sensitivityMultiplier;
                return;
            }

            visionImpairPending[player] = true;
            pendingVisionImpairSensitivity[player] = sensitivityMultiplier;
        }

        public void ConsumePendingVisionImpair(int player)
        {
            if (!visionImpairPending[player]) return;
            visionImpairPending[player] = false;
            visionImpaired[player] = true;
            visionImpairedSensitivity[player] = pendingVisionImpairSensitivity[player];
        }

        // Ends when the target leaves aim.
        public void EndVisionImpair(int player) => visionImpaired[player] = false;

        // Backing out of aim without shooting puts the debuff back on hold
        // until the next aim this turn, so it can't be shaken off that way.
        public void SuspendVisionImpair(int player)
        {
            if (!visionImpaired[player]) return;
            visionImpaired[player] = false;
            visionImpairPending[player] = true;
            pendingVisionImpairSensitivity[player] = visionImpairedSensitivity[player];
        }

        public bool IsVisionImpaired(int player) => visionImpaired[player];

        // On for the whole turn, no fade.
        public float VisionImpairmentStrength(int player) => visionImpaired[player] ? 1f : 0f;

        public float GetVisionImpairmentSensitivityMultiplier(int player) =>
            visionImpaired[player] ? visionImpairedSensitivity[player] : 1f;

        // Inverted Controls (Attack): same lifecycle as Vision Impair, but it
        // flips the look, boosts sensitivity and hides the preview.
        private readonly bool[] invertedControls = new bool[2];
        private readonly float[] invertedControlsSensitivity = new float[2];

        private readonly bool[] invertedControlsPending = new bool[2];
        private readonly float[] pendingInvertedControlsSensitivity = new float[2];

        // Same as QueueVisionImpair.
        public void QueueInvertedControls(int player, float sensitivityMultiplier)
        {
            if (IsSimultaneous || CurrentPlayer == player)
            {
                invertedControls[player] = true;
                invertedControlsSensitivity[player] = sensitivityMultiplier;
                return;
            }

            invertedControlsPending[player] = true;
            pendingInvertedControlsSensitivity[player] = sensitivityMultiplier;
        }

        public void ConsumePendingInvertedControls(int player)
        {
            if (!invertedControlsPending[player]) return;
            invertedControlsPending[player] = false;
            invertedControls[player] = true;
            invertedControlsSensitivity[player] = pendingInvertedControlsSensitivity[player];
        }

        public void EndInvertedControls(int player) => invertedControls[player] = false;

        // Same as SuspendVisionImpair.
        public void SuspendInvertedControls(int player)
        {
            if (!invertedControls[player]) return;
            invertedControls[player] = false;
            invertedControlsPending[player] = true;
            pendingInvertedControlsSensitivity[player] = invertedControlsSensitivity[player];
        }

        // Also hides the trajectory preview.
        public bool IsControlsInverted(int player) => invertedControls[player];

        public float GetInvertedControlsSensitivityMultiplier(int player) =>
            invertedControls[player] ? invertedControlsSensitivity[player] : 1f;

        // Close Pocket (Attack): a table effect, closed for the opponent's
        // whole turn.
        private PoolPocket closedPocket;
        private int closedPocketOwner;
        private PoolPocket pendingClosePocket;
        private int pendingClosePocketOwner;

        // Launched during the opponent's turn: closes now. During one's own
        // turn: closes at SwitchTurn, so it never costs the launcher a shot.
        // Speed Pool: now, until the opponent's next shot is over.
        public void QueueClosePocket(int activatingPlayer, PoolPocket pocket)
        {
            if (IsSimultaneous || CurrentPlayer != activatingPlayer)
            {
                ApplyPocketClose(activatingPlayer, pocket);
                return;
            }

            pendingClosePocket = pocket;
            pendingClosePocketOwner = activatingPlayer;
        }

        // Stays closed for the opponent's whole turn, reopened by SwitchTurn
        // when play comes back to the launcher.
        private void ApplyPocketClose(int owner, PoolPocket pocket)
        {
            if (closedPocket != null) closedPocket.SetClosed(false);
            closedPocket = null;
            if (pocket == null) return;

            pocket.SetClosed(true);
            closedPocket = pocket;
            closedPocketOwner = owner;
        }

        // Identical Groups (Attack): every ball but the cue ball looks the same
        // for the target's turn. One slot: the target, -1 when none.
        private int identicalGroupsTarget = -1;
        private IdenticalGroupsPower pendingIdenticalGroups;
        private int pendingIdenticalGroupsTarget;

        // True while the balls are disguised — the HUD hides its plate
        // (balls left per group), which would otherwise give them away.
        public bool AreGroupsDisguised => identicalGroupsTarget >= 0;

        // Same rule as QueueVisionImpair. Speed Pool: until the target's next
        // shot is over (the launcher sees it too, the look is global).
        public void QueueIdenticalGroups(int targetPlayer, IdenticalGroupsPower power)
        {
            if (IsSimultaneous || CurrentPlayer == targetPlayer)
            {
                ApplyIdenticalGroups(targetPlayer, power);
                return;
            }
            pendingIdenticalGroups = power;
            pendingIdenticalGroupsTarget = targetPlayer;
        }

        private void ApplyIdenticalGroups(int targetPlayer, IdenticalGroupsPower power)
        {
            BallDisguise.Apply(power.BuildLook());
            identicalGroupsTarget = targetPlayer;
        }

        private void EndIdenticalGroups()
        {
            BallDisguise.Restore();
            identicalGroupsTarget = -1;
        }

        // Magnet: armed on a pocket, used by the launcher's next shot, during
        // which FixedUpdate catches one of their balls and pulls it in.
        private readonly MagnetPower[] magnetArmed = new MagnetPower[2];
        private readonly PoolPocket[] magnetArmedPocket = new PoolPocket[2];
        private MagnetPower magnetThisShot;
        private PoolPocket magnetPocketThisShot;
        private int magnetShooter;
        private PoolBall magnetCaught;
        private float magnetCaughtAt;
        // The pocket's looping VFX, kept until the magnet is done.
        private readonly GameObject[] magnetArmedVfx = new GameObject[2];
        private GameObject magnetVfxThisShot;

        public void ArmMagnet(int player, MagnetPower power, PoolPocket pocket)
        {
            DisarmMagnet(player);
            magnetArmed[player] = power;
            magnetArmedPocket[player] = pocket;
            magnetArmedVfx[player] = power.SpawnPocketVfx(pocket);
        }

        private void DisarmMagnet(int player)
        {
            if (magnetArmed[player] != null) magnetArmed[player].StopPocketVfx(magnetArmedVfx[player]);
            magnetArmed[player] = null;
            magnetArmedPocket[player] = null;
            magnetArmedVfx[player] = null;
        }

        // The pocket an armed magnet will pull into, null when none is armed
        // (HUD banner).
        public PoolPocket GetArmedMagnetPocket(int player) => magnetArmed[player] != null ? magnetArmedPocket[player] : null;

        // A ball caught by the magnet (the ball, the pocket) — feedback hook.
        public static event Action<PoolBall, PoolPocket> MagnetCaught;

        private void FixedUpdate()
        {
            if (magnetThisShot == null || !(shotInProgress || IsSimultaneous)) return;

            if (magnetCaught == null)
            {
                magnetCaught = magnetThisShot.FindCapture(this, magnetShooter, magnetPocketThisShot);
                if (magnetCaught == null) return;
                // The 8 into any pocket but the called one loses the game:
                // the magnet never does that to its own user.
                if (magnetCaught.Group == BallGroup.Eight && ruleSet is EightBallRuleSet
                    && calledEightBallPocket != magnetPocketThisShot)
                {
                    magnetCaught = null;
                    return;
                }
                magnetCaughtAt = Time.time;
                MagnetCaught?.Invoke(magnetCaught, magnetPocketThisShot);
            }

            // Pocketed (deactivated), parked, or held too long (stuck against
            // a cushion jaw — the shot would otherwise never settle): done.
            if (!magnetCaught.isActiveAndEnabled || magnetCaught.Rigidbody.isKinematic
                || Time.time - magnetCaughtAt > magnetThisShot.MaxPullDuration)
            {
                EndMagnetShot();
                return;
            }
            magnetThisShot.Pull(magnetCaught, magnetPocketThisShot);
        }

        private void EndMagnetShot()
        {
            if (magnetThisShot != null) magnetThisShot.StopPocketVfx(magnetVfxThisShot);
            magnetVfxThisShot = null;
            magnetThisShot = null;
            magnetPocketThisShot = null;
            magnetCaught = null;
        }


        // Ball Blast: armed, used by the launcher's next shot.
        private readonly BallBlastPower[] ballBlastArmed = new BallBlastPower[2];
        private BallBlastPower ballBlastThisShot;
        // Speed Pool: one per player, both can be rolling at once.
        private readonly BallBlastPower[] ballBlastRolling = new BallBlastPower[2];

        public void ArmBallBlast(int player, BallBlastPower power) => ballBlastArmed[player] = power;
        public bool IsBallBlastArmed(int player) => ballBlastArmed[player] != null;

        // A ball destroyed by Ball Blast (before it's removed): the ball and
        // the shooter. For the explosion's HUD shout.
        public static event Action<PoolBall, int> BallBlasted;
    }
}
