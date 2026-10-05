using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // Hosts the shared shot-detection loop (wait for every ball to stop, then
    // resolve the shot) and the pre-match mode-select screen. The actual
    // rules for whichever PoolGameMode gets picked live in a separate
    // IPoolRuleSet (EightBallRuleSet/NineBallRuleSet/FourteenOneRuleSet) —
    // see that file for why. Deliberately a plain MonoBehaviour, not a
    // NetworkBehaviour and no "Local" variant needed: pool ball physics isn't
    // networked yet either (see TODO.md), so this only ever runs as a local
    // simulation regardless of online/offline mode.
    public class PoolMatchRules : MonoBehaviour
    {
        // One active match per scene — lets the aim controllers (which need to
        // check whose turn it is, and whether ball-in-hand placement is in
        // progress) find it without a hand-wired Inspector reference.
        public static PoolMatchRules Instance { get; private set; }

        public int CurrentPlayer { get; private set; }
        public bool GameOver { get; private set; }
        public int Winner { get; private set; } = -1;
        public bool MatchStarted { get; private set; }
        // The mode actually locked in when the match started (set by
        // StartMatch) — NOT the same as the pre-match UI's selectedMode
        // field below, which keeps changing while someone browses the
        // mode-select screen. PoolPowerCrateManager/PoolPowerBallRotator
        // read this to gate powers to Party only.
        public PoolGameMode Mode { get; private set; }

        // True from the moment a foul is registered until the fouled-against
        // player confirms where they've placed the cue ball — see
        // RegisterFoul()/ConfirmBallPlaced() and the aim controllers'
        // placement-mode handling.
        public bool BallInHand { get; private set; }

        private IPoolRuleSet ruleSet;

        // One held power per player at a time (Mario Kart-style — picking up
        // another one while already holding one overwrites it). See
        // PowerBall/PoolPowerCrate for how a power gets granted, and
        // LocalPoolPowerController for activation input.
        private readonly PoolPower[] heldPower = new PoolPower[2];
        // Consumed by the aim controllers' Shoot() — set by powers like
        // BoostedShotPower, reset back to 1 the moment it's read so it only
        // ever affects the very next shot.
        private readonly float[] shotPowerMultiplier = { 1f, 1f };

        private readonly List<PoolBall> pocketedThisShot = new List<PoolBall>();
        // Which pocket each ball in pocketedThisShot actually fell into —
        // needed by EightBallRuleSet to check a called shot against the
        // real pocket. Keyed by ball rather than kept as a parallel list so
        // GetPocketFor() stays a simple lookup regardless of iteration order.
        private readonly Dictionary<PoolBall, PoolPocket> pocketByBallThisShot = new Dictionary<PoolBall, PoolPocket>();
        private bool cueBallPocketedThisShot;
        private PoolBall firstContactThisShot;
        private bool wasMoving;
        // True only between an actual player-fired shot (NotifyShotFired) and
        // its resolution — without this, the balls settling under gravity
        // right after the table loads (a tiny bit of jostling as they drop
        // into their resting rack position) was itself read as "a shot
        // happened", and resolved as an instant foul (no contact) before
        // anyone had even taken their first shot.
        private bool shotInProgress;

        private PoolGameMode selectedMode = PoolGameMode.EightBall;
        private PoolPartyMode selectedPartyMode = PoolPartyMode.Classic;
        // Which mode-select screen is showing — the top-level list, or the
        // Party sub-mode list (see DrawPartySubmenuGUI). A UI navigation
        // flag, not match state, but it still needs the same deferred-pending
        // treatment below for the same GUILayout reason.
        private bool showingPartySubmenu;
        private string targetScoreInput = "150";

        // GUILayout draws OnGUI several times per frame (Layout pass, the
        // actual click event, then Repaint). Mutating selectedMode/MatchStarted
        // directly from a Button click changes which controls get drawn on a
        // LATER pass of that same frame than the earlier Layout pass already
        // computed — GUILayout's internal state gets out of sync and the
        // screen can get stuck instead of swapping cleanly. Deferring the
        // mutation to Update() keeps the control structure identical across
        // every OnGUI pass within a frame; it only changes starting next frame.
        private PoolGameMode? pendingModeChange;
        private PoolPartyMode? pendingPartyModeChange;
        private bool? pendingShowPartySubmenuChange;
        private bool pendingStart;
        private int pendingTargetScore;
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

        // Structured per-player state for the HUD (DescribePlayer is one
        // line of text): 8-ball group (Cue = not decided yet, or not an
        // 8-ball game), 14.1 score.
        public BallGroup GetGroup(int player) => ruleSet is EightBallRuleSet eightBall ? eightBall.GroupOf(player) : BallGroup.Cue;
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
        public void RequestStart(PoolGameMode mode, PoolPartyMode partyMode, int targetScore)
        {
            if (MatchStarted) return;
            pendingModeChange = mode;
            pendingPartyModeChange = partyMode;
            pendingTargetScore = targetScore > 0 ? targetScore : 150;
            pendingStart = true;
        }

        // Resources-loaded, shared with PoolPocket (same "what happens on a
        // pot" domain — see PoolPotEffectSettings) instead of private fields
        // here.
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

            // Owned here (not per-pocket, not per-player) because
            // Time.timeScale is a single global value — two independent
            // MonoBehaviours each starting/stopping their own coroutine for
            // it would race and could snap it back to 1 while another pot's
            // dip was still supposed to be running.
            TriggerPotSlowMotion();

            if (ball.IsCueBall)
            {
                cueBallPocketedThisShot = true;
                return;
            }
            pocketedThisShot.Add(ball);
            pocketByBallThisShot[ball] = pocket;

            // Independent of group/order rules — a power ball still counts
            // normally for whichever IPoolRuleSet is active, it just also
            // grants the shooter a power on top.
            if (ball.TryGetComponent(out PowerBall powerBall) && powerBall.Power != null)
                GrantPower(CurrentPlayer, powerBall.Power);
        }

        // Restarts the dip from full if another ball pockets while one is
        // already running (e.g. two balls potted on the same shot) rather
        // than stacking — a longer single dip reads better than the
        // timescale snapping back to 1 partway through and re-dropping.
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

        // Static, same convention as PoolBall.Pocketed/CueBallFirstContact —
        // lets each local player's own juice/feedback component subscribe
        // independently without needing a hand-wired reference to this
        // instance, and without caring about subscription-order timing
        // against Instance being set.
        public static event Action<int> PowerGranted;

        // Mario Kart-style single slot: a pickup while already holding a
        // power is wasted rather than replacing it — the crate/ball is
        // still consumed as normal (PoolPowerCrate disables itself and
        // notifies the manager regardless of this call's outcome), it just
        // doesn't change what's in hand. Called by PowerBall pickups,
        // PoolPowerCrate.
        public void GrantPower(int player, PoolPower power)
        {
            if (heldPower[player] != null) return;

            heldPower[player] = power;
            PowerGranted?.Invoke(player);
        }

        public PoolPower GetHeldPower(int player) => heldPower[player];

        // Returns false if the player wasn't holding anything.
        public bool TryActivatePower(int player)
        {
            PoolPower power = heldPower[player];
            if (power == null) return false;

            heldPower[player] = null;
            power.Activate(this, player);
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

        // Turn-bound debuff for an Attack-type power (see VisionImpairPower)
        // — whoever it's applied to gets a screen overlay and reduced look
        // sensitivity while it's active. Starts the moment they enter aim
        // mode (ConsumePendingVisionImpair, called from
        // LocalPoolAimController.EnterAim()) and is cleared the moment they
        // leave aim mode (EndVisionImpair, called from ExitAim()) — either by
        // taking the shot or backing out of it. SwitchTurn() also clears it
        // as a safety net for the case where a turn ends without an explicit
        // ExitAim() call. Previously a fixed-duration real-time timer, which
        // could tick out entirely before the opponent even got a turn, or
        // cut off mid-shot on a slow player. Tracked here (not on the
        // player's own controller) for the
        // same reason everything else in this class is: PoolMatchRules is
        // the one place that already knows "player 0" / "player 1" as stable
        // identities, and the affected player's own
        // LocalPoolPowerEffectReceiver just polls this every frame — no
        // direct reference from one player to another needed.
        private readonly bool[] visionImpaired = new bool[2];
        private readonly float[] visionImpairedSensitivity = new float[2];

        // Set on activation but NOT active yet — see QueueVisionImpair. It
        // would often go to waste (or feel completely disconnected from the
        // eventual shot) if it started counting down immediately, while it's
        // still the activating player's own turn.
        private readonly bool[] visionImpairPending = new bool[2];
        private readonly float[] pendingVisionImpairSensitivity = new float[2];

        // Called by the power on activation — queues the debuff instead of
        // starting it immediately. The affected player's own aim controller
        // calls ConsumePendingVisionImpair() the moment THEY actually enter
        // aim mode on their own turn, which is when it actually starts.
        // Off-turn activation (see PoolPower.RequiresOwnTurn) means it can
        // already BE the target's turn the moment this is called — waiting
        // for their next EnterAim() in that case could sit stale for the
        // rest of THIS shot (if they're already aiming) or even miss their
        // whole turn entirely, defeating the entire point of interrupting a
        // turn already in progress. So: if it's already their turn, apply
        // right now instead of queuing.
        public void QueueVisionImpair(int player, float sensitivityMultiplier)
        {
            if (CurrentPlayer == player)
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

        // Called from LocalPoolAimController.ExitAim() — ends the effect as
        // soon as the affected player leaves aim mode, rather than leaving
        // it running (flicker/shake/sensitivity cut) while they walk around
        // between shots for the rest of their turn.
        public void EndVisionImpair(int player) => visionImpaired[player] = false;

        public bool IsVisionImpaired(int player) => visionImpaired[player];

        // 1 while active, 0 otherwise — no fade anymore, since there's no
        // timer left to fade against; it's simply on for the player's whole
        // turn, then off.
        public float VisionImpairmentStrength(int player) => visionImpaired[player] ? 1f : 0f;

        public float GetVisionImpairmentSensitivityMultiplier(int player) =>
            visionImpaired[player] ? visionImpairedSensitivity[player] : 1f;

        // Second Attack-type turn-bound debuff (see InvertedControlsPower) —
        // same queue/consume/end lifecycle as Vision Impair above, just
        // inverting look (plus a sensitivity boost and hiding the
        // trajectory preview — see IsControlsInverted's call sites) instead
        // of blinding it. Kept as separate arrays rather than generalizing
        // the two into one "debuffs" system for now — with only two of
        // these so far, a shared abstraction would be guesswork about what
        // future debuffs actually need in common.
        private readonly bool[] invertedControls = new bool[2];
        private readonly float[] invertedControlsSensitivity = new float[2];

        private readonly bool[] invertedControlsPending = new bool[2];
        private readonly float[] pendingInvertedControlsSensitivity = new float[2];

        // Same reasoning as QueueVisionImpair above — off-turn activation
        // means it can already be the target's turn, so apply immediately
        // instead of queuing for an EnterAim() that might already have
        // happened (or not come again until much later).
        public void QueueInvertedControls(int player, float sensitivityMultiplier)
        {
            if (CurrentPlayer == player)
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

        // Also gates hiding the trajectory preview (LocalPoolAimController.
        // UpdateAim()) — no separate flag, it's the same "controls are
        // inverted" moment driving all three symptoms (look flip,
        // sensitivity boost, blind aiming) together.
        public bool IsControlsInverted(int player) => invertedControls[player];

        public float GetInvertedControlsSensitivityMultiplier(int player) =>
            invertedControls[player] ? invertedControlsSensitivity[player] : 1f;

        // Third Attack-type power (see ClosePocketPower) — a physical,
        // shared table effect rather than a per-player screen debuff, but
        // still turn-bound the same way: see QueueClosePocket below for
        // when it applies immediately vs. waits for SwitchTurn().
        private PoolPocket closedPocket;
        private int closedPocketOwner;
        private bool closePocketPending;
        private int pendingClosePocketOwner;

        // Called by ClosePocketPower.Activate. If it's already the OPPONENT's
        // turn (off-turn activation — see PoolPower.RequiresOwnTurn), there's
        // nothing left of the activator's own turn to protect, so it closes
        // right away instead of waiting for a SwitchTurn() that might not
        // come for a while (the opponent could have several shots left).
        // Otherwise (activated during the activator's own turn), the actual
        // pocket pick happens later, in SwitchTurn(), once their turn is
        // genuinely over — closing it immediately here would eat into their
        // OWN remaining shots instead of only hindering the opponent about
        // to receive it.
        public void QueueClosePocket(int activatingPlayer)
        {
            if (CurrentPlayer != activatingPlayer)
            {
                ApplyRandomPocketClose(activatingPlayer);
                return;
            }

            closePocketPending = true;
            pendingClosePocketOwner = activatingPlayer;
        }

        // Picks a random currently-open pocket and closes it (see
        // PoolPocket.SetClosed) — stays closed through the activating
        // player's opponent's ENTIRE turn, reopened by SwitchTurn() once
        // play actually comes back around to the activator.
        private void ApplyRandomPocketClose(int owner)
        {
            closedPocket?.SetClosed(false);
            closedPocket = null;

            IReadOnlyList<PoolPocket> pockets = PoolPocket.Active;
            if (pockets.Count == 0) return;

            PoolPocket chosen = pockets[UnityEngine.Random.Range(0, pockets.Count)];
            chosen.SetClosed(true);
            closedPocket = chosen;
            closedPocketOwner = owner;
        }

        // In split-screen (2 local PlayerInputs), a physical player always IS
        // the same index throughout — returns it unchanged. In hot-seat solo
        // testing (1 local PlayerInput, PlayerInput.playerIndex always 0),
        // that one screen represents whichever side is CURRENTLY up instead —
        // returns CurrentPlayer so per-player state (activating a power,
        // receiving a debuff meant for "the opponent") resolves against
        // whoever the single player is standing in for right now, rather than
        // being permanently locked to slot 0.
        public int GetEffectivePlayerIndex(int physicalPlayerIndex) =>
            PlayerInput.all.Count <= 1 ? CurrentPlayer : physicalPlayerIndex;

        private void HandleFirstContact(PoolBall cueBall, PoolBall other)
        {
            if (!MatchStarted) return;
            firstContactThisShot = other;

            // Ball Blast armed for this shot: the touched ball explodes and
            // leaves the table as if pocketed by the shooter — the rule set
            // then judges it like any pot (own ball: turn kept; wrong ball:
            // illegal first contact, so a foul, and the ball is gone anyway).
            BallBlastPower blast = ballBlastThisShot;
            ballBlastThisShot = null;
            if (blast == null) return;
            if (!blast.CanBlast(other))
            {
                Debug.Log($"[BallBlast] Joueur {CurrentPlayer + 1} : {other.name} épargnée (bille 8) — pouvoir perdu.");
                return;
            }
            blast.PlayEffects(other);
            BallBlasted?.Invoke(other, CurrentPlayer);
            other.OnPocketed();
        }

        // Ball Blast (BallBlastPower): armed by the power, it waits for the
        // activating player's next shot, which consumes it (NotifyShotFired)
        // whatever that shot touches.
        private readonly BallBlastPower[] ballBlastArmed = new BallBlastPower[2];
        private BallBlastPower ballBlastThisShot;

        public void ArmBallBlast(int player, BallBlastPower power) => ballBlastArmed[player] = power;
        public bool IsBallBlastArmed(int player) => ballBlastArmed[player] != null;

        // A ball destroyed by Ball Blast (before it's removed): the ball and
        // the shooter. For the explosion's HUD shout.
        public static event Action<PoolBall, int> BallBlasted;

        // Whether playerIndex (0 or 1) is currently allowed to shoot — used by
        // the offline aim controllers (PlayerInput.playerIndex identifies which
        // player is which) to stop the player who isn't up from grabbing the
        // cue or shooting. Not yet enforced online — see TODO.md.
        //
        // Falls back to "always allowed" when at most one local PlayerInput is
        // actually active (no split-screen P2 spawned) — otherwise a solo
        // player testing/playing 2-player rules alone would get completely
        // locked out the moment CurrentPlayer switches to "player 2", since
        // there's no second controller around to ever take that turn. With a
        // real P2 present, turns are enforced normally.
        public bool CanPlayerShoot(int playerIndex) =>
            MatchStarted && !GameOver && (CurrentPlayer == playerIndex || PlayerInput.all.Count <= 1);

        // Static, same convention as PowerGranted above — fired before
        // SwitchTurn(), so CurrentPlayer at invocation time is still the
        // player who committed the foul, not who it's rebounding to.
        public static event Action Fouled;

        // Called by a rule set from ResolveShot whenever the shot was a foul
        // (wrong ball hit first, no contact at all, or a scratch) — parks the
        // cue ball in a pickup-able state for the player who now has the turn.
        public void RegisterFoul()
        {
            BallInHand = true;
            PoolBall.FindCueBall()?.BeginBallInHand();
            Fouled?.Invoke();
        }

        // Called by the aim controller once the player has confirmed the cue
        // ball's new position.
        public void ConfirmBallPlaced() => BallInHand = false;

        // Called by the aim controllers right when a shot is actually struck —
        // this (not ball movement alone) is what defines "a shot happened", so
        // physics settling never gets mistaken for one.
        public void NotifyShotFired()
        {
            pocketedThisShot.Clear();
            pocketByBallThisShot.Clear();
            cueBallPocketedThisShot = false;
            firstContactThisShot = null;
            shotInProgress = true;

            ballBlastThisShot = ballBlastArmed[CurrentPlayer];
            ballBlastArmed[CurrentPlayer] = null;
        }

        // Which pocket a ball pocketed THIS shot actually fell into — null if
        // ball wasn't pocketed this shot, or fell off the table instead of
        // going in a pocket (see PoolBall.FixedUpdate's off-table check).
        public PoolPocket GetPocketFor(PoolBall ball) =>
            pocketByBallThisShot.TryGetValue(ball, out PoolPocket pocket) ? pocket : null;

        // 8-ball call-shot: which pocket the current player has declared for
        // their next attempt at the 8 (see EightBallRuleSet.ResolveShot).
        // Null means no call is in effect. Set directly by the aim
        // controllers' top-down call-pocket view (HandleCallPocket) when the
        // player confirms — no deferred-mutation dance needed here (unlike
        // pendingModeChange etc. below) since that confirmation happens in
        // Update(), not from an OnGUI button click.
        private PoolPocket calledEightBallPocket;

        public PoolPocket CalledEightBallPocket => calledEightBallPocket;

        public void CallEightBallPocket(PoolPocket pocket) => calledEightBallPocket = pocket;

        // Read-and-clear, same convention as ConsumeShotPowerMultiplier — a
        // call only ever covers the very next shot resolution. EightBallRuleSet
        // consumes this on EVERY shot (not just ones that pocket the 8), so a
        // call made ahead of a shot that turns out not to be the 8 is simply
        // discarded rather than lingering into a later, unrelated attempt.
        public PoolPocket ConsumeCalledEightBallPocket()
        {
            PoolPocket pocket = calledEightBallPocket;
            calledEightBallPocket = null;
            return pocket;
        }

        // Whether player is currently eligible to shoot at the 8 (group
        // assigned and fully cleared) — only EightBallRuleSet (and Party's
        // Classic sub-mode, which reuses it) has this concept, hence the
        // type check rather than a method on IPoolRuleSet every mode would
        // need to implement for no reason.
        public bool IsShootingForEightBall(int player) =>
            ruleSet is EightBallRuleSet eightBall && eightBall.IsShootingForEightBall(player);

        // Test-only: skips straight to "the current player's group is
        // cleared, next legal shot is the 8" without having to actually pot
        // 7 balls first — lets the 8-ball win condition / call-shot flow be
        // tested on demand. Triggered by the C+W cheat, see
        // Assets/Scripts/Core/EightBallEndgameCheat.cs (same idea as
        // SplitScreenCheatSpawner's C+P).
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

            // Snapshot first — deactivating a ball removes it from
            // PoolBall.Active via OnDisable, which would otherwise modify
            // the list out from under this same foreach.
            foreach (PoolBall ball in new List<PoolBall>(PoolBall.Active))
            {
                if (ball.Group == BallGroup.Solid || ball.Group == BallGroup.Stripe)
                    ball.gameObject.SetActive(false);
            }

            eightBall.DebugAssignGroup(CurrentPlayer, BallGroup.Solid);
            Debug.Log($"[EightBallEndgameCheat] Joueur {CurrentPlayer + 1} peut maintenant tirer la bille 8.");
        }

        private void Update()
        {
            if (!MatchStarted || GameOver)
            {
                // LocalFpsPlayerController locks and hides the
                // cursor as soon as the player spawns, which happens before this
                // menu is even shown — without this, the cursor stays pinned to
                // the screen center and none of the buttons below are reachable.
                // Asserted every frame (not just once) in case a player spawns
                // after this screen is already up and re-locks it. Also applies
                // once GameOver — otherwise the "Rejouer" button on the
                // game-over screen would be unclickable (cursor still locked
                // to screen center from normal gameplay).
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (pendingModeChange.HasValue)
            {
                selectedMode = pendingModeChange.Value;
                pendingModeChange = null;
            }

            if (pendingPartyModeChange.HasValue)
            {
                selectedPartyMode = pendingPartyModeChange.Value;
                pendingPartyModeChange = null;
            }

            if (pendingShowPartySubmenuChange.HasValue)
            {
                showingPartySubmenu = pendingShowPartySubmenuChange.Value;
                pendingShowPartySubmenuChange = null;
            }

            if (pendingStart)
            {
                pendingStart = false;
                StartMatch(selectedMode, selectedPartyMode, pendingTargetScore);
            }

            if (pendingRestart)
            {
                pendingRestart = false;
                Restart();
            }

            if (!MatchStarted || GameOver) return;

            bool moving = PoolBall.AnyMoving();

            if (shotInProgress && !moving && wasMoving)
            {
                shotInProgress = false;
                ruleSet.ResolveShot(this, pocketedThisShot, cueBallPocketedThisShot, firstContactThisShot);
            }

            wasMoving = moving;
        }

        public void SwitchTurn()
        {
            // Any turn-bound debuff against the player finishing their turn
            // ends with it — see VisionImpairPower/InvertedControlsPower:
            // meant to last exactly as long as the affected player holds the
            // cue, not a fixed real-time duration. (Normally already cleared
            // by ExitAim() calling EndVisionImpair/EndInvertedControls —
            // this is the safety net for a turn ending without one.)
            visionImpaired[CurrentPlayer] = false;
            invertedControls[CurrentPlayer] = false;

            // Reopens once the CLOSING player's opponent's turn actually
            // ends (CurrentPlayer here is whoever's turn is finishing) —
            // not the instant that turn begins — so ClosePocketPower covers
            // the opponent's whole turn instead of vanishing before their
            // first shot.
            if (closedPocket != null && CurrentPlayer != closedPocketOwner)
            {
                closedPocket.SetClosed(false);
                closedPocket = null;
            }

            CurrentPlayer = 1 - CurrentPlayer;

            // Applied right here, the moment control actually leaves the
            // activator — not in QueueClosePocket()/Activate(), which could
            // still be mid-way through the activator's own turn (never
            // eats into their own remaining shots).
            if (closePocketPending && CurrentPlayer != pendingClosePocketOwner)
            {
                closePocketPending = false;
                ApplyRandomPocketClose(pendingClosePocketOwner);
            }
            TurnChanged?.Invoke(CurrentPlayer);
        }

        public void Win(int player)
        {
            GameOver = true;
            Winner = player;
        }

        // "Rejouer" on the game-over screen — resets every ball back to its
        // rack position (PoolBall.ResetToSpawn, works whether a ball is
        // still active or was pocketed/deactivated) and clears all
        // per-match state, then starts a fresh match with the same
        // mode/settings as the one that just ended. Mode/selectedPartyMode
        // are reused rather than re-shown on a menu — selectedPartyMode
        // never changes once a match starts (the mode-select screen is
        // gone), and Mode is literally "whichever mode actually got locked
        // in" (see its own doc comment above), so both already hold exactly
        // what was just played.
        private void Restart()
        {
            // FindObjectsInactive.Include: a pocketed ball is an inactive
            // GameObject, and PoolBall.Active (OnEnable/OnDisable-driven)
            // wouldn't include it — this needs every ball regardless.
            foreach (PoolBall ball in FindObjectsByType<PoolBall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                ball.ResetToSpawn();

            heldPower[0] = null;
            heldPower[1] = null;
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

            closedPocket?.SetClosed(false);
            closedPocket = null;
            closePocketPending = false;
            calledEightBallPocket = null;
            ballBlastArmed[0] = ballBlastArmed[1] = null;
            ballBlastThisShot = null;

            pocketedThisShot.Clear();
            pocketByBallThisShot.Clear();
            cueBallPocketedThisShot = false;
            firstContactThisShot = null;
            shotInProgress = false;
            wasMoving = false;

            CurrentPlayer = 0;
            GameOver = false;
            Winner = -1;

            int targetScore = int.TryParse(targetScoreInput, out int parsed) && parsed > 0 ? parsed : 150;
            StartMatch(Mode, selectedPartyMode, targetScore);
        }

        private void StartMatch(PoolGameMode mode, PoolPartyMode partyMode, int targetScore)
        {
            Mode = mode;
            ruleSet = mode switch
            {
                PoolGameMode.NineBall => new NineBallRuleSet(),
                PoolGameMode.FourteenOne => new FourteenOneRuleSet(targetScore),
                PoolGameMode.Party => CreatePartyRuleSet(partyMode),
                _ => new EightBallRuleSet(),
            };
            ruleSet.Setup(this);
            MatchStarted = true;

            // Hand cursor control back to normal FPS/aim look now that the menu is gone.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // One case per PoolPartyMode — add a new one here (and its own
        // IPoolRuleSet if it needs different base rules) alongside a new
        // enum value and a button in DrawPartySubmenuGUI.
        private static IPoolRuleSet CreatePartyRuleSet(PoolPartyMode partyMode) => partyMode switch
        {
            // Classic: plain 8-ball rules — the powers themselves come from
            // PoolPowerCrate/PowerBall/the two spawn managers, not from the
            // ruleset, so no dedicated IPoolRuleSet is needed for this one.
            _ => new EightBallRuleSet(),
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
            GUILayout.Label($"Tour : Joueur {CurrentPlayer + 1}");
            GUILayout.Label($"Joueur 1 — {ruleSet.DescribePlayer(0)}");
            GUILayout.Label($"Joueur 2 — {ruleSet.DescribePlayer(1)}");
            GUILayout.Label($"Pouvoir J1 : {(heldPower[0] != null ? heldPower[0].PowerName : "—")}");
            GUILayout.Label($"Pouvoir J2 : {(heldPower[1] != null ? heldPower[1].PowerName : "—")}");
            if (BallInHand)
                GUILayout.Label($"Faute ! Joueur {CurrentPlayer + 1} a la main libre — regarde où placer la bille blanche et valide avec Interact.");

            // 8-ball call-shot: once a player's group is cleared, potting the
            // 8 in an uncalled (or wrong) pocket doesn't win — see
            // EightBallRuleSet.ResolveShot. The actual declaring happens in
            // a top-down table view (LocalPoolAimController's
            // HandleCallPocket, mirrors ball-in-hand placement) rather than
            // buttons here — this is just a status line so it's clear from
            // the regular HUD whether a call is still needed.
            if (IsShootingForEightBall(CurrentPlayer))
            {
                GUILayout.Label(calledEightBallPocket != null
                    ? $"Poche appelée pour la bille 8 : {calledEightBallPocket.DescribeLocation()}"
                    : "Bille 8 : désigne une poche (vue du dessus) et valide avec Interact.");
            }
            GUILayout.EndArea();
        }

        // Centered, large "match over" message — replaces the small
        // top-left label the rest of the HUD uses, since this is the one
        // moment worth actually stopping to read clearly.
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
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 150f, 300f, 300f), GUI.skin.box);

            if (showingPartySubmenu)
            {
                DrawPartySubmenuGUI();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("Choisir les règles de la partie");

            DrawModeButton(PoolGameMode.EightBall, "8-Ball");
            DrawModeButton(PoolGameMode.NineBall, "9-Ball");
            DrawModeButton(PoolGameMode.FourteenOne, "14.1 (score cible)");

            // Party doesn't select immediately like the others above — it
            // opens a second screen (DrawPartySubmenuGUI) listing whichever
            // party sub-modes exist, since Party is meant to grow into
            // several distinct variants over time rather than the top-level
            // list itself growing with them.
            GUI.color = selectedMode == PoolGameMode.Party ? Color.green : Color.white;
            string partyLabel = selectedMode == PoolGameMode.Party
                ? $"Party — {PartyModeLabel(selectedPartyMode)}"
                : "Party";
            if (GUILayout.Button(partyLabel)) pendingShowPartySubmenuChange = true;
            GUI.color = Color.white;

            if (selectedMode == PoolGameMode.FourteenOne)
            {
                GUILayout.Label("Score cible :");
                targetScoreInput = GUILayout.TextField(targetScoreInput);
            }

            if (GUILayout.Button("Commencer la partie"))
            {
                if (!int.TryParse(targetScoreInput, out int targetScore) || targetScore <= 0)
                    targetScore = 150;
                pendingTargetScore = targetScore;
                pendingStart = true;
            }

            GUILayout.EndArea();
        }

        private void DrawPartySubmenuGUI()
        {
            GUILayout.Label("Choisir un mode Party");

            DrawPartyModeButton(PoolPartyMode.Classic, "Classic (8-ball + pouvoirs)");
            // Add a button here for each new PoolPartyMode as it's implemented.

            if (GUILayout.Button("Retour")) pendingShowPartySubmenuChange = false;
        }

        private void DrawModeButton(PoolGameMode mode, string label)
        {
            GUI.color = selectedMode == mode ? Color.green : Color.white;
            if (GUILayout.Button(label)) pendingModeChange = mode;
            GUI.color = Color.white;
        }

        private void DrawPartyModeButton(PoolPartyMode partyMode, string label)
        {
            GUI.color = selectedPartyMode == partyMode ? Color.green : Color.white;
            if (GUILayout.Button(label))
            {
                pendingPartyModeChange = partyMode;
                pendingModeChange = PoolGameMode.Party;
                pendingShowPartySubmenuChange = false;
            }
            GUI.color = Color.white;
        }

        private static string PartyModeLabel(PoolPartyMode partyMode) => partyMode switch
        {
            PoolPartyMode.Classic => "Classic",
            _ => partyMode.ToString(),
        };
    }
}
