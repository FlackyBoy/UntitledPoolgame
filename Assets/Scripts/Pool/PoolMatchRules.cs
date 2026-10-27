using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Runs the match: waits for the balls to stop after each shot, then
    // hands the shot to the IPoolRuleSet of the chosen mode. Also holds the
    // powers in stock and their turn-bound effects. Local only for now (ball
    // physics isn't networked).
    public class PoolMatchRules : MonoBehaviour
    {
        // One match per scene.
        public static PoolMatchRules Instance { get; private set; }

        [Tooltip("Diagnostic : à la fin de chaque tir, la console donne la 1re bille touchée, les billes rentrées et pourquoi c'est une faute ou non ([ShotTrace]).")]
        [SerializeField] private bool traceShots = true;
        public bool TraceShots => traceShots;

        public int CurrentPlayer { get; private set; }
        public bool GameOver { get; private set; }
        public int Winner { get; private set; } = -1;
        public bool MatchStarted { get; private set; }
        // Type, mode and powers locked in at StartMatch (normalized: never
        // the former Party / SpeedPool modes).
        public PoolMatchSetup Setup { get; private set; }
        public PoolGameMode Mode => Setup.mode;
        // Read by the crates and the power ball.
        public bool PowersEnabled => Setup.powers;

        // From a foul until the cue ball is placed again.
        public bool BallInHand { get; private set; }

        private IPoolRuleSet ruleSet;

        // Speed Pool: no turns, both players shoot whenever their own cue ball
        // is still. No shot is ever resolved: the rule set judges each pot as
        // it happens (SpeedPoolRuleSet.HandlePocketed).
        public bool IsSimultaneous => Setup.type == PoolMatchType.SpeedPool;

        // Each player has their own cue ball (Speed Pool, Hungry Hole, Snake
        // Pool, Power Wash Pool), in turns or not.
        public bool HasPlayerCueBalls => ruleSet is SpeedPoolRuleSet;

        // Two powers in stock per player; the selected one is what the power
        // button launches (switched with the wheel / D-pad).
        public const int PowerSlots = 2;
        public const int MaxPowerLevel = 2;
        private readonly PoolPower[,] heldPowers = new PoolPower[2, PowerSlots];
        private readonly int[,] heldLevels = { { 1, 1 }, { 1, 1 } };
        private readonly int[] selectedSlot = new int[2];
        // Set by Boosted Shot, read once by the next shot.
        private readonly float[] shotPowerMultiplier = { 1f, 1f };

        private readonly List<PoolBall> pocketedThisShot = new List<PoolBall>();
        // Pocket each ball of this shot fell into (8-ball call check).
        private readonly Dictionary<PoolBall, PoolPocket> pocketByBallThisShot = new Dictionary<PoolBall, PoolPocket>();
        private bool cueBallPocketedThisShot;
        private PoolBall firstContactThisShot;
        private bool wasMoving;
        // Set by an actual shot only: the balls settling after the table
        // loads would otherwise count as a shot (and a foul).
        private bool shotInProgress;

        // The provisional OnGUI mode select.
        private PoolMatchSetup selected = new PoolMatchSetup(PoolGameMode.EightBall, PoolMatchType.TurnBased, false, 150);
        private string targetScoreInput = "150";

        // OnGUI runs several passes per frame; changing what gets drawn from a
        // button click between passes breaks GUILayout. Clicks are stored here
        // and applied in Update.
        private PoolMatchSetup? pendingSelection;
        private bool pendingStart;
        private bool pendingRestart;

        // Set by a menu drawn elsewhere (the Synthèse menu, UI Toolkit): the
        // provisional OnGUI mode select is then not drawn.
        public static bool ExternalMenu { get; set; }

        // Set by the in-game HUD (MatchHud, UI Toolkit): the provisional
        // OnGUI status lines, game-over box and aim hints are then not drawn.
        public static bool ExternalHud { get; set; }

        // "Revanche" from outside (the HUD's end card), through the same
        // deferred path as the OnGUI "Rejouer" button.
        public void RequestRestart()
        {
            if (GameOver) pendingRestart = true;
        }

        // For the HUD: 8-ball group (Cue = undecided or not 8-ball), 14.1 score.
        public BallGroup GetGroup(int player) => ruleSet switch
        {
            EightBallRuleSet eightBall => eightBall.GroupOf(player),
            SpeedPoolRuleSet speedPool => speedPool.GroupOf(player),
            _ => BallGroup.Cue,
        };
        public bool TryGetScore(int player, out int score, out int target)
        {
            score = target = 0;
            if (!(ruleSet is FourteenOneRuleSet straight)) return false;
            score = straight.ScoreOf(player);
            target = straight.TargetScore;
            return true;
        }

        // Fired after a shot is resolved when the turn passes to the other
        // player (CurrentPlayer is already the new one).
        public static event Action<int> TurnChanged;

        // Starts a match from outside (a menu), through the same deferred path
        // as the OnGUI buttons: applied in the next Update.
        public void RequestStart(PoolMatchSetup setup)
        {
            if (MatchStarted) return;
            pendingSelection = setup;
            pendingStart = true;
        }

        // Shared with PoolPocket.
        private static PoolPotEffectSettings potEffectSettings;

        private void Awake()
        {
            Instance = this;

            if (potEffectSettings == null)
                potEffectSettings = PoolSettingsLoader.LoadOrDefault<PoolPotEffectSettings>("PoolPotEffectSettings");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Safety net: never leave the game stuck slowed down if this
            // gets destroyed (scene reload, etc.) mid-dip.
            Time.timeScale = 1f;
            // Static state: not left pointing at this scene's balls.
            BallDisguise.Restore();
        }

        private void OnEnable()
        {
            PoolBall.Pocketed += HandleBallPocketed;
            PoolBall.CueBallFirstContact += HandleFirstContact;
        }

        private void OnDisable()
        {
            PoolBall.Pocketed -= HandleBallPocketed;
            PoolBall.CueBallFirstContact -= HandleFirstContact;
        }

        private Coroutine slowMotionRoutine;

        private void HandleBallPocketed(PoolBall ball, PoolPocket pocket)
        {
            if (!MatchStarted) return;

            SpeedPoolRuleSet ownCueBalls = ruleSet as SpeedPoolRuleSet;
            if (IsSimultaneous)
            {
                // No slow motion: the other player is mid-shot.
                if (GameOver || ownCueBalls == null) return;
                if (ball.IsCueBall) StartCoroutine(RespawnCueBall(ball, MatchGeneration));
                ownCueBalls.HandlePocketed(this, ball);
                return;
            }

            // Here rather than per pocket: Time.timeScale is global and two
            // coroutines would fight over it.
            TriggerPotSlowMotion();

            // Each their own cue ball, in turns: judged on each pot as in Speed
            // Pool, the shot only decides who plays next (ResolveShot).
            if (ownCueBalls != null)
            {
                if (GameOver) return;
                if (ball.IsCueBall) StartCoroutine(RespawnCueBall(ball, MatchGeneration));
                ownCueBalls.HandlePocketed(this, ball);
                // Sending the opponent's cue ball in costs them, not the shooter.
                if (ball.IsCueBall && ball.CueOwner >= 0 && ball.CueOwner != CurrentPlayer) return;
            }

            if (ball.IsCueBall)
            {
                cueBallPocketedThisShot = true;
                return;
            }
            pocketedThisShot.Add(ball);
            pocketByBallThisShot[ball] = pocket;

            // A power ball still counts normally for the rules.
            if (ball.TryGetComponent(out PowerBall powerBall) && powerBall.Power != null)
                GrantPower(CurrentPlayer, powerBall.Power);
        }

        // A player's own cue ball potted: it waits in hand (PoolBall.OnPocketed
        // parked it on its spawn spot), then comes back there or as near as
        // is free. Speed Pool: after Cue Respawn Delay, the wait is the
        // penalty. In turns: once the shot is over.
        private IEnumerator RespawnCueBall(PoolBall cueBall, int generation)
        {
            if (IsSimultaneous)
            {
                if (speedPoolSettings == null)
                    speedPoolSettings = PoolSettingsLoader.LoadOrDefault<SpeedPoolSettings>("SpeedPoolSettings");
                yield return new WaitForSecondsRealtime(speedPoolSettings.cueRespawnDelay);
            }
            else yield return new WaitUntil(() => !shotInProgress || generation != MatchGeneration);
            // "Rejouer" meanwhile already put it back.
            if (generation != MatchGeneration || cueBall == null || !cueBall.Rigidbody.isKinematic) yield break;
            // Its own starting spot, not its transform: the transform may still
            // be in the pocket (OnPocketed moves the body), and the ball came
            // back inside the pocket trigger, potted again over and over. Only
            // moved aside if another ball sits on that spot.
            Vector3 spot = FreeSpotNear(cueBall.RespawnPosition, cueBall);
            if (traceShots) Debug.Log($"[SpeedPool] Blanche de J{cueBall.CueOwner + 1} remise en jeu à {spot}.", cueBall);
            cueBall.PlaceAt(spot);
            cueBall.EndBallInHand();
        }

        private static SpeedPoolSettings speedPoolSettings;

        // A potted ball back on the table, at its rack spot or the nearest
        // free one (Speed Pool scratch penalty).
        public void RespotBall(PoolBall ball)
        {
            // Its respawn spot: a mode may have re-racked it (Hungry Hole's sizes).
            Vector3 spot = FreeSpotNear(ball.RespawnPosition, ball);
            ball.ResetToSpawn();
            ball.PlaceAt(spot);
        }

        // A second pot during the dip restarts it from full instead of
        // stacking.
        private void TriggerPotSlowMotion()
        {
            if (slowMotionRoutine != null) StopCoroutine(slowMotionRoutine);
            slowMotionRoutine = StartCoroutine(PotSlowMotionRoutine());
        }

        private IEnumerator PotSlowMotionRoutine()
        {
            Time.timeScale = potEffectSettings.potSlowMotionScale;
            yield return new WaitForSecondsRealtime(potEffectSettings.potSlowMotionDuration);
            Time.timeScale = 1f;
            slowMotionRoutine = null;
        }

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

        // Balls the player is after (what the magnet may catch). 8-ball: their
        // group, the 8 once cleared, any solid or stripe while undecided.
        // Other modes: any object ball.
        public bool IsOwnBall(int player, PoolBall ball)
        {
            if (ball.IsCueBall) return false;
            if (ruleSet is SpeedPoolRuleSet) return ball.Group == GetGroup(player);
            if (!(ruleSet is EightBallRuleSet)) return true;
            if (IsShootingForEightBall(player)) return ball.Group == BallGroup.Eight;
            BallGroup group = GetGroup(player);
            return group == BallGroup.Cue ? ball.Group != BallGroup.Eight : ball.Group == group;
        }

        // What a ball potted during the current shot means for the shooter,
        // for the HUD and Feel (the rules judge the whole shot later):
        //   Good       one of theirs;
        //   Opponent   the other group's (a foul on its own);
        //   EarlyEight the 8 before their group is cleared (game lost).
        // Read before the ball leaves the table, so potting one's last ball
        // still counts as one's own.
        public enum PotMeaning { Good, Opponent, EarlyEight }

        public PotMeaning MeaningOfPot(PoolBall ball)
        {
            // Speed Pool: every group ball is good news for its owner (PotOwner).
            if (IsSimultaneous) return PotMeaning.Good;
            if (IsOwnBall(CurrentPlayer, ball)) return PotMeaning.Good;
            // Own cue balls in turns: the 8 never loses on the spot there.
            if (HasPlayerCueBalls) return PotMeaning.Opponent;
            return ball.Group == BallGroup.Eight ? PotMeaning.EarlyEight : PotMeaning.Opponent;
        }

        // Who a pot is about, for the HUD's shout: the shooter, or in Speed
        // Pool the owner of the cue ball or of the ball's group.
        public int PotOwner(PoolBall ball)
        {
            if (!IsSimultaneous) return CurrentPlayer;
            if (ball.IsCueBall) return Mathf.Max(0, ball.CueOwner);
            return Mathf.Max(0, SpeedPoolRuleSet.OwnerOf(ball));
        }

        // Split screen: the player's own index. Hot-seat solo (one
        // PlayerInput): whoever's turn it is, so powers and debuffs resolve
        // against the side being played.
        public int GetEffectivePlayerIndex(int physicalPlayerIndex) =>
            PlayerInput.all.Count <= 1 ? CurrentPlayer : physicalPlayerIndex;

        private void HandleFirstContact(PoolBall cueBall, PoolBall other)
        {
            if (!MatchStarted) return;
            firstContactThisShot = other;

            // Ball Blast: the touched ball explodes and leaves the table as if
            // potted by the shooter, then the rule set judges it like any pot.
            // Speed Pool: the shot of this cue ball's owner.
            int shooter = IsSimultaneous ? Mathf.Max(0, cueBall.CueOwner) : CurrentPlayer;
            BallBlastPower blast;
            if (IsSimultaneous) { blast = ballBlastRolling[shooter]; ballBlastRolling[shooter] = null; }
            else { blast = ballBlastThisShot; ballBlastThisShot = null; }
            if (blast == null) return;
            if (!blast.CanBlast(other))
            {
                Debug.Log($"[BallBlast] Joueur {shooter + 1} : {other.name} épargnée (bille 8) — pouvoir perdu.");
                return;
            }
            blast.PlayEffects(other);
            BallBlasted?.Invoke(other, shooter);
            other.OnPocketed();
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

        // Whether this player may shoot (offline only for now). With at most
        // one active PlayerInput, always true: a solo player would otherwise
        // be locked out on player 2's turn.
        public bool CanPlayerShoot(int playerIndex) =>
            MatchStarted && !GameOver && (IsSimultaneous || CurrentPlayer == playerIndex || PlayerInput.all.Count <= 1);

        // Raised before SwitchTurn: CurrentPlayer is the one who fouled.
        public static event Action Fouled;

        // Called by the rule set after a foul: the cue ball goes in hand for
        // the player who now has the turn.
        public void RegisterFoul()
        {
            if (traceShots) Debug.Log($"[Match] Faute de J{CurrentPlayer + 1} ({Mode}, {ruleSet?.GetType().Name}), frame {Time.frameCount}.", this);
            BallInHand = true;
            PoolBall.FindCueBall()?.BeginBallInHand();
            Fouled?.Invoke();
        }

        // Called by the aim controller once the player has confirmed the cue
        // ball's new position.
        public void ConfirmBallPlaced() => BallInHand = false;

        // Called when a shot is struck: only this starts a shot. cueBall: the
        // ball struck (Speed Pool: tells whose shot it is).
        public void NotifyShotFired(PoolBall cueBall = null)
        {
            // Speed Pool has no shot loop, only each player's own shot.
            if (IsSimultaneous)
            {
                BeginPlayerShot(cueBall);
                return;
            }
            pocketedThisShot.Clear();
            pocketByBallThisShot.Clear();
            cueBallPocketedThisShot = false;
            firstContactThisShot = null;
            shotInProgress = true;
            if (ruleSet is SpeedPoolRuleSet ownCueBalls) ownCueBalls.BeginShot();

            ballBlastThisShot = ballBlastArmed[CurrentPlayer];
            ballBlastArmed[CurrentPlayer] = null;

            EndMagnetShot();
            magnetThisShot = magnetArmed[CurrentPlayer];
            magnetPocketThisShot = magnetArmedPocket[CurrentPlayer];
            magnetVfxThisShot = magnetArmedVfx[CurrentPlayer];
            magnetShooter = CurrentPlayer;
            magnetArmed[CurrentPlayer] = null;
            magnetArmedPocket[CurrentPlayer] = null;
            magnetArmedVfx[CurrentPlayer] = null;

            // Where every ball stood before the shot: a ball that falls in a
            // table hole comes back there.
            shotStartPositions.Clear();
            foreach (PoolBall ball in PoolBall.Active)
                shotStartPositions[ball] = ball.transform.position;
            caughtByLivingHoleThisShot.Clear();
        }

        // ---------- Speed Pool: each player's shot is their "turn" ----------
        // It starts when they strike and ends once their cue ball is at rest
        // again (or potted). The effects that last "the opponent's turn" in
        // the other modes last until the end of the target's next shot.

        private readonly PoolBall[] rollingCueBall = new PoolBall[2];
        private readonly float[] rollingSince = new float[2];
        // The cue ball reads as still for a frame or two after the strike.
        private const float MinShotTime = 0.3f;

        private void BeginPlayerShot(PoolBall cueBall)
        {
            int p = cueBall != null ? Mathf.Clamp(cueBall.CueOwner, 0, 1) : 0;
            if (rollingCueBall[p] != null) EndPlayerShot(p);
            rollingCueBall[p] = cueBall;
            rollingSince[p] = Time.time;

            ballBlastRolling[p] = ballBlastArmed[p];
            ballBlastArmed[p] = null;

            if (magnetArmed[p] != null)
            {
                EndMagnetShot();
                magnetThisShot = magnetArmed[p];
                magnetPocketThisShot = magnetArmedPocket[p];
                magnetVfxThisShot = magnetArmedVfx[p];
                magnetShooter = p;
                magnetArmed[p] = null;
                magnetArmedPocket[p] = null;
                magnetArmedVfx[p] = null;
            }
        }

        private void UpdatePlayerShots()
        {
            for (int p = 0; p < 2; p++)
            {
                PoolBall ball = rollingCueBall[p];
                if (ball == null || Time.time - rollingSince[p] < MinShotTime) continue;
                if (!ball.isActiveAndEnabled || ball.Rigidbody.isKinematic || !ball.IsRolling) EndPlayerShot(p);
            }
        }

        private void EndPlayerShot(int p)
        {
            rollingCueBall[p] = null;
            // Unused: the cue ball touched nothing.
            ballBlastRolling[p] = null;
            if (magnetThisShot != null && magnetShooter == p) EndMagnetShot();

            // What was aimed at p ends with p's shot.
            if (closedPocket != null && closedPocketOwner != p)
            {
                closedPocket.SetClosed(false);
                closedPocket = null;
            }
            if (identicalGroupsTarget == p) EndIdenticalGroups();

            for (int i = tableHoles.Count - 1; i >= 0; i--)
            {
                TableHoleEntry entry = tableHoles[i];
                bool close = entry.hole == null;
                switch (entry.duration)
                {
                    case TableHoleDuration.OpponentTurn:
                        close |= p != entry.owner;
                        break;
                    case TableHoleDuration.Turns:
                        if (p != entry.owner && --entry.turnsLeft <= 0) close = true;
                        break;
                    case TableHoleDuration.NextShot:
                        if (p == entry.owner)
                        {
                            if (entry.skipNextResolve) entry.skipNextResolve = false;
                            else close = true;
                        }
                        break;
                }
                if (!close) continue;
                if (entry.hole != null) entry.hole.Close(instant: false);
                tableHoles.RemoveAt(i);
            }
        }

        // ---------- Table holes (TableHolePower) ----------

        private class TableHoleEntry
        {
            public TableHole hole;
            public int owner;
            public TableHoleDuration duration;
            public int turnsLeft;
            // Opened while a shot was rolling: "next shot" means the one
            // after it, not the one already under way.
            public bool skipNextResolve;
        }

        private readonly List<TableHoleEntry> tableHoles = new List<TableHoleEntry>();
        private readonly List<PoolBall> ballsInTableHoles = new List<PoolBall>();
        private readonly Dictionary<PoolBall, Vector3> shotStartPositions = new Dictionary<PoolBall, Vector3>();

        // Bumped by every restart: a meteor still falling from the previous
        // match lands without opening anything (TableHoleMeteor).
        public int MatchGeneration { get; private set; }

        // Holes launched during the launcher's own turn: spots marked by a
        // ring, meteors fall at SwitchTurn.
        private class PendingTableHoles
        {
            public TableHolePower power;
            public List<Vector3> centers;
            public int owner;
            public readonly List<GameObject> markers = new List<GameObject>();
        }

        private readonly List<PendingTableHoles> pendingTableHoles = new List<PendingTableHoles>();

        public void QueueTableHoles(TableHolePower power, List<Vector3> centers, int owner)
        {
            // Already the opponent's turn (split screen), or Speed Pool: right now.
            if (IsSimultaneous || CurrentPlayer != owner)
            {
                power.LaunchMeteors(centers, owner, MatchGeneration);
                return;
            }

            var pending = new PendingTableHoles { power = power, centers = centers, owner = owner };
            foreach (Vector3 center in centers)
            {
                LineRenderer ring = RingLine.Create(null, "TableHoleTarget", power.RimWidth, power.RimColor);
                RingLine.Set(ring, center + Vector3.up * 0.003f, power.HoleRadius);
                pending.markers.Add(ring.gameObject);
            }
            pendingTableHoles.Add(pending);
        }

        // From SwitchTurn, CurrentPlayer being the player whose turn starts.
        private void LaunchPendingTableHoles()
        {
            for (int i = pendingTableHoles.Count - 1; i >= 0; i--)
            {
                PendingTableHoles pending = pendingTableHoles[i];
                if (CurrentPlayer == pending.owner) continue;
                pending.power.LaunchMeteors(pending.centers, pending.owner, MatchGeneration);
                DestroyMarkers(pending);
                pendingTableHoles.RemoveAt(i);
            }
        }

        private static void DestroyMarkers(PendingTableHoles pending)
        {
            foreach (GameObject marker in pending.markers)
                if (marker != null) Destroy(marker);
        }

        public void AddTableHole(TableHole hole, int owner, TableHolePower power)
        {
            tableHoles.Add(new TableHoleEntry
            {
                hole = hole,
                owner = owner,
                duration = power.Duration,
                turnsLeft = power.Turns,
                skipNextResolve = IsSimultaneous ? rollingCueBall[owner] != null : shotInProgress,
            });
        }

        // A ball fell in a table hole. The cue ball is a foul (like a
        // scratch). Others sink and come back to where they were once the
        // shot is over. Outside a shot, the ball goes back on the hole's edge.
        public void HandleBallFellInTableHole(PoolBall ball, TableHole hole)
        {
            if (!MatchStarted || GameOver) return;
            if (hole.Living != null)
            {
                HandleBallCaughtByLivingHole(ball, hole);
                return;
            }
            if (!shotInProgress)
            {
                Vector3 away = ball.transform.position - hole.transform.position;
                away.y = 0f;
                Vector3 direction = away.sqrMagnitude > 1e-8f ? away.normalized : Vector3.forward;
                Vector3 edge = hole.transform.position + direction * (hole.RadiusToward(direction) + ball.Radius + 0.005f);
                ball.Rigidbody.linearVelocity = Vector3.zero;
                ball.PlaceAt(new Vector3(edge.x, ball.transform.position.y, edge.z));
                return;
            }
            if (ball.IsCueBall)
            {
                ball.OnPocketed();
                return;
            }
            ball.FallIntoTableHole(hole.transform.position, hole.Power != null ? hole.Power.SinkTime : 0.35f);
            ballsInTableHoles.Add(ball);
        }

        // Catch the Hole: balls a living hole potted during this shot (the 8
        // counts there without a call: a moving hole can't be called).
        private readonly HashSet<PoolBall> caughtByLivingHoleThisShot = new HashSet<PoolBall>();
        public bool WasCaughtByLivingHole(PoolBall ball) => caughtByLivingHoleThisShot.Contains(ball);

        // A living hole (Catch the Hole) reached a ball:
        //   not played (at rest, or between shots): it pops up on the nearest
        //   free spot, the hole came to it;
        //   rolling cue ball: a scratch;
        //   rolling 8 while the shooter isn't on it: back to its spot after
        //   the shot, like the Trou power;
        //   any other rolling ball: potted once it has sunk.
        private void HandleBallCaughtByLivingHole(PoolBall ball, TableHole hole)
        {
            float sinkTime = hole.Power != null ? hole.Power.SinkTime : 0.35f;
            string decision;
            // Read before the fall stops the ball. Speed Pool has no shot loop:
            // any rolling ball is in play.
            bool fellIn = (shotInProgress || IsSimultaneous) && ball.IsRolling;
            if (!fellIn)
            {
                Vector3 spot = FreeSpotNear(ball.transform.position, ball);
                ball.Rigidbody.linearVelocity = Vector3.zero;
                ball.Rigidbody.angularVelocity = Vector3.zero;
                ball.PlaceAt(new Vector3(spot.x, ball.transform.position.y, spot.z));
                decision = TableHole.Overlaps(spot, 0f) ? "non jouée, AUCUNE place libre : reste sur le trou" : "non jouée : réapparaît à côté";
            }
            else if (ball.IsCueBall)
            {
                ball.OnPocketed();
                decision = "blanche qui roule : faute";
            }
            else if (IsSimultaneous)
            {
                // Potted once sunk and judged then like any pot; nothing waits
                // for it (no shot to settle).
                ball.FallIntoTableHole(hole.transform.position, sinkTime, pocket: true, follow: hole.transform);
                decision = "empochée (Speed Pool)";
            }
            else if (ball.Group == BallGroup.Eight && !IsShootingForEightBall(CurrentPlayer))
            {
                ball.FallIntoTableHole(hole.transform.position, sinkTime, follow: hole.transform);
                ballsInTableHoles.Add(ball);
                decision = "la 8 avant son tour : revient à sa place après le tir";
            }
            else
            {
                ball.FallIntoTableHole(hole.transform.position, sinkTime, pocket: true, follow: hole.transform);
                // Listed so the shot waits for the end of the fall (AnyBallSinking).
                ballsInTableHoles.Add(ball);
                caughtByLivingHoleThisShot.Add(ball);
                decision = "empochée";
            }
            // Every ball that really fell in grows it (not one popped aside).
            if (fellIn) hole.Living.OnSwallowed(ball);

            if (hole.Living.Settings.debugLogs)
                Debug.Log($"[CatchTheHole] {hole.name} (rayon {hole.MaxRadius * 100f:0.0} cm) attrape {(ball.IsCueBall ? "la blanche" : $"la {ball.Number}")} : " +
                          $"vitesse {ball.HorizontalSpeed:0.000} m/s, {(fellIn ? "en jeu" : "entre deux tirs")} → {decision}.");
        }

        private bool AnyBallSinking()
        {
            foreach (PoolBall ball in ballsInTableHoles)
                if (ball != null && ball.IsSinking) return true;
            return false;
        }

        private void ReturnBallsFromTableHoles()
        {
            foreach (PoolBall ball in ballsInTableHoles)
            {
                if (ball == null || !ball.InTableHole) continue;
                Vector3 target = shotStartPositions.TryGetValue(ball, out Vector3 before) ? before : ball.transform.position;
                ball.ReturnFromTableHole(FreeSpotNear(target, ball));
            }
            ballsInTableHoles.Clear();
        }

        // target, or the nearest free spot around it: on the felt, away from
        // balls, holes and pockets.
        private static Vector3 FreeSpotNear(Vector3 target, PoolBall ball)
        {
            if (IsFreeSpot(target, ball)) return target;
            PoolTableSurface surface = PoolTableSurface.Instance;
            for (int ring = 1; ring <= 8; ring++)
            {
                float distance = ring * ball.Radius * 1.5f;
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2f / 12f;
                    Vector3 candidate = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (surface != null) candidate = surface.ClampToPlayArea(candidate, ball.Radius);
                    candidate = PoolPocket.AvoidAllPockets(candidate, ball.Radius);
                    candidate.y = target.y;
                    if (IsFreeSpot(candidate, ball)) return candidate;
                }
            }
            return target;
        }

        private static bool IsFreeSpot(Vector3 point, PoolBall ball)
        {
            if (TableHole.Overlaps(point, ball.Radius)) return false;
            // Hungry Hole: the hole is much wider than its hidden cue ball, and
            // a ball put back under it would be swallowed at once.
            foreach (HungryHoleCue cue in HungryHoleCue.All)
            {
                Vector3 offset = cue.transform.position - point;
                offset.y = 0f;
                float min = cue.Radius + ball.Radius * 2f;
                if (offset.sqrMagnitude < min * min) return false;
            }
            foreach (PoolBall other in PoolBall.Active)
            {
                if (other == ball || other.InTableHole) continue;
                float min = ball.Radius + other.Radius + 0.002f;
                if ((other.transform.position - point).sqrMagnitude < min * min) return false;
            }
            return true;
        }

        // Ends the holes whose time is up. CurrentPlayer is the player whose
        // turn is ending, when called from SwitchTurn.
        private void CloseTableHoles(bool turnEnding, bool shotResolved)
        {
            for (int i = tableHoles.Count - 1; i >= 0; i--)
            {
                TableHoleEntry entry = tableHoles[i];
                bool close = entry.hole == null;
                switch (entry.duration)
                {
                    case TableHoleDuration.OpponentTurn:
                        close |= turnEnding && CurrentPlayer != entry.owner;
                        break;
                    case TableHoleDuration.Turns:
                        if (turnEnding && --entry.turnsLeft <= 0) close = true;
                        break;
                    case TableHoleDuration.NextShot:
                        if (shotResolved)
                        {
                            if (entry.skipNextResolve) entry.skipNextResolve = false;
                            else close = true;
                        }
                        break;
                }
                if (!close) continue;
                if (entry.hole != null) entry.hole.Close(instant: false);
                tableHoles.RemoveAt(i);
            }
        }

        private void ClearTableHoles()
        {
            foreach (TableHoleEntry entry in tableHoles)
                if (entry.hole != null) entry.hole.Close(instant: true);
            tableHoles.Clear();
            ballsInTableHoles.Clear();
            foreach (PendingTableHoles pending in pendingTableHoles) DestroyMarkers(pending);
            pendingTableHoles.Clear();
        }

        // Pocket a ball of this shot fell into; null if it didn't, or fell
        // off the table.
        public PoolPocket GetPocketFor(PoolBall ball) =>
            pocketByBallThisShot.TryGetValue(ball, out PoolPocket pocket) ? pocket : null;

        // Pocket called for the next shot at the 8; null if none.
        private PoolPocket calledEightBallPocket;

        public PoolPocket CalledEightBallPocket => calledEightBallPocket;

        public void CallEightBallPocket(PoolPocket pocket) => calledEightBallPocket = pocket;

        // Read and cleared on every shot, so a call never carries over.
        public PoolPocket ConsumeCalledEightBallPocket()
        {
            PoolPocket pocket = calledEightBallPocket;
            calledEightBallPocket = null;
            return pocket;
        }

        // Group assigned and cleared (8-ball rules only).
        public bool IsShootingForEightBall(int player) =>
            ruleSet is EightBallRuleSet eightBall && eightBall.IsShootingForEightBall(player);

        // A pocket to call before aiming: the 8 (8-ball), or the pocket the
        // chain will pour into (Snake Pool, each player their own).
        public bool NeedsPocketCall(int player) => ruleSet is SnakePoolRuleSet snake
            ? snake.NeedsPocketCall(player)
            : calledEightBallPocket == null && IsShootingForEightBall(player);

        // Playing for a called pocket (HUD banner), called or not yet.
        public bool IsGoingForPocket(int player) => ruleSet is SnakePoolRuleSet snake
            ? snake.IsGoingForPocket(player)
            : IsShootingForEightBall(player);

        public PoolPocket CalledPocketOf(int player) => ruleSet is SnakePoolRuleSet snake ? snake.CalledPocket(player) : calledEightBallPocket;

        public void CallPocket(int player, PoolPocket pocket)
        {
            if (ruleSet is SnakePoolRuleSet snake) snake.CallPocket(player, pocket);
            else CallEightBallPocket(pocket);
        }

        // Test (C+W cheat): the current player's group counts as cleared,
        // next legal shot is the 8.
        public void DebugForceEightBallEndgame()
        {
            if (!MatchStarted || GameOver)
            {
                Debug.LogWarning("[EightBallEndgameCheat] No match in progress.");
                return;
            }

            if (!(ruleSet is EightBallRuleSet eightBall))
            {
                Debug.LogWarning("[EightBallEndgameCheat] Current mode isn't 8-ball.");
                return;
            }

            // Copy: deactivating a ball removes it from Active.
            foreach (PoolBall ball in new List<PoolBall>(PoolBall.Active))
            {
                if (ball.Group == BallGroup.Solid || ball.Group == BallGroup.Stripe)
                    ball.gameObject.SetActive(false);
            }

            eightBall.DebugAssignGroup(CurrentPlayer, BallGroup.Solid);
            Debug.Log($"[EightBallEndgameCheat] Joueur {CurrentPlayer + 1} peut maintenant tirer la bille 8.");
        }

        // ---------- Test-only entry points (DebugMenu) ----------
        // Same paths as real play, so they behave like the real thing.

        // A match is on and the table is still: the only time a fake shot
        // or a turn change makes sense.
        public bool DebugCanAct => MatchStarted && !GameOver && !shotInProgress && !PoolBall.AnyMoving();

        // Sets a power at that level: its own slot if already in stock, else a
        // free slot, else replaces the selected one. Null empties both.
        public void DebugSetHeldPower(int player, PoolPower power, int level = 1)
        {
            if (power == null)
            {
                for (int s = 0; s < PowerSlots; s++) { heldPowers[player, s] = null; heldLevels[player, s] = 1; }
                SelectSlot(player, 0);
                return;
            }
            int slot = -1;
            for (int s = 0; s < PowerSlots && slot < 0; s++) if (heldPowers[player, s] == power) slot = s;
            for (int s = 0; s < PowerSlots && slot < 0; s++) if (heldPowers[player, s] == null) slot = s;
            if (slot < 0) slot = selectedSlot[player];
            bool added = heldPowers[player, slot] != power;
            heldPowers[player, slot] = power;
            heldLevels[player, slot] = Mathf.Clamp(level, 1, MaxPowerLevel);
            SelectSlot(player, slot);
            PowerGranted?.Invoke(player);
            PowerPickedUp?.Invoke(player, power, added ? PowerPickup.Added : PowerPickup.LevelUp, heldLevels[player, slot]);
        }

        public void DebugSwitchTurn()
        {
            if (DebugCanAct) SwitchTurn();
        }

        // A foul committed by player: the turn passes to the other one, with
        // ball in hand — as after a scratch.
        public void DebugFoul(int player)
        {
            if (!DebugCanAct) return;
            if (CurrentPlayer != player) SwitchTurn();
            RegisterFoul();
            SwitchTurn();
        }

        // A shot judged by the real rule set: pocketed (may be null) leaves the
        // table, firstContact is what the cue ball touched (null = nothing).
        public void DebugResolveShot(PoolBall pocketed, PoolBall firstContact)
        {
            if (!DebugCanAct) return;
            pocketedThisShot.Clear();
            pocketByBallThisShot.Clear();
            cueBallPocketedThisShot = false;
            // Through OnPocketed: HandleBallPocketed records it (and plays the
            // pot feedback) exactly as for a real pot.
            if (pocketed != null) pocketed.OnPocketed();
            ruleSet.ResolveShot(this, pocketedThisShot, cueBallPocketedThisShot, firstContact);
        }

        private void Update()
        {
            if (!MatchStarted || GameOver)
            {
                // The players lock the cursor when they spawn; the menu and the
                // end screen need it. Every frame, in case a player spawns later.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (pendingSelection.HasValue)
            {
                selected = pendingSelection.Value;
                pendingSelection = null;
            }

            if (pendingStart)
            {
                pendingStart = false;
                StartMatch(selected);
            }

            if (pendingRestart)
            {
                pendingRestart = false;
                Restart();
            }

            if (!MatchStarted || GameOver) return;
            if (IsSimultaneous)
            {
                UpdatePlayerShots();
                return;
            }

            // A ball still sinking into a table hole counts as moving: the shot
            // is judged once it's gone, not halfway down.
            bool moving = PoolBall.AnyMoving() || AnyBallSinking();

            if (shotInProgress && !moving && wasMoving)
            {
                shotInProgress = false;
                EndMagnetShot();
                // Back on the table before the shot is judged: a ball that
                // fell in a table hole was never pocketed.
                ReturnBallsFromTableHoles();
                ruleSet.ResolveShot(this, pocketedThisShot, cueBallPocketedThisShot, firstContactThisShot);
                CloseTableHoles(turnEnding: false, shotResolved: true);
            }

            wasMoving = moving;
        }

        public void SwitchTurn()
        {
            // Turn-bound debuffs end with the turn (normally already done by
            // ExitAim).
            visionImpaired[CurrentPlayer] = false;
            invertedControls[CurrentPlayer] = false;
            // On hold since the player backed out of aim: the turn is over.
            visionImpairPending[CurrentPlayer] = false;
            invertedControlsPending[CurrentPlayer] = false;

            // Reopens when the opponent's turn ends, not when it begins.
            if (closedPocket != null && CurrentPlayer != closedPocketOwner)
            {
                closedPocket.SetClosed(false);
                closedPocket = null;
            }

            // The disguised balls get their looks back once the target's turn is over.
            if (identicalGroupsTarget == CurrentPlayer) EndIdenticalGroups();

            CloseTableHoles(turnEnding: true, shotResolved: false);

            CurrentPlayer = 1 - CurrentPlayer;

            LaunchPendingTableHoles();

            if (pendingIdenticalGroups != null && CurrentPlayer == pendingIdenticalGroupsTarget)
            {
                ApplyIdenticalGroups(pendingIdenticalGroupsTarget, pendingIdenticalGroups);
                pendingIdenticalGroups = null;
            }

            // Now that control leaves the launcher.
            if (pendingClosePocket != null && CurrentPlayer != pendingClosePocketOwner)
            {
                ApplyPocketClose(pendingClosePocketOwner, pendingClosePocket);
                pendingClosePocket = null;
            }
            TurnChanged?.Invoke(CurrentPlayer);
        }

        public void Win(int player)
        {
            // The end of the match shows the table as it really is.
            EndIdenticalGroups();
            pendingIdenticalGroups = null;
            GameOver = true;
            Winner = player;
            // The console's stack trace on this line shows what ended the match.
            if (traceShots) Debug.Log($"[Match] J{player + 1} gagne ({Mode}, frame {Time.frameCount}).", this);
        }

        // "Rejouer": every ball back to the rack, match state cleared, same
        // mode again.
        private void Restart()
        {
            if (traceShots) Debug.Log($"[Match] Rejouer ({Mode}, frame {Time.frameCount}).", this);
            // Include inactive: potted balls are inactive and not in Active.
            foreach (PoolBall ball in FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
                ball.ResetToSpawn();

            for (int p = 0; p < 2; p++)
            {
                for (int s = 0; s < PowerSlots; s++) { heldPowers[p, s] = null; heldLevels[p, s] = 1; }
                selectedSlot[p] = 0;
            }
            ClearTableHoles();
            MatchGeneration++;
            shotPowerMultiplier[0] = 1f;
            shotPowerMultiplier[1] = 1f;

            visionImpaired[0] = false;
            visionImpaired[1] = false;
            visionImpairPending[0] = false;
            visionImpairPending[1] = false;
            invertedControls[0] = false;
            invertedControls[1] = false;
            invertedControlsPending[0] = false;
            invertedControlsPending[1] = false;

            if (closedPocket != null) closedPocket.SetClosed(false);
            closedPocket = null;
            pendingClosePocket = null;
            EndIdenticalGroups();
            pendingIdenticalGroups = null;
            calledEightBallPocket = null;
            ballBlastArmed[0] = ballBlastArmed[1] = null;
            ballBlastThisShot = null;
            ballBlastRolling[0] = ballBlastRolling[1] = null;
            rollingCueBall[0] = rollingCueBall[1] = null;
            DisarmMagnet(0);
            DisarmMagnet(1);
            EndMagnetShot();

            pocketedThisShot.Clear();
            pocketByBallThisShot.Clear();
            caughtByLivingHoleThisShot.Clear();
            cueBallPocketedThisShot = false;
            firstContactThisShot = null;
            shotInProgress = false;
            wasMoving = false;

            CurrentPlayer = 0;
            GameOver = false;
            Winner = -1;

            StartMatch(Setup);
        }

        private void StartMatch(PoolMatchSetup setup)
        {
            Setup = setup.Normalized();
            ruleSet = CreateRuleSet(Setup);
            // Normal balls again if the last match was Hungry Hole.
            if (!(ruleSet is HungryHoleRuleSet)) HungryHoleRuleSet.Cleanup();
            // No chain left from a Snake Pool match (Snake Pool clears its own).
            if (!(ruleSet is SnakePoolRuleSet)) SnakePoolRuleSet.Cleanup();
            // No grime left from a Power Wash match (Power Wash makes its own).
            if (!(ruleSet is PowerWashPoolRuleSet)) PowerWashPoolRuleSet.Cleanup();
            ruleSet.Setup(this);
            MatchStarted = true;
            // The living holes open with the rack (Rejouer comes through here too).
            if (Mode == PoolGameMode.CatchTheHole) CatchTheHole.Ensure().Begin();
            else if (CatchTheHole.Instance != null) CatchTheHole.Instance.Clear();
            if (traceShots) Debug.Log($"[Match] Partie lancée : {Setup.Describe()} ({ruleSet.GetType().Name}), frame {Time.frameCount}.", this);

            // Hand cursor control back to normal FPS/aim look now that the menu is gone.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // setup is normalized: a type the mode supports. The powers don't
        // change the rules, the crates and the power ball add them.
        private static IPoolRuleSet CreateRuleSet(PoolMatchSetup setup) => setup.mode switch
        {
            PoolGameMode.NineBall => new NineBallRuleSet(),
            PoolGameMode.FourteenOne => new FourteenOneRuleSet(setup.targetScore),
            PoolGameMode.HungryHole => new HungryHoleRuleSet(),
            PoolGameMode.SnakePool => new SnakePoolRuleSet(),
            PoolGameMode.PowerWashPool => new PowerWashPoolRuleSet(),
            // 8-ball, and Catch the Hole (8-ball plus the living holes).
            _ => setup.type == PoolMatchType.SpeedPool ? new SpeedPoolRuleSet() : new EightBallRuleSet(),
        };

        private void OnGUI()
        {
            if (!MatchStarted)
            {
                if (!ExternalMenu) DrawModeSelectGUI();
                return;
            }

            if (ExternalHud) return;

            if (GameOver)
            {
                DrawGameOverGUI();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 320, 190));
            GUILayout.Label(IsSimultaneous ? "Speed Pool : tout le monde tire !" : $"Tour : Joueur {CurrentPlayer + 1}");
            GUILayout.Label($"Joueur 1 — {ruleSet.DescribePlayer(0)}");
            GUILayout.Label($"Joueur 2 — {ruleSet.DescribePlayer(1)}");
            GUILayout.Label($"Pouvoirs J1 : {DescribeStock(0)}");
            GUILayout.Label($"Pouvoirs J2 : {DescribeStock(1)}");
            if (BallInHand)
                GUILayout.Label($"Faute ! Joueur {CurrentPlayer + 1} a la main libre — regarde où placer la bille blanche et valide avec Interact.");

            // Status line only; the call itself is made in
            // LocalPoolAimController.HandleCallPocket.
            if (IsShootingForEightBall(CurrentPlayer))
            {
                GUILayout.Label(calledEightBallPocket != null
                    ? $"Poche appelée pour la bille 8 : {calledEightBallPocket.DescribeLocation()}"
                    : "Bille 8 : désigne une poche (vue du dessus) et valide avec Interact.");
            }
            GUILayout.EndArea();
        }

        // Big centered "match over" box.
        private void DrawGameOverGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 110f, 400f, 220f), GUI.skin.box);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

            GUILayout.Label("Partie terminée", titleStyle, GUILayout.Height(40));
            GUILayout.Label($"Joueur {Winner + 1} gagne !", titleStyle, GUILayout.Height(40));
            GUILayout.Label($"{ruleSet.DescribePlayer(0)}  —  {ruleSet.DescribePlayer(1)}", subtitleStyle);

            if (GUILayout.Button("Rejouer", GUILayout.Height(35))) pendingRestart = true;

            GUILayout.EndArea();
        }

        private void DrawModeSelectGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 190f, 300f, 380f), GUI.skin.box);

            GUILayout.Label("Type de partie");
            GUILayout.BeginHorizontal();
            DrawTypeButton(PoolMatchType.TurnBased);
            DrawTypeButton(PoolMatchType.SpeedPool);
            GUILayout.EndHorizontal();

            GUILayout.Label("Mode");
            DrawModeButton(PoolGameMode.EightBall, "Classique (8-ball)");
            DrawModeButton(PoolGameMode.NineBall, "9-ball");
            DrawModeButton(PoolGameMode.FourteenOne, "14.1 (score cible)");
            DrawModeButton(PoolGameMode.CatchTheHole, "Catch the Hole (trous vivants)");
            DrawModeButton(PoolGameMode.HungryHole, "Hungry Hole (la blanche est un trou)");
            DrawModeButton(PoolGameMode.SnakePool, "Snake Pool (chaîne de billes)");
            DrawModeButton(PoolGameMode.PowerWashPool, "Power Wash Pool (laver le tapis)");

            bool powers = GUILayout.Toggle(selected.powers, "Pouvoirs");
            if (powers != selected.powers) Select((ref PoolMatchSetup s) => s.powers = powers);

            if (selected.mode == PoolGameMode.FourteenOne)
            {
                GUILayout.Label("Score cible :");
                targetScoreInput = GUILayout.TextField(targetScoreInput);
            }

            if (GUILayout.Button("Commencer la partie"))
            {
                int targetScore = int.TryParse(targetScoreInput, out int parsed) && parsed > 0 ? parsed : 150;
                Select((ref PoolMatchSetup s) => s.targetScore = targetScore);
                pendingStart = true;
            }

            GUILayout.EndArea();
        }

        private delegate void SetupChange(ref PoolMatchSetup setup);

        // From the latest pending click, so two clicks in a frame both count.
        private void Select(SetupChange change)
        {
            PoolMatchSetup next = pendingSelection ?? selected;
            change(ref next);
            pendingSelection = next;
        }

        private void DrawTypeButton(PoolMatchType type)
        {
            GUI.enabled = PoolMatchSetup.Supports(selected.mode, type);
            GUI.color = selected.type == type ? Color.green : Color.white;
            if (GUILayout.Button(PoolMatchSetup.TypeName(type))) Select((ref PoolMatchSetup s) => s.type = type);
            GUI.color = Color.white;
            GUI.enabled = true;
        }

        // A new mode starts on its own type (PoolMatchSetup.NativeType).
        private void DrawModeButton(PoolGameMode mode, string label)
        {
            GUI.color = selected.mode == mode ? Color.green : Color.white;
            if (GUILayout.Button(label))
                Select((ref PoolMatchSetup s) => { s.mode = mode; s.type = PoolMatchSetup.NativeType(mode); });
            GUI.color = Color.white;
        }
    }
}
