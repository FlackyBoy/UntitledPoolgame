using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace UntitledPoolGame.Pool
{
    // Pocket Size: one player (the tiny one) is shrunk onto the felt, the
    // other (the giant) hunts them with the cue ball. Hits To Lose touches and
    // the tiny one loses the round; Round Duration survived and they win it.
    // Two rounds, the roles swapped for the second. One round each: whoever
    // won both wins; one each, the longer survival as the tiny one, then the
    // fewer touches taken.
    //
    // Speed Pool only (no turns): the giant shoots whenever their cue ball
    // is still. Derived from SpeedPoolRuleSet for the cue balls owned by a
    // player (PoolBall.CueOwner, which LocalPoolAimController aims), but
    // with its own table: a few coloured balls scattered as cover, coming
    // back when potted; no groups, no win by potting.
    //
    // PoolMatchRules calls Tick every frame (rounds, touches, falls, powers),
    // CanShoot (only the giant, only while hunting), and ShotPowerScale.
    // The crates ask TryTakeCrate: each power belongs to one role, the other
    // role running over its crate destroys it.
    public class PocketSizeRuleSet : SpeedPoolRuleSet
    {
        public enum Phase { Countdown, Hunt, RoundOver }

        public const int Rounds = 2;

        // The match under way, null in other modes (the crates and the
        // powers look it up).
        public static PocketSizeRuleSet Current { get; private set; }

        // A touch (or a fall): the tiny player, their touches so far, where.
        public static event Action<int, int, Vector3> TinyHit;
        // A round over: who won it, and whether the tiny one survived.
        public static event Action<int, bool> RoundEnded;

        private static PocketSizeSettings cfg;
        public static PocketSizeSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<PocketSizeSettings>("PocketSizeSettings");

        // The giant's cue ball, and the second one of Double Cue Ball. Kept
        // across "Rejouer" (Restart resets every ball to its spawn first),
        // handed back by Cleanup.
        private static PoolBall mainCue, extraCue;

        public Phase CurrentPhase { get; private set; }
        public int Round { get; private set; }
        public int Giant => Round == 0 ? Settings.firstGiant : 1 - Settings.firstGiant;
        public int Tiny => 1 - Giant;
        public PocketSizeRole RoleOf(int player) => player == Giant ? PocketSizeRole.Giant : PocketSizeRole.Tiny;

        private float phaseTime;
        // Per player, as the tiny one.
        private readonly int[] hitsTaken = new int[2];
        private readonly float[] timeSurvived = new float[2];
        private readonly int[] roundsWon = new int[2];
        private float invulnerableUntil;

        private readonly LocalPlayerShrink[] bodies = new LocalPlayerShrink[2];
        private Vector3 tinySpawn;
        private bool tinyPlaced;
        private int tinyReadyFrame;
        private float feltY;
        private int ballLayers;

        // Coloured balls potted, back on the felt at the time given.
        private readonly List<(PoolBall ball, float time)> respots = new List<(PoolBall, float)>();
        private readonly List<Vector3> placed = new List<Vector3>();
        // Each ball's velocity last frame (CheckHits).
        private readonly Dictionary<PoolBall, Vector3> lastVelocity = new Dictionary<PoolBall, Vector3>();

        // Powers under way (Time.time they end, and their strength).
        private float slowCueUntil, slowCueMaxSpeed;
        private float sprintUntil, sprintMultiplier;
        private float slowTinyUntil, slowTinyMultiplier;
        private float shotBoostUntil, shotBoostMultiplier;
        private float extraCueUntil = -1f;

        public float TimeLeft => CurrentPhase == Phase.Hunt ? Mathf.Max(0f, Settings.roundDuration - phaseTime) : Settings.roundDuration;
        public float CountdownLeft => CurrentPhase == Phase.Countdown ? Mathf.Max(0f, Settings.countdown - phaseTime) : 0f;

        public float ShotPowerScale => Time.time < shotBoostUntil ? shotBoostMultiplier : 1f;

        public bool CanShoot(int player) => CurrentPhase == Phase.Hunt && player == Giant;

        public override void Setup(PoolMatchRules match)
        {
            cfg = PoolSettingsLoader.LoadOrDefault<PocketSizeSettings>("PocketSizeSettings");
            LocalPlayerShrink.RestoreAll();
            Current = this;
            ballLayers = LayerMask.GetMask("Poolball");

            if (mainCue == null || extraCue == null)
            {
                if (!FindPlayerCueBalls(out mainCue, out extraCue)) return;
                extraCue.name = "CueBall_Extra (Pocket Size)";
            }

            // Two players or nothing: started alone (Lancer sans le menu, the
            // 1-player menu), the second one joins in split screen right away,
            // as C+P would (a free gamepad, else the keyboard: Tab swaps).
            if (PlayerInput.all.Count < 2)
                Debug.Log($"[PocketSize] Un seul joueur : écran partagé. {Core.SplitScreenCheatSpawner.JoinPlayer()}");
            bodies[0] = bodies[1] = null;
            FindBodies();
            if (bodies[0] == null || bodies[1] == null)
                Debug.LogWarning("[PocketSize] Il faut deux joueurs : le petit sera posé sur la table dès que le deuxième arrive (C+P).");

            StartRound(match, 0);
        }

        // The players' bodies by index. One just joined is given two frames
        // before being shrunk and moved: its puppet and controllers start up
        // first, and the join itself moves it to its spawn point.
        private void FindBodies()
        {
            foreach (PlayerInput input in PlayerInput.all)
            {
                int index = input.playerIndex;
                if (index < 0 || index >= 2 || bodies[index] != null) continue;
                bodies[index] = LocalPlayerShrink.For(input.gameObject);
                tinyReadyFrame = Time.frameCount + 2;
            }
        }

        // Shrinks the tiny one and puts them on the felt, as soon as they're
        // there and ready.
        private void TryPlaceTiny()
        {
            if (tinyPlaced) return;
            if (bodies[Tiny] == null) FindBodies();
            LocalPlayerShrink tiny = bodies[Tiny];
            if (tiny == null || Time.frameCount < tinyReadyFrame) return;
            PocketSizeSettings s = Settings;
            tiny.Shrink(s.tinyScale, s.tinyBlocksBalls ? ballLayers : 0, s.tinyNearClip, s.tinyJumpHeight);
            tiny.Teleport(tinySpawn, FacingCueBall(tinySpawn));
            tinyPlaced = true;
            if (s.debugLogs) Debug.Log($"[PocketSize] J{Tiny + 1} rétréci ({s.tinyScale:0.###}×) et posé sur la table à {tinySpawn}.", tiny);
        }

        // On the felt (not beside the table, not above it): something else
        // may have moved the body right after it was placed.
        private bool OnTable(LocalPlayerShrink tiny)
        {
            Vector3 p = tiny.transform.position;
            if (Mathf.Abs(p.y - feltY) > 0.15f) return false;
            PoolTableSurface surface = PoolTableSurface.Instance;
            return surface == null || Flat(surface.ClampToPlayArea(p, 0f) - p).sqrMagnitude < 0.05f * 0.05f;
        }

        // Another mode comes next: everyone full size, the extra cue ball
        // gone, the table's cue ball shared again.
        public static new void Cleanup()
        {
            Current = null;
            LocalPlayerShrink.RestoreAll();
            if (extraCue != null)
            {
                extraCue.gameObject.SetActive(false);
                Object.Destroy(extraCue.gameObject);
            }
            if (mainCue != null) mainCue.MakeSharedCueBall();
            mainCue = extraCue = null;
        }

        private void StartRound(PoolMatchRules match, int round)
        {
            PocketSizeSettings s = Settings;
            Round = round;
            CurrentPhase = Phase.Countdown;
            phaseTime = 0f;
            LocalPlayerShrink.RestoreAll();
            // Powers belong to a role: a stock held over would be the wrong one.
            match.ClearHeldPowers();
            slowCueUntil = sprintUntil = slowTinyUntil = shotBoostUntil = extraCueUntil = -1f;
            hitsTaken[Tiny] = 0;
            timeSurvived[Tiny] = 0f;
            respots.Clear();
            lastVelocity.Clear();

            PoolTableSurface surface = PoolTableSurface.Instance;
            feltY = surface != null ? surface.FeltTopY : mainCue.SpawnPosition.y - mainCue.Radius;

            // The giant's cue ball on the head spot, the second one beside it, put away.
            mainCue.MakePlayerCueBall(Giant, mainCue.SpawnPosition, s.giantCueColor);
            Vector3 across = surface != null ? surface.transform.rotation * Vector3.forward : Vector3.forward;
            extraCue.MakePlayerCueBall(Giant, mainCue.SpawnPosition + across * (mainCue.Radius * 3f), s.giantCueColor);
            extraCue.gameObject.SetActive(false);

            // The tiny one starts on the foot spot, across the table from the cue ball.
            tinySpawn = mainCue.SpawnPosition;
            if (surface != null)
            {
                Vector3 center = surface.transform.position;
                Vector3 along = surface.transform.rotation * Vector3.right;
                if (Vector3.Dot(mainCue.SpawnPosition - center, along) > 0f) along = -along;
                tinySpawn = center + along * (surface.HalfLength * 0.5f);
            }
            tinySpawn.y = feltY + 0.01f;

            ScatterObstacles();

            tinyPlaced = false;
            invulnerableUntil = 0f;
            if (s.debugLogs)
                Debug.Log($"[PocketSize] Manche {round + 1}/{Rounds} : J{Giant + 1} grand (blanche {mainCue.transform.position}), J{Tiny + 1} petit. Chasse dans {s.countdown:0.#} s.");
            TryPlaceTiny();
        }

        private Quaternion FacingCueBall(Vector3 from)
        {
            Vector3 look = mainCue.SpawnPosition - from;
            look.y = 0f;
            return look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look) : Quaternion.identity;
        }

        // Obstacle Balls coloured balls at random spots, clear of the
        // pockets, the cue ball and the tiny one's start; the rest (and Speed
        // Pool's extra copies) put away for the round.
        private void ScatterObstacles()
        {
            var balls = new List<PoolBall>();
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                if (ball.IsCueBall) continue;
                if (ball.IsExtra) ball.gameObject.SetActive(false);
                else balls.Add(ball);
            }
            for (int i = balls.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (balls[i], balls[j]) = (balls[j], balls[i]);
            }

            placed.Clear();
            int keep = Mathf.Min(Settings.obstacleBalls, balls.Count);
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (i >= keep)
                {
                    ball.gameObject.SetActive(false);
                    continue;
                }
                ball.ResetToSpawn();
                Vector3 spot = FeltSpot(ball.Radius, ball.SpawnPosition.y);
                placed.Add(spot);
                ball.PlaceAt(spot);
            }
            // Only for spacing this scatter: the balls move from here on.
            placed.Clear();
        }

        // A random point of the felt at height y, clear of the pockets, the
        // cue balls, the tiny one's start and the balls already placed.
        // Falls back to the nearest free spot around the table's centre.
        private Vector3 FeltSpot(float radius, float y)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return new Vector3(tinySpawn.x, y, tinySpawn.z);
            Vector3 center = surface.transform.position;
            Vector3 along = surface.transform.rotation * Vector3.right;
            Vector3 across = surface.transform.rotation * Vector3.forward;
            float clearance = Settings.obstacleClearance;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p = center + along * (Random.Range(-1f, 1f) * surface.HalfLength) + across * (Random.Range(-1f, 1f) * surface.HalfWidth);
                p = surface.ClampToPlayArea(p, radius * 2f);
                p = PoolPocket.AvoidAllPockets(p, radius * 2f);
                p.y = y;
                if (IsClear(p, clearance, radius)) return p;
            }
            Vector3 fallback = surface.ClampToPlayArea(center, radius);
            fallback.y = y;
            return fallback;
        }

        private bool IsClear(Vector3 p, float clearance, float radius)
        {
            if (Flat(p - tinySpawn).sqrMagnitude < clearance * clearance) return false;
            foreach (PoolBall ball in PoolBall.Active)
                if (ball.IsCueBall && Flat(p - ball.transform.position).sqrMagnitude < clearance * clearance) return false;
            float apart = radius * 3f;
            foreach (Vector3 other in placed)
                if (Flat(p - other).sqrMagnitude < apart * apart) return false;
            return true;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ---------- Every frame (PoolMatchRules.Update) ----------

        public void Tick(PoolMatchRules match)
        {
            // No cue ball on the table (Setup said so): nothing to play.
            if (match.GameOver || mainCue == null) return;
            PocketSizeSettings s = Settings;
            phaseTime += Time.deltaTime;
            UpdateRespots();
            TryPlaceTiny();
            LocalPlayerShrink tiny = tinyPlaced ? bodies[Tiny] : null;

            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    // Free to find their footing: a fall doesn't count yet,
                    // and a body moved off the table goes back on it.
                    if (tiny != null && !OnTable(tiny))
                    {
                        if (Settings.debugLogs) Debug.Log($"[PocketSize] J{Tiny + 1} n'est pas sur la table ({tiny.transform.position}) pendant le compte à rebours : reposé à {tinySpawn}.", tiny);
                        tiny.Teleport(tinySpawn, FacingCueBall(tinySpawn));
                    }
                    if (phaseTime < s.countdown) break;
                    CurrentPhase = Phase.Hunt;
                    phaseTime = 0f;
                    if (s.debugLogs) Debug.Log($"[PocketSize] Chasse ! J{Giant + 1} peut tirer.");
                    break;

                case Phase.Hunt:
                    timeSurvived[Tiny] = phaseTime;
                    UpdatePowers(tiny);
                    if (tiny != null)
                    {
                        CheckFall(tiny, true);
                        CheckHits(tiny);
                    }
                    if (hitsTaken[Tiny] >= s.hitsToLose) EndRound(false);
                    else if (phaseTime >= s.roundDuration) EndRound(true);
                    break;

                case Phase.RoundOver:
                    if (phaseTime < s.roundOverTime) break;
                    if (Round + 1 < Rounds) StartRound(match, Round + 1);
                    else Finish(match);
                    break;
            }
        }

        private void UpdateRespots()
        {
            for (int i = respots.Count - 1; i >= 0; i--)
            {
                (PoolBall ball, float time) = respots[i];
                if (Time.time < time) continue;
                respots.RemoveAt(i);
                if (ball == null) continue;
                ball.ResetToSpawn();
                ball.PlaceAt(PoolMatchRules.FindFreeSpot(FeltSpot(ball.Radius, ball.SpawnPosition.y), ball));
            }
        }

        private void UpdatePowers(LocalPlayerShrink tiny)
        {
            float now = Time.time;
            if (now < slowCueUntil)
            {
                foreach (PoolBall ball in PoolBall.Active)
                {
                    if (!IsGiantCueBall(ball)) continue;
                    Vector3 v = ball.Rigidbody.linearVelocity;
                    if (v.sqrMagnitude > slowCueMaxSpeed * slowCueMaxSpeed) ball.Rigidbody.linearVelocity = v.normalized * slowCueMaxSpeed;
                }
            }
            if (extraCueUntil > 0f && now >= extraCueUntil)
            {
                extraCueUntil = -1f;
                PutExtraCueAway();
            }
            if (tiny != null)
                tiny.SetSpeedMultiplier((now < sprintUntil ? sprintMultiplier : 1f) * (now < slowTinyUntil ? slowTinyMultiplier : 1f));
        }

        private bool IsGiantCueBall(PoolBall ball) =>
            ball.IsCueBall && ball.CueOwner == Giant && !ball.Rigidbody.isKinematic && ball.gameObject.activeInHierarchy;

        // Parked first: an aim on it ends (LocalPoolAimController leaves a
        // kinematic cue ball).
        private static void PutExtraCueAway()
        {
            if (extraCue == null || !extraCue.gameObject.activeSelf) return;
            extraCue.BeginBallInHand();
            extraCue.gameObject.SetActive(false);
        }

        // Under the felt (a pocket, off the table): a touch while hunting,
        // and back onto the felt either way. Not counted when the touch that
        // threw it there already was (still invulnerable).
        private void CheckFall(LocalPlayerShrink tiny, bool counts)
        {
            Vector3 where = tiny.transform.position;
            if (where.y > feltY - Settings.fallDepth) return;
            bool counted = counts && Time.time >= invulnerableUntil;
            Vector3 spot = FeltSpot(0.03f, feltY + 0.01f);
            tiny.Teleport(spot, FacingCueBall(spot));
            invulnerableUntil = Time.time + Settings.invulnerableTime;
            if (counted)
            {
                hitsTaken[Tiny]++;
                TinyHit?.Invoke(Tiny, hitsTaken[Tiny], where);
            }
            if (Settings.debugLogs)
                Debug.Log($"[PocketSize] J{Tiny + 1} tombe de la table à {where}{(counted ? $" : touche {hitsTaken[Tiny]}/{Settings.hitsToLose}" : "")}, replacé à {spot}.");
        }

        // Any ball rolling fast enough into the tiny one's capsule counts for
        // the giant: the cue ball, or a coloured ball it sent flying. A
        // distance test rather than collision callbacks: with Tiny Blocks
        // Balls the ball bounces off the body before overlapping it (Hit
        // Margin). Invulnerable Time after a touch, none counts.
        private void CheckHits(LocalPlayerShrink tiny)
        {
            PocketSizeSettings s = Settings;
            bool open = Time.time >= invulnerableUntil;
            tiny.GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            PoolBall hitBy = null;
            Vector3 velocity = Vector3.zero, closest = Vector3.zero;
            foreach (PoolBall ball in PoolBall.Active)
            {
                // Last frame's velocity too, kept for every ball every frame:
                // the ball now collides with the body and may have bounced
                // off it already, in the physics steps before this frame.
                Vector3 now = ball.Rigidbody.linearVelocity;
                lastVelocity.TryGetValue(ball, out Vector3 before);
                lastVelocity[ball] = now;
                if (!open || hitBy != null || ball.Rigidbody.isKinematic) continue;

                Vector3 c = ball.transform.position;
                Vector3 nearest = ClosestOnSegment(bottom, top, c);
                // Coming at the tiny one, fast enough, either then or now: a
                // ball rolling away (bounced off them already) doesn't count.
                Vector3 incoming = Coming(now, nearest - c, s.minHitSpeed) ? now
                                 : Coming(before, nearest - c, s.minHitSpeed) ? before : Vector3.zero;
                if (incoming == Vector3.zero) continue;
                // Plus what it rolled in a frame: it may be off the body again.
                float reach = radius + ball.Radius + s.hitMargin + incoming.magnitude * Time.deltaTime;
                if ((c - nearest).sqrMagnitude > reach * reach) continue;
                hitBy = ball;
                velocity = incoming;
                closest = nearest;
            }
            if (hitBy != null) Hit(tiny, hitBy, velocity, closest);
        }

        private static bool Coming(Vector3 velocity, Vector3 toTiny, float minSpeed) =>
            velocity.sqrMagnitude >= minSpeed * minSpeed && Vector3.Dot(Flat(velocity), Flat(toTiny)) > 0f;

        // A touch: counted, and the tiny one is thrown by the impact. How
        // hard: the ball's speed into them (head on, all of it; grazing,
        // little), times Knock Transfer. Which way: between where the ball
        // was rolling and straight away from the point of impact (Knock
        // Along Impact). velocity is coming at them, never flat zero.
        private void Hit(LocalPlayerShrink tiny, PoolBall ball, Vector3 velocity, Vector3 closest)
        {
            PocketSizeSettings s = Settings;
            hitsTaken[Tiny]++;
            invulnerableUntil = Time.time + s.invulnerableTime;
            Vector3 rolling = Flat(velocity).normalized;
            Vector3 away = Flat(closest - ball.transform.position);
            away = away.sqrMagnitude > 1e-8f ? away.normalized : rolling;
            float into = Mathf.Max(Vector3.Dot(Flat(velocity), away), 0f);
            float speed = Mathf.Min(into * s.knockTransfer, s.knockMaxSpeed);
            Vector3 direction = Vector3.Slerp(rolling, away, s.knockAlongImpact).normalized;
            tiny.Knock((direction + Vector3.up * s.knockLift) * speed, s.knockDamping);
            TinyHit?.Invoke(Tiny, hitsTaken[Tiny], closest);
            if (s.debugLogs)
                Debug.Log($"[PocketSize] J{Tiny + 1} touché par {(ball.IsCueBall ? "la blanche" : $"la bille {ball.Number}")} ({velocity.magnitude:0.00} m/s, {into:0.00} m/s vers lui) à {closest} : {hitsTaken[Tiny]}/{s.hitsToLose}, projeté à {speed:0.00} m/s.", ball);
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSqr);
        }

        private void EndRound(bool tinySurvived)
        {
            CurrentPhase = Phase.RoundOver;
            phaseTime = 0f;
            PutExtraCueAway();
            int winner = tinySurvived ? Tiny : Giant;
            roundsWon[winner]++;
            RoundEnded?.Invoke(winner, tinySurvived);
            if (Settings.debugLogs)
                Debug.Log($"[PocketSize] Fin de la manche {Round + 1} : J{Tiny + 1} {(tinySurvived ? "a tenu" : $"touché {hitsTaken[Tiny]} fois après {timeSurvived[Tiny]:0.0} s")} → manche pour J{winner + 1} ({roundsWon[0]}-{roundsWon[1]}).");
        }

        private void Finish(PoolMatchRules match)
        {
            LocalPlayerShrink.RestoreAll();
            int winner;
            string why;
            if (roundsWon[0] != roundsWon[1])
            {
                winner = roundsWon[0] > roundsWon[1] ? 0 : 1;
                why = "les deux manches";
            }
            else if (Mathf.Abs(timeSurvived[0] - timeSurvived[1]) > 0.05f)
            {
                winner = timeSurvived[0] > timeSurvived[1] ? 0 : 1;
                why = $"a tenu plus longtemps en petit ({timeSurvived[winner]:0.0} s contre {timeSurvived[1 - winner]:0.0} s)";
            }
            else if (hitsTaken[0] != hitsTaken[1])
            {
                winner = hitsTaken[0] < hitsTaken[1] ? 0 : 1;
                why = $"moins touché en petit ({hitsTaken[winner]} contre {hitsTaken[1 - winner]})";
            }
            else
            {
                winner = Random.Range(0, 2);
                why = "égalité parfaite, tiré au sort";
            }
            if (Settings.debugLogs) Debug.Log($"[PocketSize] Victoire de J{winner + 1} : {why}.");
            match.Win(winner);
        }

        // Coloured balls come back after Respot Delay; a cue ball comes back
        // through PoolMatchRules (Speed Pool's respawn delay).
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (!ball.IsCueBall) respots.Add((ball, Time.time + Settings.respotDelay));
        }

        // ---------- Crates (PoolPowerCrate) ----------

        // True when someone took the crate at position: the tiny one walking
        // onto it (Tiny Crate Reach) or one of the giant's cue balls rolling
        // over it (cueReach, the crate's own pickup radius). The power goes
        // to its role; the other role only destroys it.
        public bool TryTakeCrate(PoolMatchRules match, Vector3 position, float cueReach, PoolPower power)
        {
            if (CurrentPhase != Phase.Hunt) return false;
            int taker = -1;
            LocalPlayerShrink tiny = tinyPlaced ? bodies[Tiny] : null;
            float tinyReach = Settings.tinyCrateReach;
            if (tiny != null && Flat(tiny.transform.position - position).sqrMagnitude <= tinyReach * tinyReach) taker = Tiny;
            if (taker < 0)
            {
                foreach (PoolBall ball in PoolBall.Active)
                {
                    if (!IsGiantCueBall(ball)) continue;
                    float reach = cueReach + ball.Radius;
                    if ((ball.transform.position - position).sqrMagnitude > reach * reach) continue;
                    taker = Giant;
                    break;
                }
            }
            if (taker < 0) return false;

            bool own = power is PocketSizePower pocketPower && pocketPower.Role == RoleOf(taker);
            if (own) match.GrantPower(taker, power);
            if (Settings.debugLogs)
                Debug.Log($"[PocketSize] Caisse « {power.PowerName} » {(own ? "ramassée" : "détruite")} par J{taker + 1} ({(taker == Giant ? "grand" : "petit")}).");
            return true;
        }

        public static Color CrateColor(PocketSizeRole role) => role == PocketSizeRole.Giant ? Settings.giantCrateColor : Settings.tinyCrateColor;

        // ---------- Powers (PocketSizePower) ----------

        public void SlowCueBalls(float duration, float maxSpeed)
        {
            slowCueUntil = Time.time + duration;
            slowCueMaxSpeed = maxSpeed;
            Trace($"blanches ralenties à {maxSpeed:0.##} m/s pendant {duration:0.#} s");
        }

        // Each of the giant's cue balls rolls off towards its nearest open pocket.
        public void SendCueBallsToPockets(float speed)
        {
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (!IsGiantCueBall(ball)) continue;
                PoolPocket nearest = null;
                float best = float.MaxValue;
                foreach (PoolPocket pocket in PoolPocket.Active)
                {
                    if (pocket.IsClosed) continue;
                    float d = Flat(pocket.transform.position - ball.transform.position).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    nearest = pocket;
                }
                if (nearest == null) continue;
                Vector3 direction = Flat(nearest.transform.position - ball.transform.position).normalized;
                ball.Rigidbody.linearVelocity = direction * speed;
                ball.Rigidbody.angularVelocity = Vector3.zero;
                Trace($"blanche repoussée vers la poche {nearest.DescribeLocation()}");
            }
        }

        public void BoostTiny(float duration, float multiplier)
        {
            sprintUntil = Time.time + duration;
            sprintMultiplier = multiplier;
            Trace($"petit plus rapide ×{multiplier:0.##} pendant {duration:0.#} s");
        }

        public void SlowTiny(float duration, float multiplier)
        {
            slowTinyUntil = Time.time + duration;
            slowTinyMultiplier = multiplier;
            Trace($"petit ralenti ×{multiplier:0.##} pendant {duration:0.#} s");
        }

        public void BoostShots(float duration, float multiplier)
        {
            shotBoostUntil = Time.time + duration;
            shotBoostMultiplier = multiplier;
            Trace($"tirs ×{multiplier:0.##} pendant {duration:0.#} s");
        }

        // The second cue ball beside the first (or as near as is free); a
        // second use while it's out only extends it.
        public void AddSecondCueBall(float duration)
        {
            extraCueUntil = Time.time + duration;
            if (extraCue == null || extraCue.gameObject.activeSelf) return;
            Vector3 beside = mainCue.transform.position + (extraCue.RespawnPosition - mainCue.SpawnPosition);
            extraCue.MakePlayerCueBall(Giant, extraCue.RespawnPosition, Settings.giantCueColor);
            extraCue.PlaceAt(PoolMatchRules.FindFreeSpot(new Vector3(beside.x, extraCue.RespawnPosition.y, beside.z), extraCue));
            Trace($"deuxième blanche pendant {duration:0.#} s");
        }

        private static void Trace(string what)
        {
            if (Settings.debugLogs) Debug.Log($"[PocketSize] Pouvoir : {what}.");
        }

        // ---------- HUD ----------

        public string RoleLabel(int player) => player == Giant ? Settings.giantLabel : Settings.tinyLabel;

        // Round, then the phase: countdown, clock and touches, or the result.
        public string DescribeRound(int player)
        {
            PocketSizeSettings s = Settings;
            string round = string.Format(s.roundFormat, Round + 1, Rounds);
            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    return $"{round}  ·  {string.Format(s.countdownFormat, Mathf.CeilToInt(CountdownLeft))}";
                case Phase.Hunt:
                    int left = Mathf.CeilToInt(TimeLeft);
                    return $"{round}  ·  {left / 60}:{left % 60:00}  ·  {string.Format(s.hitsFormat, hitsTaken[Tiny], s.hitsToLose)}";
                default:
                    bool tinySurvived = hitsTaken[Tiny] < s.hitsToLose;
                    bool won = (player == Tiny) == tinySurvived;
                    return $"{round}  ·  {(won ? s.roundWonText : s.roundLostText)}";
            }
        }

        // Changes whenever DescribeRound would.
        public int PlateKey(int player) =>
            ((Round * 4 + (int)CurrentPhase) * 1000 + (CurrentPhase == Phase.Countdown ? Mathf.CeilToInt(CountdownLeft) : Mathf.CeilToInt(TimeLeft))) * 16
            + hitsTaken[Tiny] * 2 + (player == Giant ? 1 : 0);

        public override string DescribePlayer(int player) => $"{RoleLabel(player)} · {DescribeRound(player)}";
    }
}
