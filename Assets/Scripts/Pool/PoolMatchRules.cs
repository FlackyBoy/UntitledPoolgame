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

        // True from the moment a foul is registered until the fouled-against
        // player confirms where they've placed the cue ball — see
        // RegisterFoul()/ConfirmBallPlaced() and the aim controllers'
        // placement-mode handling.
        public bool BallInHand { get; private set; }

        private IPoolRuleSet ruleSet;

        // One held power per player at a time (Mario Kart-style — picking up
        // another one while already holding one overwrites it). See
        // PowerBall/PoolPowerCrate for how a power gets granted, and
        // PoolPowerController/LocalPoolPowerController for activation input.
        private readonly PoolPower[] heldPower = new PoolPower[2];
        // Consumed by the aim controllers' Shoot() — set by powers like
        // BoostedShotPower, reset back to 1 the moment it's read so it only
        // ever affects the very next shot.
        private readonly float[] shotPowerMultiplier = { 1f, 1f };

        private readonly List<PoolBall> pocketedThisShot = new List<PoolBall>();
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
        private bool pendingStart;
        private int pendingTargetScore;

        // Resources-loaded, shared with PoolPocket (same "what happens on a
        // pot" domain — see PoolPotEffectSettings) instead of private fields
        // here.
        private static PoolPotEffectSettings potEffectSettings;

        private void Awake()
        {
            Instance = this;

            if (potEffectSettings == null)
            {
                potEffectSettings = Resources.Load<PoolPotEffectSettings>("PoolPotEffectSettings");
                if (potEffectSettings == null)
                {
                    Debug.LogWarning("PoolPotEffectSettings asset not found in Assets/Resources — using fallback defaults. Run Tools > Pool > Ensure Config Assets Exist to create it.");
                    potEffectSettings = ScriptableObject.CreateInstance<PoolPotEffectSettings>();
                }
            }
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

        private void HandleBallPocketed(PoolBall ball)
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

        // Overwrites whatever the player was already holding (Mario
        // Kart-style single slot) — called by PowerBall pickups, PoolPowerCrate.
        public void GrantPower(int player, PoolPower power)
        {
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
        public void QueueVisionImpair(int player, float sensitivityMultiplier)
        {
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

        public void QueueInvertedControls(int player, float sensitivityMultiplier)
        {
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
        }

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
            cueBallPocketedThisShot = false;
            firstContactThisShot = null;
            shotInProgress = true;
        }

        private void Update()
        {
            if (!MatchStarted)
            {
                // FpsPlayerController/LocalFpsPlayerController lock and hide the
                // cursor as soon as the player spawns, which happens before this
                // menu is even shown — without this, the cursor stays pinned to
                // the screen center and none of the buttons below are reachable.
                // Asserted every frame (not just once) in case a player spawns
                // after this screen is already up and re-locks it.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (pendingModeChange.HasValue)
            {
                selectedMode = pendingModeChange.Value;
                pendingModeChange = null;
            }

            if (pendingStart)
            {
                pendingStart = false;
                StartMatch(selectedMode, pendingTargetScore);
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
            CurrentPlayer = 1 - CurrentPlayer;
        }

        public void Win(int player)
        {
            GameOver = true;
            Winner = player;
        }

        private void StartMatch(PoolGameMode mode, int targetScore)
        {
            ruleSet = mode switch
            {
                PoolGameMode.NineBall => new NineBallRuleSet(),
                PoolGameMode.FourteenOne => new FourteenOneRuleSet(targetScore),
                // EightBall and Party (no powers defined yet) share the same rules for now.
                _ => new EightBallRuleSet(),
            };
            ruleSet.Setup(this);
            MatchStarted = true;

            // Hand cursor control back to normal FPS/aim look now that the menu is gone.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (!MatchStarted)
            {
                DrawModeSelectGUI();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            if (GameOver)
            {
                GUILayout.Label($"Partie terminée — Joueur {Winner + 1} gagne !");
            }
            else
            {
                GUILayout.Label($"Tour : Joueur {CurrentPlayer + 1}");
                GUILayout.Label($"Joueur 1 — {ruleSet.DescribePlayer(0)}");
                GUILayout.Label($"Joueur 2 — {ruleSet.DescribePlayer(1)}");
                GUILayout.Label($"Pouvoir J1 : {(heldPower[0] != null ? heldPower[0].PowerName : "—")}");
                GUILayout.Label($"Pouvoir J2 : {(heldPower[1] != null ? heldPower[1].PowerName : "—")}");
                if (BallInHand)
                    GUILayout.Label($"Faute ! Joueur {CurrentPlayer + 1} a la main libre — regarde où placer la bille blanche et valide avec Interact.");
            }
            GUILayout.EndArea();
        }

        private void DrawModeSelectGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 130f, 300f, 260f), GUI.skin.box);
            GUILayout.Label("Choisir les règles de la partie");

            DrawModeButton(PoolGameMode.EightBall, "8-Ball");
            DrawModeButton(PoolGameMode.NineBall, "9-Ball");
            DrawModeButton(PoolGameMode.FourteenOne, "14.1 (score cible)");
            DrawModeButton(PoolGameMode.Party, "Party (pouvoirs à venir)");

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

        private void DrawModeButton(PoolGameMode mode, string label)
        {
            GUI.color = selectedMode == mode ? Color.green : Color.white;
            if (GUILayout.Button(label)) pendingModeChange = mode;
            GUI.color = Color.white;
        }
    }
}
