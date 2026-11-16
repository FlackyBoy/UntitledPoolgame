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
    // Split by concern: PoolMatchRules.Powers, .TableHoles, .Debug and
    // .LegacyGui (the provisional OnGUI).
    public partial class PoolMatchRules : MonoBehaviour
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

        // Shot strength multiplier of the mode: Play Anywhere is played across
        // the room, not on a table; Pocket Size's Rapid Shot power.
        public float ShotPowerScale => ruleSet switch
        {
            PlayAnywhereRuleSet golf => golf.ShotPowerScale,
            PocketSizeRuleSet pocketSize => pocketSize.ShotPowerScale,
            _ => 1f,
        };

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
        private PoolMatchSetup? restartSetup;

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
            if (!GameOver) return;
            restartSetup = null;
            pendingRestart = true;
        }

        // Asked, applied in the next Update: the end card stays hidden meanwhile.
        public bool RestartPending => pendingRestart;

        // The next match with other choices (Boogie Night's wheel), through
        // the same path: the table is reset, then set up for the new mode.
        public void RequestRestart(PoolMatchSetup setup)
        {
            if (!GameOver) return;
            restartSetup = setup;
            pendingRestart = true;
        }

        // Raised once a match is set up (the first one, and each "Rejouer"),
        // Setup already holds its choices: the crates and the power ball
        // follow its powers.
        public static event Action MatchBegan;

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
            // Play Anywhere: strokes over the hole's par.
            if (ruleSet is PlayAnywhereRuleSet golf)
            {
                score = golf.StrokesOf(player);
                target = golf.Par;
                return true;
            }
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
                // Play Anywhere brings its ball back itself (where it was struck from),
                // Sumo Pool with the next round.
                if (ball.IsCueBall && !(ownCueBalls is PlayAnywhereRuleSet || ownCueBalls is SumoPoolRuleSet)) StartCoroutine(RespawnCueBall(ball, MatchGeneration));
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
                // Play Anywhere brings its ball back itself (where it was struck from),
                // Sumo Pool with the next round.
                if (ball.IsCueBall && !(ownCueBalls is PlayAnywhereRuleSet || ownCueBalls is SumoPoolRuleSet)) StartCoroutine(RespawnCueBall(ball, MatchGeneration));
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

        // target, or the nearest free spot on the felt around it.
        public static Vector3 FindFreeSpot(Vector3 target, PoolBall ball) => FreeSpotNear(target, ball);

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
            // Speed Pool: good when the shooter who sent it in (PotOwner) owns it.
            if (IsSimultaneous) return SpeedPoolRuleSet.OwnerOf(ball) == PotOwner(ball) ? PotMeaning.Good : PotMeaning.Opponent;
            if (IsOwnBall(CurrentPlayer, ball)) return PotMeaning.Good;
            // Own cue balls in turns: the 8 never loses on the spot there.
            if (HasPlayerCueBalls) return PotMeaning.Opponent;
            return ball.Group == BallGroup.Eight ? PotMeaning.EarlyEight : PotMeaning.Opponent;
        }

        // Who a pot is about, for the HUD's shout: the shooter. Speed Pool:
        // the owner of a cue ball; for another ball, whoever's shot sent it
        // in (the ball's group owner if nobody's shot touched it).
        public int PotOwner(PoolBall ball)
        {
            if (!IsSimultaneous) return CurrentPlayer;
            if (ball.IsCueBall) return Mathf.Max(0, ball.CueOwner);
            return ball.LastShooter >= 0 ? ball.LastShooter : Mathf.Max(0, SpeedPoolRuleSet.OwnerOf(ball));
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

        // Whether this player may shoot (offline only for now). With at most
        // one active PlayerInput, always true: a solo player would otherwise
        // be locked out on player 2's turn.
        // Play Anywhere also needs the shooting phase and a stroke left this
        // round (for the player whose ball it is: alone, the current one).
        // Pocket Size: only the giant, only while hunting.
        // Hide and seek: only the seeker, while they have shots left.
        // Shooter miniature: nobody, the shots are the rifle's.
        // Bowling Pool: between throws, not while the pins fall.
        public bool CanPlayerShoot(int playerIndex) =>
            MatchStarted && !GameOver && (IsSimultaneous || CurrentPlayer == playerIndex || PlayerInput.all.Count <= 1)
            && (!(ruleSet is PlayAnywhereRuleSet golf) || golf.CanShoot(IsSimultaneous || PlayerInput.all.Count > 1 ? playerIndex : CurrentPlayer))
            && (!(ruleSet is PocketSizeRuleSet pocketSize) || pocketSize.CanShoot(playerIndex))
            && (!(ruleSet is HideAndSeekRuleSet hideAndSeek) || hideAndSeek.CanShoot(playerIndex))
            && !(ruleSet is ShooterMiniRuleSet) && !(ruleSet is SumoPoolRuleSet)
            && (!(ruleSet is BowlingPoolRuleSet bowling) || bowling.CanShoot);

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
            // Every stroke counts, in turns or not.
            if (ruleSet is PlayAnywhereRuleSet golf) golf.OnStroke(cueBall);
            if (ruleSet is BowlingPoolRuleSet bowling) bowling.OnThrow();
            if (ruleSet is HideAndSeekRuleSet hideAndSeek) hideAndSeek.OnShot();

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
            // The cup and out of bounds, checked every frame.
            if (ruleSet is PlayAnywhereRuleSet golfHole) golfHole.Tick(this);
            // Rounds, touches and falls of Pocket Size.
            else if (ruleSet is PocketSizeRuleSet pocketSizeRounds) pocketSizeRounds.Tick(this);
            // Health, hits and eliminations of the Shooter miniature.
            else if (ruleSet is ShooterMiniRuleSet shooterMini) shooterMini.Tick(this);
            // Pins off the table, and the sweep after each throw.
            else if (ruleSet is BowlingPoolRuleSet bowlingPins) bowlingPins.Tick(this);
            // Rounds and falls of Sumo Pool.
            else if (ruleSet is SumoPoolRuleSet sumo) sumo.Tick(this);
            // Hiding, the search, the decoys and the rounds of Hide and seek.
            else if (ruleSet is HideAndSeekRuleSet hideAndSeek) hideAndSeek.Tick(this);
            if (IsSimultaneous)
            {
                UpdatePlayerShots();
                return;
            }

            // A ball still sinking into a table hole counts as moving: the shot
            // is judged once it's gone, not halfway down.
            // So does a pin still falling.
            bool moving = PoolBall.AnyMoving() || AnyBallSinking() || (ruleSet is BowlingPoolRuleSet bowling && bowling.PinsMoving);

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

            ClearHeldPowers();
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

            PoolMatchSetup next = restartSetup ?? Setup;
            restartSetup = null;
            StartMatch(next);
        }

        private void StartMatch(PoolMatchSetup setup)
        {
            Setup = setup.Normalized();
            ruleSet = CreateRuleSet(Setup);
            meleeUntil[0] = meleeUntil[1] = 0f;
            // Normal balls again if the last match was Hungry Hole.
            if (!(ruleSet is HungryHoleRuleSet)) HungryHoleRuleSet.Cleanup();
            // No chain left from a Snake Pool match (Snake Pool clears its own).
            if (!(ruleSet is SnakePoolRuleSet)) SnakePoolRuleSet.Cleanup();
            // No grime left from a Power Wash match (Power Wash makes its own).
            if (!(ruleSet is PowerWashPoolRuleSet)) PowerWashPoolRuleSet.Cleanup();
            // Table balls again after Play Anywhere.
            if (!(ruleSet is PlayAnywhereRuleSet)) PlayAnywhereRuleSet.Cleanup();
            // Full-size players and one cue ball after Pocket Size.
            if (!(ruleSet is PocketSizeRuleSet)) PocketSizeRuleSet.Cleanup();
            // Full-size players and the cue ball back after the Shooter miniature.
            if (!(ruleSet is ShooterMiniRuleSet)) ShooterMiniRuleSet.Cleanup();
            // No pins left after Bowling Pool (Bowling Pool clears its own).
            if (!(ruleSet is BowlingPoolRuleSet)) BowlingPoolRuleSet.Cleanup();
            // The real table and the players' bodies back after Sumo Pool.
            if (!(ruleSet is SumoPoolRuleSet)) SumoPoolRuleSet.Cleanup();
            // Full-size players and one cue ball after Hide and seek.
            if (!(ruleSet is HideAndSeekRuleSet)) HideAndSeekRuleSet.Cleanup();
            // One shared cue ball again after a match with one each.
            if (!(ruleSet is SpeedPoolRuleSet)) SpeedPoolRuleSet.Cleanup();
            ruleSet.Setup(this);
            MatchStarted = true;
            // The living holes open with the rack (Rejouer comes through here too).
            if (Mode == PoolGameMode.CatchTheHole) CatchTheHole.Ensure().Begin();
            else if (CatchTheHole.Instance != null) CatchTheHole.Instance.Clear();
            if (traceShots) Debug.Log($"[Match] Partie lancée : {Setup.Describe()} ({ruleSet.GetType().Name}), frame {Time.frameCount}.", this);

            // Hand cursor control back to normal FPS/aim look now that the menu is gone.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            MatchBegan?.Invoke();
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
            PoolGameMode.PlayAnywhere => new PlayAnywhereRuleSet(),
            PoolGameMode.PocketSize => new PocketSizeRuleSet(),
            PoolGameMode.ShooterMini => new ShooterMiniRuleSet(),
            PoolGameMode.ShooterPool => new ShooterPoolRuleSet(),
            PoolGameMode.BowlingPool => new BowlingPoolRuleSet(),
            PoolGameMode.SumoPool => new SumoPoolRuleSet(),
            PoolGameMode.SumoBilliard => new SumoBilliardRuleSet(),
            PoolGameMode.HideAndSeek => new HideAndSeekRuleSet(),
            // 8-ball, and Catch the Hole (8-ball plus the living holes).
            _ => setup.type == PoolMatchType.SpeedPool ? new SpeedPoolRuleSet() : new EightBallRuleSet(),
        };

    }
}
