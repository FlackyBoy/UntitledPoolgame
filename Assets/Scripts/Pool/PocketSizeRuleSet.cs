using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace UntitledPoolGame.Pool
{
    // Pocket Size: the tiny ones are shrunk onto the felt, the giant hunts
    // them with the cue ball. Hits To Lose touches and a tiny one is out;
    // all of them out and the giant wins the round, Round Duration survived
    // by one of them and the tiny ones do.
    //
    // At two: two rounds, the roles swapped for the second. At three or four
    // each for themselves: one giant against all the others, each player the
    // giant once. In 2 v 2: one team giants (a cue ball each), the other
    // tiny, then the other way round. A round won scores points: the giant's
    // (Giant Win Points), or the tiny ones' together, the least touched
    // first (Tiny Rank Points). Most points wins; level, the longer survival
    // as a tiny one, then the fewer touches taken. At two, with the default
    // points, that's the old rule: a round each, then survival, then touches.
    //
    // Speed Pool only (no turns): a giant shoots whenever their cue ball is
    // still. Derived from SpeedPoolRuleSet for the cue balls owned by a
    // player (PoolBall.CueOwner, which LocalPoolAimController aims), but
    // with its own table: a few coloured balls scattered as cover, coming
    // back when potted; no groups, no win by potting.
    //
    // PoolMatchRules calls Tick every frame (rounds, touches, falls, powers),
    // CanShoot (only a giant, only while hunting), and ShotPowerScale.
    // The crates ask TryTakeCrate: each power belongs to one role, the other
    // role running over its crate destroys it.
    public class PocketSizeRuleSet : SpeedPoolRuleSet
    {
        public enum Phase { Countdown, Hunt, RoundOver }

        // The match under way, null in other modes (the crates and the
        // powers look it up).
        public static PocketSizeRuleSet Current { get; private set; }

        // A touch (or a fall): the tiny player, their touches so far, where.
        public static event Action<int, int, Vector3> TinyHit;
        // A round over: a player of the side that won it (the best tiny one,
        // or a giant), and whether the tiny ones won.
        public static event Action<int, bool> RoundEnded;

        private static PocketSizeSettings cfg;
        public static PocketSizeSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<PocketSizeSettings>("PocketSizeSettings");

        // The giants' cue balls, then the second one of Double Cue Ball (the
        // last). Built again by every Setup, handed back by Cleanup.
        private static PoolBall tableCue;
        private static readonly List<PoolBall> cues = new List<PoolBall>();
        private int giantCueCount;
        private PoolBall ExtraCue => cues.Count > giantCueCount ? cues[giantCueCount] : null;
        private PoolBall FirstCue => cues.Count > 0 ? cues[0] : null;

        public Phase CurrentPhase { get; private set; }
        public int Round { get; private set; }
        public int RoundCount => players <= 2 || teams ? 2 : players;

        private int players = 2;
        private bool teams;
        // This round's sides.
        private readonly List<int> giants = new List<int>();
        private readonly List<int> tinies = new List<int>();
        private readonly bool[] isGiant = new bool[PoolMatchRules.MaxPlayers];

        public bool IsGiant(int player) => player >= 0 && player < isGiant.Length && isGiant[player];
        public PocketSizeRole RoleOf(int player) => IsGiant(player) ? PocketSizeRole.Giant : PocketSizeRole.Tiny;

        private float phaseTime;
        // Per player, this round as a tiny one: touches, time held, out.
        private readonly int[] hitsTaken = new int[PoolMatchRules.MaxPlayers];
        private readonly float[] timeSurvived = new float[PoolMatchRules.MaxPlayers];
        private readonly bool[] isOut = new bool[PoolMatchRules.MaxPlayers];
        private readonly float[] invulnerableUntil = new float[PoolMatchRules.MaxPlayers];
        // Down from a ball's touch (protected while down), or from another
        // tiny one's blow (touchable all the while).
        private readonly bool[] downFromTouch = new bool[PoolMatchRules.MaxPlayers];
        private readonly bool[] downFromBlow = new bool[PoolMatchRules.MaxPlayers];
        // Over the match: points, and the tie-breaks (as a tiny one).
        private readonly int[] points = new int[PoolMatchRules.MaxPlayers];
        private readonly float[] totalSurvived = new float[PoolMatchRules.MaxPlayers];
        private readonly int[] totalHits = new int[PoolMatchRules.MaxPlayers];
        private bool tiniesWon;

        // Each tiny one's small body, once placed.
        private readonly LocalPlayerShrink[] tinyBodies = new LocalPlayerShrink[PoolMatchRules.MaxPlayers];
        private readonly PlayerInput[] tinyWaitedFor = new PlayerInput[PoolMatchRules.MaxPlayers];
        private readonly int[] tinyReadyFrame = new int[PoolMatchRules.MaxPlayers];
        private readonly Vector3[] tinySpawns = new Vector3[PoolMatchRules.MaxPlayers];
        private float feltY;
        private int ballLayers;

        // Coloured balls potted, back on the felt at the time given.
        private readonly List<(PoolBall ball, float time)> respots = new List<(PoolBall, float)>();
        private readonly List<Vector3> placed = new List<Vector3>();
        // Each ball's velocity last frame (CheckHits).
        private readonly Dictionary<PoolBall, Vector3> lastVelocity = new Dictionary<PoolBall, Vector3>();
        // CheckHits, per tiny one: the ball that touched them this frame.
        private readonly PoolBall[] hitBy = new PoolBall[PoolMatchRules.MaxPlayers];
        private readonly Vector3[] hitVelocity = new Vector3[PoolMatchRules.MaxPlayers];
        private readonly Vector3[] hitPoint = new Vector3[PoolMatchRules.MaxPlayers];

        // Powers under way (Time.time they end, and their strength). Sprint
        // is the tiny one's who took it; the others, everyone's on that side.
        private float slowCueUntil, slowCueMaxSpeed;
        private readonly float[] sprintUntil = new float[PoolMatchRules.MaxPlayers];
        private float sprintMultiplier = 1f;
        private float slowTinyUntil, slowTinyMultiplier;
        private float shotBoostUntil, shotBoostMultiplier;
        private float extraCueUntil = -1f;

        public float TimeLeft => CurrentPhase == Phase.Hunt ? Mathf.Max(0f, Settings.roundDuration - phaseTime) : Settings.roundDuration;
        public float CountdownLeft => CurrentPhase == Phase.Countdown ? Mathf.Max(0f, Settings.countdown - phaseTime) : 0f;

        public override float ShotPowerScale => Time.time < shotBoostUntil ? shotBoostMultiplier : 1f;

        public override bool AllowsShot(PoolMatchRules match, int playerIndex) => CanShoot(playerIndex);

        public bool CanShoot(int player) => CurrentPhase == Phase.Hunt && IsGiant(player);

        // Several tiny ones on the felt: they may punch and kick each other
        // (not a teammate: MeleeHit). A blow costs no touch, only the giants'
        // shots do (CheckHits); down from a blow, a giant's ball can touch
        // them all the while.
        public override bool AllowsMelee(int player) =>
            CurrentPhase == Phase.Hunt && tinies.Count > 1 && !IsGiant(player)
            && player >= 0 && player < isOut.Length && !isOut[player] && tinyBodies[player] != null;

        public override void Setup(PoolMatchRules match)
        {
            cfg = PoolSettingsLoader.LoadOrDefault<PocketSizeSettings>("PocketSizeSettings");
            LocalPlayerShrink.RestoreAll();
            Current = this;
            ballLayers = LayerMask.GetMask("Poolball");
            players = Mathf.Clamp(PlayerBallGroups.Players, 2, PoolMatchRules.MaxPlayers);
            teams = PlayerBallGroups.Teams;
            Array.Clear(points, 0, points.Length);
            Array.Clear(totalSurvived, 0, totalSurvived.Length);
            Array.Clear(totalHits, 0, totalHits.Length);

            // Both bodies come from these: without them, nothing to play
            // (no cue ball, Tick does nothing).
            if (Settings.tinyPrefab == null || PlayerInputManager.instance == null || PlayerInputManager.instance.playerPrefab == null)
            {
                Debug.LogError("[PocketSize] Il manque le prefab du petit (PocketSizeSettings, Tiny Prefab) ou celui du joueur (PlayerInputManager, Player Prefab) : pas de partie.");
                cues.Clear();
                return;
            }

            // A cue ball per giant, and the spare for Double Cue Ball.
            giantCueCount = teams ? 2 : 1;
            if (!TakeCueBalls(ref tableCue, giantCueCount + 1, cues, "Pocket Size")) return;

            // Two players or nothing: started alone (Lancer sans le menu, the
            // 1-player menu), the second one joins in split screen right away,
            // as C+P would (a free gamepad, else the keyboard: Tab swaps).
            if (PlayerInput.all.Count < 2)
                Debug.Log($"[PocketSize] Un seul joueur : écran partagé. {Core.SplitScreenCheatSpawner.JoinPlayer()}");
            if (PlayerInput.all.Count < 2)
                Debug.LogWarning("[PocketSize] Il faut deux joueurs : le petit sera posé sur la table dès que le deuxième arrive (C+P).");

            StartRound(match, 0);
        }

        // Swaps each tiny one's body for the small one, on the felt, as soon
        // as they're there and ready. A body just there is given two frames:
        // its puppet and controllers start up first.
        private void TryPlaceTinies()
        {
            foreach (int p in tinies)
            {
                if (tinyBodies[p] != null || isOut[p]) continue;
                PlayerInput player = PlayerInput.GetPlayerByIndex(p);
                if (player == null) continue;
                if (player != tinyWaitedFor[p])
                {
                    tinyWaitedFor[p] = player;
                    tinyReadyFrame[p] = Time.frameCount + 2;
                }
                if (Time.frameCount < tinyReadyFrame[p]) continue;
                PocketSizeSettings s = Settings;
                Vector3 spawn = tinySpawns[p];
                tinyBodies[p] = LocalPlayerShrink.Shrink(player, s.tinyPrefab, PlayerInputManager.instance.playerPrefab,
                    spawn, FacingCueBall(spawn), s.tinyBlocksBalls ? ballLayers : 0, s.tinyNearClip, s.tinyJumpHeight, s.tinyFallStiffness, s.tinyFallGravity, s.tinyFallDamper);
                if (s.debugLogs) Debug.Log($"[PocketSize] J{p + 1} : corps du petit ({s.tinyPrefab.name}) posé sur la table à {spawn}.", tinyBodies[p]);
            }
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

        // Another mode comes next: everyone full size, the extra cue balls
        // gone, the table's cue ball shared again.
        public static new void Cleanup()
        {
            Current = null;
            LocalPlayerShrink.RestoreAll();
            ReleaseCueBalls(ref tableCue, cues);
        }

        private void StartRound(PoolMatchRules match, int round)
        {
            PocketSizeSettings s = Settings;
            Round = round;
            CurrentPhase = Phase.Countdown;
            phaseTime = 0f;
            LocalPlayerShrink.RestoreAll();
            Array.Clear(tinyBodies, 0, tinyBodies.Length);
            Array.Clear(tinyWaitedFor, 0, tinyWaitedFor.Length);
            // Powers belong to a role: a stock held over would be the wrong one.
            match.ClearHeldPowers();
            slowCueUntil = slowTinyUntil = shotBoostUntil = extraCueUntil = -1f;
            for (int p = 0; p < sprintUntil.Length; p++) sprintUntil[p] = -1f;
            respots.Clear();
            lastVelocity.Clear();

            AssignRoles();
            Array.Clear(hitsTaken, 0, hitsTaken.Length);
            Array.Clear(timeSurvived, 0, timeSurvived.Length);
            Array.Clear(isOut, 0, isOut.Length);
            Array.Clear(invulnerableUntil, 0, invulnerableUntil.Length);
            Array.Clear(downFromTouch, 0, downFromTouch.Length);
            Array.Clear(downFromBlow, 0, downFromBlow.Length);

            PoolTableSurface surface = PoolTableSurface.Instance;
            PoolBall first = FirstCue;
            Vector3 head = first.SpawnPosition;
            feltY = surface != null ? surface.FeltTopY : head.y - first.Radius;
            Vector3 across = surface != null ? surface.transform.rotation * Vector3.forward : Vector3.forward;

            // The giants' cue balls on the head spot, side by side; the
            // second one of Double Cue Ball beside the first, put away.
            for (int i = 0; i < giantCueCount; i++)
            {
                PoolBall cue = cues[i];
                if (i >= giants.Count)
                {
                    cue.gameObject.SetActive(false);
                    continue;
                }
                cue.gameObject.SetActive(true);
                float offset = (i - (giants.Count - 1) * 0.5f) * first.Radius * 5f;
                cue.MakePlayerCueBall(giants[i], head + across * offset, s.giantCueColor);
            }
            PoolBall extra = ExtraCue;
            extra.gameObject.SetActive(true);
            extra.MakePlayerCueBall(giants[0], head + across * (first.Radius * 3f), s.giantCueColor);
            extra.gameObject.SetActive(false);

            // The tiny ones start around the foot spot, across the table from
            // the cue balls, spread across its width.
            Vector3 foot = head;
            if (surface != null)
            {
                Vector3 center = surface.transform.position;
                Vector3 along = surface.transform.rotation * Vector3.right;
                if (Vector3.Dot(head - center, along) > 0f) along = -along;
                foot = center + along * (surface.HalfLength * 0.5f);
            }
            float halfWidth = surface != null ? surface.HalfWidth : 0.5f;
            for (int i = 0; i < tinies.Count; i++)
            {
                Vector3 spawn = foot + across * ((i - (tinies.Count - 1) * 0.5f) * halfWidth * 0.6f);
                spawn.y = feltY + 0.01f;
                tinySpawns[tinies[i]] = spawn;
            }

            ScatterObstacles();

            if (s.debugLogs)
                Debug.Log($"[PocketSize] Manche {round + 1}/{RoundCount} : {Names(giants)} grand{(giants.Count > 1 ? "s" : "")}, {Names(tinies)} petit{(tinies.Count > 1 ? "s" : "")}. Chasse dans {s.countdown:0.#} s.");
            TryPlaceTinies();
        }

        // At two, the roles swap; each for themselves, one giant in turn;
        // in 2 v 2, one team then the other.
        private void AssignRoles()
        {
            giants.Clear();
            tinies.Clear();
            int first = Settings.firstGiant;
            int giantSide = (PlayerBallGroups.SideOf(first % players) + Round) % 2;
            int giant = (first + Round) % players;
            for (int p = 0; p < players; p++)
            {
                isGiant[p] = teams ? PlayerBallGroups.SideOf(p) == giantSide : p == giant;
                (isGiant[p] ? giants : tinies).Add(p);
            }
            for (int p = players; p < isGiant.Length; p++) isGiant[p] = false;
        }

        private static string Names(List<int> list)
        {
            string names = "";
            foreach (int p in list) names += (names.Length > 0 ? ", " : "") + $"J{p + 1}";
            return names;
        }

        private Quaternion FacingCueBall(Vector3 from)
        {
            Vector3 look = FirstCue.SpawnPosition - from;
            look.y = 0f;
            return look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look) : Quaternion.identity;
        }

        // Obstacle Balls coloured balls at random spots, clear of the
        // pockets, the cue balls and the tiny ones' starts; the rest (and
        // Speed Pool's extra copies) put away for the round.
        private void ScatterObstacles()
        {
            var balls = new List<PoolBall>();
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                if (ball.IsCueBall) continue;
                if (ball.IsExtra || ball.InSecondRack) ball.gameObject.SetActive(false);
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
        // cue balls, the tiny ones' starts and the balls already placed.
        // Falls back to the nearest free spot around the table's centre.
        private Vector3 FeltSpot(float radius, float y)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null) return new Vector3(tinySpawns[0].x, y, tinySpawns[0].z);
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
            foreach (int t in tinies)
                if (Flat(p - tinySpawns[t]).sqrMagnitude < clearance * clearance) return false;
            foreach (PoolBall ball in PoolBall.Active)
                if (ball.IsCueBall && Flat(p - ball.transform.position).sqrMagnitude < clearance * clearance) return false;
            float apart = radius * 3f;
            foreach (Vector3 other in placed)
                if (Flat(p - other).sqrMagnitude < apart * apart) return false;
            return true;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private int TiniesLeft
        {
            get
            {
                int left = 0;
                foreach (int p in tinies) if (!isOut[p]) left++;
                return left;
            }
        }

        // ---------- Every frame (PoolMatchRules.Update) ----------

        public override void Tick(PoolMatchRules match)
        {
            // No cue ball on the table (Setup said so): nothing to play.
            if (match.GameOver || cues.Count == 0 || FirstCue == null) return;
            PocketSizeSettings s = Settings;
            phaseTime += Time.deltaTime;
            UpdateRespots();
            TryPlaceTinies();

            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    // Free to find their footing: a fall doesn't count yet,
                    // and a body moved off the table goes back on it.
                    foreach (int p in tinies)
                    {
                        LocalPlayerShrink tiny = tinyBodies[p];
                        if (tiny == null || OnTable(tiny)) continue;
                        if (s.debugLogs) Debug.Log($"[PocketSize] J{p + 1} n'est pas sur la table ({tiny.transform.position}) pendant le compte à rebours : reposé à {tinySpawns[p]}.", tiny);
                        tiny.Teleport(tinySpawns[p], FacingCueBall(tinySpawns[p]));
                    }
                    if (phaseTime < s.countdown) break;
                    CurrentPhase = Phase.Hunt;
                    phaseTime = 0f;
                    if (s.debugLogs) Debug.Log($"[PocketSize] Chasse ! {Names(giants)} peu{(giants.Count > 1 ? "vent" : "t")} tirer.");
                    break;

                case Phase.Hunt:
                    foreach (int p in tinies) if (!isOut[p]) timeSurvived[p] = phaseTime;
                    UpdatePowers();
                    foreach (int p in tinies)
                        if (tinyBodies[p] != null && !isOut[p]) CheckFall(p, tinyBodies[p]);
                    CheckHits();
                    foreach (int p in tinies)
                        if (!isOut[p] && hitsTaken[p] >= s.hitsToLose) TakeOut(p);
                    if (TiniesLeft == 0) EndRound(false);
                    else if (phaseTime >= s.roundDuration) EndRound(true);
                    break;

                case Phase.RoundOver:
                    if (phaseTime < s.roundOverTime) break;
                    if (Round + 1 < RoundCount) StartRound(match, Round + 1);
                    else Finish(match);
                    break;
            }
        }

        // Out of the round: back to full size, beside the table, while the
        // others play on. The last one out ends the round (Tick).
        private void TakeOut(int player)
        {
            isOut[player] = true;
            timeSurvived[player] = phaseTime;
            if (Settings.debugLogs) Debug.Log($"[PocketSize] J{player + 1} éliminé ({hitsTaken[player]} touches, {phaseTime:0.0} s).");
            if (TiniesLeft == 0) return;
            LocalPlayerShrink body = tinyBodies[player];
            tinyBodies[player] = null;
            if (body != null) body.Restore();
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

        private void UpdatePowers()
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
            float slow = now < slowTinyUntil ? slowTinyMultiplier : 1f;
            foreach (int p in tinies)
                if (tinyBodies[p] != null) tinyBodies[p].SetSpeedMultiplier((now < sprintUntil[p] ? sprintMultiplier : 1f) * slow);
        }

        private bool IsGiantCueBall(PoolBall ball) =>
            ball.IsCueBall && IsGiant(ball.CueOwner) && !ball.Rigidbody.isKinematic && ball.gameObject.activeInHierarchy;

        // Parked first: an aim on it ends (LocalPoolAimController leaves a
        // kinematic cue ball).
        private void PutExtraCueAway()
        {
            PoolBall extra = ExtraCue;
            if (extra == null || !extra.gameObject.activeSelf) return;
            extra.BeginBallInHand();
            extra.gameObject.SetActive(false);
        }

        // Under the felt (a pocket, off the table): a touch while hunting,
        // and back onto the felt either way. Not counted when the touch that
        // threw it there already was (still invulnerable).
        private void CheckFall(int player, LocalPlayerShrink tiny)
        {
            Vector3 where = tiny.BodyPosition;
            if (where.y > feltY - Settings.fallDepth) return;
            // Knocked into a pocket by another tiny one: their blow, not a touch.
            bool counted = Time.time >= invulnerableUntil[player] && !downFromBlow[player];
            Vector3 spot = FeltSpot(0.03f, feltY + 0.01f);
            tiny.Teleport(spot, FacingCueBall(spot));
            invulnerableUntil[player] = Time.time + Settings.invulnerableTime;
            if (counted)
            {
                hitsTaken[player]++;
                totalHits[player]++;
                TinyHit?.Invoke(player, hitsTaken[player], where);
            }
            if (Settings.debugLogs)
                Debug.Log($"[PocketSize] J{player + 1} tombe de la table à {where}{(counted ? $" : touche {hitsTaken[player]}/{Settings.hitsToLose}" : "")}, replacé à {spot}.");
        }

        // Any ball rolling fast enough into a tiny one's capsule counts for
        // the giants, as long as a giant's shot set it moving: a cue ball, or
        // a coloured ball it sent flying. A
        // distance test rather than collision callbacks: with Tiny Blocks
        // Balls the ball bounces off the body before overlapping it (Hit
        // Margin). None counts while the tiny one is down, nor for
        // Invulnerable Time once they're back up.
        private void CheckHits()
        {
            PocketSizeSettings s = Settings;
            foreach (int p in tinies)
            {
                hitBy[p] = null;
                LocalPlayerShrink tiny = tinyBodies[p];
                if (tiny == null) continue;
                if (tiny.IsDown)
                {
                    // Down from a touch: protected the whole time. Not from a
                    // touch: another tiny one's blow, and slowing them down
                    // for the giant is the point of it.
                    if (downFromTouch[p]) invulnerableUntil[p] = Time.time + s.invulnerableTime;
                    else downFromBlow[p] = true;
                }
                else downFromTouch[p] = downFromBlow[p] = false;
            }
            foreach (PoolBall ball in PoolBall.Active)
            {
                // Last frame's velocity too, kept for every ball every frame:
                // the ball now collides with the body and may have bounced
                // off it already, in the physics steps before this frame.
                Vector3 now = ball.Rigidbody.linearVelocity;
                lastVelocity.TryGetValue(ball, out Vector3 before);
                lastVelocity[ball] = now;
                // Only what a giant's shot set moving: not a ball a tiny one
                // kicked (MarkHitBy), nor one never shot.
                if (ball.Rigidbody.isKinematic || !IsGiant(ball.LastShooter)) continue;
                Vector3 c = ball.transform.position;
                foreach (int p in tinies)
                {
                    LocalPlayerShrink tiny = tinyBodies[p];
                    if (tiny == null || isOut[p] || hitBy[p] != null || Time.time < invulnerableUntil[p]) continue;
                    tiny.GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
                    Vector3 nearest = ClosestOnSegment(bottom, top, c);
                    // Coming at the tiny one, fast enough, either then or now:
                    // a ball rolling away (bounced off them already) doesn't count.
                    Vector3 incoming = Coming(now, nearest - c, s.minHitSpeed) ? now
                                     : Coming(before, nearest - c, s.minHitSpeed) ? before : Vector3.zero;
                    if (incoming == Vector3.zero) continue;
                    // Plus what it rolled in a frame: it may be off the body again.
                    float reach = radius + ball.Radius + s.hitMargin + incoming.magnitude * Time.deltaTime;
                    if ((c - nearest).sqrMagnitude > reach * reach) continue;
                    hitBy[p] = ball;
                    hitVelocity[p] = incoming;
                    hitPoint[p] = nearest;
                }
            }
            foreach (int p in tinies)
                if (hitBy[p] != null) Hit(p, tinyBodies[p], hitBy[p], hitVelocity[p], hitPoint[p]);
        }

        private static bool Coming(Vector3 velocity, Vector3 toTiny, float minSpeed) =>
            velocity.sqrMagnitude >= minSpeed * minSpeed && Vector3.Dot(Flat(velocity), Flat(toTiny)) > 0f;

        // A touch: counted, and the tiny one falls, thrown by the impact. How
        // hard: the ball's speed into them (head on, all of it; grazing,
        // little), times Knock Transfer. Which way: between where the ball
        // was rolling and straight away from the point of impact (Knock
        // Along Impact). velocity is coming at them, never flat zero.
        private void Hit(int player, LocalPlayerShrink tiny, PoolBall ball, Vector3 velocity, Vector3 closest)
        {
            PocketSizeSettings s = Settings;
            hitsTaken[player]++;
            totalHits[player]++;
            invulnerableUntil[player] = Time.time + s.invulnerableTime;
            Vector3 rolling = Flat(velocity).normalized;
            Vector3 away = Flat(closest - ball.transform.position);
            away = away.sqrMagnitude > 1e-8f ? away.normalized : rolling;
            float into = Mathf.Max(Vector3.Dot(Flat(velocity), away), 0f);
            float speed = Mathf.Min(into * s.knockTransfer, s.knockMaxSpeed);
            Vector3 direction = Vector3.Slerp(rolling, away, s.knockAlongImpact).normalized;
            downFromTouch[player] = true;
            downFromBlow[player] = false;
            tiny.KnockDown((direction + Vector3.up * s.knockLift) * speed, s.debugLogs, ball.Rigidbody);
            TinyHit?.Invoke(player, hitsTaken[player], closest);
            if (s.debugLogs)
                Debug.Log($"[PocketSize] J{player + 1} touché par {(ball.IsCueBall ? $"la blanche de J{ball.CueOwner + 1}" : $"la bille {ball.Number}")} ({velocity.magnitude:0.00} m/s, {into:0.00} m/s vers lui) à {closest} : {hitsTaken[player]}/{s.hitsToLose}, projeté à {speed:0.00} m/s.", ball);
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSqr);
        }

        // Points: every giant's, or every tiny one's by rank of touches taken
        // (the least touched first, level touches the same rank).
        private void EndRound(bool survived)
        {
            PocketSizeSettings s = Settings;
            CurrentPhase = Phase.RoundOver;
            phaseTime = 0f;
            tiniesWon = survived;
            PutExtraCueAway();
            foreach (int p in tinies) totalSurvived[p] += timeSurvived[p];

            int representative = giants[0];
            if (survived)
            {
                int best = int.MaxValue;
                foreach (int p in tinies)
                {
                    int rank = 0;
                    foreach (int q in tinies) if (hitsTaken[q] < hitsTaken[p]) rank++;
                    points[p] += RankPoints(rank);
                    if (hitsTaken[p] < best) { best = hitsTaken[p]; representative = p; }
                }
            }
            else
            {
                foreach (int p in giants) points[p] += s.giantWinPoints;
            }
            RoundEnded?.Invoke(representative, survived);
            if (s.debugLogs)
                Debug.Log($"[PocketSize] Fin de la manche {Round + 1} : {(survived ? $"{Names(tinies)} {(tinies.Count > 1 ? "ont" : "a")} tenu" : $"tous les petits touchés, en {phaseTime:0.0} s")} → points : {DescribePoints()}.");
        }

        private static int RankPoints(int rank)
        {
            int[] table = Settings.tinyRankPoints;
            if (table == null || table.Length == 0) return 0;
            return table[Mathf.Min(rank, table.Length - 1)];
        }

        private string DescribePoints()
        {
            string text = "";
            for (int p = 0; p < players; p++) text += (p > 0 ? ", " : "") + $"J{p + 1} {points[p]}";
            return text;
        }

        // Most points (a team's, added up, in 2 v 2); level, the longer
        // survival as a tiny one, then the fewer touches, then a draw.
        private void Finish(PoolMatchRules match)
        {
            LocalPlayerShrink.RestoreAll();
            int sides = teams ? 2 : players;
            string why;
            var level = new List<int>();
            for (int side = 0; side < sides; side++) level.Add(side);
            Narrow(level, side => SideTotal(side, p => points[p]));
            if (level.Count == 1) why = $"le plus de points ({DescribePoints()})";
            else
            {
                Narrow(level, side => SideTotal(side, p => totalSurvived[p]));
                if (level.Count == 1) why = $"a tenu le plus longtemps en petit ({SideTotal(level[0], p => totalSurvived[p]):0.0} s)";
                else
                {
                    Narrow(level, side => -SideTotal(side, p => totalHits[p]));
                    why = level.Count == 1 ? $"le moins touché en petit ({SideTotal(level[0], p => totalHits[p]):0} touches)" : "égalité parfaite, tiré au sort";
                }
            }
            // In 2 v 2 the side is J1's or J2's team: J1 or J2 wins it.
            int winner = level[Random.Range(0, level.Count)];
            if (Settings.debugLogs) Debug.Log($"[PocketSize] Victoire de {(teams ? $"l'équipe de J{winner + 1}" : $"J{winner + 1}")} : {why}.");
            match.Win(winner);
        }

        // A side's total: the player's own each for themselves, the team's in 2 v 2.
        private float SideTotal(int side, Func<int, float> value)
        {
            if (!teams) return value(side);
            float total = 0f;
            for (int p = 0; p < players; p++) if (PlayerBallGroups.SideOf(p) == side) total += value(p);
            return total;
        }

        // Keeps those with the highest value (within 0.05).
        private static void Narrow(List<int> sides, Func<int, float> value)
        {
            float best = float.MinValue;
            foreach (int side in sides) best = Mathf.Max(best, value(side));
            sides.RemoveAll(side => value(side) < best - 0.05f);
        }

        // Coloured balls come back after Respot Delay; a cue ball comes back
        // through PoolMatchRules (Speed Pool's respawn delay).
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (!ball.IsCueBall) respots.Add((ball, Time.time + Settings.respotDelay));
        }

        // ---------- Crates (PoolPowerCrate) ----------

        // True when someone took the crate at position: a tiny one walking
        // onto it (Tiny Crate Reach) or one of the giants' cue balls rolling
        // over it (cueReach, the crate's own pickup radius). A Pocket Size
        // power goes to its role, the other role only destroys it; another
        // mode's power goes to whoever takes it.
        public bool TryTakeCrate(PoolMatchRules match, Vector3 position, float cueReach, PoolPower power)
        {
            if (CurrentPhase != Phase.Hunt) return false;
            int taker = -1;
            float tinyReach = Settings.tinyCrateReach;
            foreach (int p in tinies)
            {
                LocalPlayerShrink tiny = tinyBodies[p];
                if (tiny == null || isOut[p] || tiny.IsDown) continue;
                if (Flat(tiny.transform.position - position).sqrMagnitude > tinyReach * tinyReach) continue;
                taker = p;
                break;
            }
            if (taker < 0)
            {
                foreach (PoolBall ball in PoolBall.Active)
                {
                    if (!IsGiantCueBall(ball)) continue;
                    float reach = cueReach + ball.Radius;
                    if ((ball.transform.position - position).sqrMagnitude > reach * reach) continue;
                    taker = ball.CueOwner;
                    break;
                }
            }
            if (taker < 0) return false;

            // Another mode's power (Pocket Size ticked in its Only In Modes)
            // has no role: whoever takes it keeps it.
            bool own = !(power is PocketSizePower pocketPower) || pocketPower.Role == RoleOf(taker);
            if (own) match.GrantPower(taker, power);
            if (Settings.debugLogs)
                Debug.Log($"[PocketSize] Caisse « {power.PowerName} » {(own ? "ramassée" : "détruite")} par J{taker + 1} ({(IsGiant(taker) ? "grand" : "petit")}).");
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

        // Each of the giants' cue balls rolls off towards its nearest open pocket.
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

        // Only the tiny one who took it.
        public void BoostTiny(int player, float duration, float multiplier)
        {
            if (player < 0 || player >= sprintUntil.Length) return;
            sprintUntil[player] = Time.time + duration;
            sprintMultiplier = multiplier;
            Trace($"J{player + 1} plus rapide ×{multiplier:0.##} pendant {duration:0.#} s");
        }

        // Every tiny one.
        public void SlowTiny(float duration, float multiplier)
        {
            slowTinyUntil = Time.time + duration;
            slowTinyMultiplier = multiplier;
            Trace($"petits ralentis ×{multiplier:0.##} pendant {duration:0.#} s");
        }

        public void BoostShots(float duration, float multiplier)
        {
            shotBoostUntil = Time.time + duration;
            shotBoostMultiplier = multiplier;
            Trace($"tirs ×{multiplier:0.##} pendant {duration:0.#} s");
        }

        // The second cue ball beside the player's own (or as near as is
        // free), theirs; a second use while it's out only extends it.
        public void AddSecondCueBall(int player, float duration)
        {
            extraCueUntil = Time.time + duration;
            PoolBall extra = ExtraCue;
            if (extra == null || extra.gameObject.activeSelf) return;
            PoolBall own = FirstCue;
            for (int i = 0; i < giantCueCount; i++)
                if (cues[i].CueOwner == player && cues[i].gameObject.activeSelf) own = cues[i];
            Vector3 beside = own.transform.position + (extra.RespawnPosition - FirstCue.RespawnPosition);
            extra.gameObject.SetActive(true);
            extra.MakePlayerCueBall(player, extra.RespawnPosition, Settings.giantCueColor);
            extra.PlaceAt(PoolMatchRules.FindFreeSpot(new Vector3(beside.x, extra.RespawnPosition.y, beside.z), extra));
            Trace($"deuxième blanche pour J{player + 1} pendant {duration:0.#} s");
        }

        private static void Trace(string what)
        {
            if (Settings.debugLogs) Debug.Log($"[PocketSize] Pouvoir : {what}.");
        }

        // ---------- HUD ----------

        public string RoleLabel(int player) => IsGiant(player) ? Settings.giantLabel : Settings.tinyLabel;

        // What the plate counts: a tiny one's own touches; the giant's, the
        // only tiny one's touches, or the tiny ones still in.
        private int Shown(int player)
        {
            if (!IsGiant(player)) return hitsTaken[player];
            return tinies.Count == 1 ? hitsTaken[tinies[0]] : TiniesLeft;
        }

        // A side's points: the player's, or the team's.
        // No delegate: the plate asks every frame.
        private int PointsOf(int player)
        {
            if (!teams) return player >= 0 && player < points.Length ? points[player] : 0;
            int side = PlayerBallGroups.SideOf(player), total = 0;
            for (int p = 0; p < players; p++) if (PlayerBallGroups.SideOf(p) == side) total += points[p];
            return total;
        }

        // Round, then the phase: countdown, clock and touches, or the result.
        // At more than two, the points after it.
        public string DescribeRound(int player)
        {
            PocketSizeSettings s = Settings;
            string round = string.Format(s.roundFormat, Round + 1, RoundCount);
            string text;
            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    text = $"{round}  ·  {string.Format(s.countdownFormat, Mathf.CeilToInt(CountdownLeft))}";
                    break;
                case Phase.Hunt:
                    int left = Mathf.CeilToInt(TimeLeft);
                    string count = !IsGiant(player) && isOut[player] ? s.tinyOutText
                        : IsGiant(player) && tinies.Count > 1 ? string.Format(s.tiniesLeftFormat, TiniesLeft, tinies.Count)
                        : string.Format(s.hitsFormat, Shown(player), s.hitsToLose);
                    text = $"{round}  ·  {left / 60}:{left % 60:00}  ·  {count}";
                    break;
                default:
                    bool won = IsGiant(player) != tiniesWon;
                    text = $"{round}  ·  {(won ? s.roundWonText : s.roundLostText)}";
                    break;
            }
            return players > 2 ? $"{text}  ·  {string.Format(s.pointsFormat, PointsOf(player))}" : text;
        }

        // Changes whenever DescribeRound would.
        public int PlateKey(int player)
        {
            int seconds = CurrentPhase == Phase.Countdown ? Mathf.CeilToInt(CountdownLeft) : Mathf.CeilToInt(TimeLeft);
            int state = (IsGiant(player) ? 1 : 0) + (isOut[Mathf.Clamp(player, 0, isOut.Length - 1)] ? 2 : 0) + (tiniesWon ? 4 : 0);
            return (((Round * 4 + (int)CurrentPhase) * 1000 + seconds) * 8 + Mathf.Min(Shown(player), 7)) * 1024
                + Mathf.Min(PointsOf(player), 127) * 8 + state;
        }

        public override string DescribePlayer(int player) => $"{RoleLabel(player)} · {DescribeRound(player)}";
    }
}
