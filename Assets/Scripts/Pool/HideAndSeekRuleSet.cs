using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace UntitledPoolGame.Pool
{
    // Hide and seek: while the seekers keep their eyes shut (nothing enforces
    // it, the players next to them are trusted), the hiders, shrunk on the
    // felt, each take the place of a ball. They then roll it from a view from
    // above with no mark on it, among decoys that roll the same way, and the
    // table shakes now and then. The seekers have a few shots each and a
    // clock: a cue ball's first contact with a hider's ball finds them, and a
    // fall into a pocket gives it away. A swap of ball reaches the seekers'
    // plates after a random delay, or the message would show which ball had
    // just set off. A hider who stays put too long is warned, then their ball
    // rolls off by itself (UpdateMustMove).
    //
    // At two: one seeker, one hider, the roles swap every round, first to
    // Points To Win. At three or four each for themselves: one seeker against
    // all the others, each player the seeker in turn (Rounds Per Seeker
    // times); in 2 v 2, one team seeks, then the other. A round scores points:
    // every seeker's when all the hiders are found (Seeker Win Points), else
    // every hider's, the best hidden first (Hider Rank Points). Most points
    // after the last round wins. Each round the seekers are given a power
    // (Seeker Power, the Sonar: Ping).
    //
    // Derived from SpeedPoolRuleSet for the cue balls owned by a player
    // (PoolBall.CueOwner, which LocalPoolAimController aims), with its own
    // table as in Pocket Size. Speed Pool only, no powers (PoolMatchSetup).
    // PoolMatchRules calls Tick every frame, CanShoot and OnShot.
    public class HideAndSeekRuleSet : SpeedPoolRuleSet
    {
        public enum Phase { Hiding, Search, RoundOver }

        // How far from the stick's direction a target may be (cos 60°).
        private const float AimCone = 0.5f;
        // Below this, the stick gives no direction.
        private const float StickDeadZone = 0.3f;
        // A shot's cue ball may not have started rolling yet.
        private const float LastShotGrace = 0.5f;
        // How far the Sonar's centre may be from the ball, in share of its
        // radius: the ball stays inside the zone.
        private const float PingOffCentre = 0.85f;
        private const float PingAboveFelt = 0.003f;
        private const int Max = PoolMatchRules.MaxPlayers;

        // The match under way, null in other modes.
        public static HideAndSeekRuleSet Current { get; private set; }

        private static HideAndSeekSettings cfg;
        public static HideAndSeekSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<HideAndSeekSettings>("HideAndSeekSettings");

        // A cue ball per seeker (TakeCueBalls), handed back by Cleanup.
        private static PoolBall tableCue;
        private static readonly List<PoolBall> cues = new List<PoolBall>();

        public Phase CurrentPhase { get; private set; }

        private int players = 2;
        private bool teams;
        private int round;
        private float phaseTime;
        // This round's sides.
        private readonly List<int> seekers = new List<int>();
        private readonly List<int> hiders = new List<int>();
        private readonly bool[] isSeeker = new bool[Max];
        public bool IsSeeker(int player) => player >= 0 && player < Max && isSeeker[player];
        private int RoundCount => (teams ? 2 : players) * Mathf.Max(1, Settings.roundsPerSeeker);

        private readonly int[] points = new int[Max];
        // Over the match, as a hider: time before being found (tie-break).
        private readonly float[] totalHidden = new float[Max];
        private readonly int[] shotsLeft = new int[Max];
        private float lastShotTime;
        // Who won the round: the seekers (all found) or the hiders. At two,
        // the player who scored.
        private bool seekersWon;
        private int roundWinner = -1;

        private float tableScale = 1f;
        private readonly List<HideAndSeekBall> tableBalls = new List<HideAndSeekBall>();
        private readonly List<Vector3> placed = new List<Vector3>();
        // Off the table: OnPocketed turns it off just after HandlePocketed.
        private readonly List<PoolBall> toRespot = new List<PoolBall>();
        private float feltY;

        // Per hider: their small body, once placed, then their ball, their
        // controls, their view from above, their tricks left, and when they
        // were found (-1 = not yet).
        private readonly LocalPlayerShrink[] tinyBodies = new LocalPlayerShrink[Max];
        private readonly PlayerInput[] waitedFor = new PlayerInput[Max];
        private readonly int[] readyFrame = new int[Max];
        private readonly Vector3[] tinySpawns = new Vector3[Max];
        private readonly HideAndSeekBall[] hiddenBalls = new HideAndSeekBall[Max];
        private readonly InputAction[] moveActions = new InputAction[Max];
        private readonly InputAction[] swapActions = new InputAction[Max];
        private readonly InputAction[] fakeActions = new InputAction[Max];
        private readonly GameObject[] cameraObjects = new GameObject[Max];
        private readonly int[] swapsLeft = new int[Max];
        private readonly int[] fakesLeft = new int[Max];
        private readonly float[] foundAt = new float[Max];

        // What the seekers know of the swaps: when the next message comes, 0 =
        // none on its way, when the one up goes, and the swaps left as last told.
        private float swapAlertAt, swapAlertUntil;
        private int swapsTold;

        private float nextDecoy, nextShake;
        private readonly Gamepad[] rumbling = new Gamepad[Max];
        private readonly float[] rumbleUntil = new float[Max];

        // Where each hider's ball last counted as having moved, when it has
        // to have moved again, and until when it rolls by itself.
        private readonly Vector3[] moveAnchor = new Vector3[Max];
        private readonly float[] moveDeadline = new float[Max];
        private readonly float[] forcedUntil = new float[Max];
        private readonly bool[] moveWarned = new bool[Max];

        // The Sonar's zone on the felt, and when it goes.
        private GameObject pingZone;
        private float pingUntil;

        public override bool AllowsShot(PoolMatchRules match, int playerIndex) => CanShoot(playerIndex);

        public override void OnShotFired(PoolBall cueBall) => OnShot(cueBall != null ? cueBall.CueOwner : -1);

        public override bool AllowsPower(int player) => CanPing(player);

        public bool CanShoot(int player) => CurrentPhase == Phase.Search && IsSeeker(player) && shotsLeft[player] > 0;

        private bool IsHidden(int hider) => hiddenBalls[hider] != null && foundAt[hider] < 0f;

        public override void Setup(PoolMatchRules match)
        {
            // "Rejouer" comes straight back here, with the same mode.
            Cleanup();
            cfg = PoolSettingsLoader.LoadOrDefault<HideAndSeekSettings>("HideAndSeekSettings");
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null || PocketSizeRuleSet.Settings.tinyPrefab == null || PlayerInputManager.instance == null || PlayerInputManager.instance.playerPrefab == null)
            {
                Debug.LogError("[HideAndSeek] Il manque la table, le prefab du petit (PocketSizeSettings, Tiny Prefab) ou celui du joueur (PlayerInputManager, Player Prefab) : pas de partie.");
                return;
            }
            players = Mathf.Clamp(PlayerBallGroups.Players, 2, Max);
            teams = PlayerBallGroups.Teams;
            // A cue ball per seeker: two in 2 v 2.
            if (!TakeCueBalls(ref tableCue, teams ? 2 : 1, cues, "Hide And Seek")) return;
            Current = this;
            tableScale = surface.TableScale;
            feltY = surface.FeltTopY;
            Array.Clear(points, 0, points.Length);
            Array.Clear(totalHidden, 0, totalHidden.Length);
            PoolBall.CueBallFirstContact += OnCueContact;

            // Two players or nothing, as in Pocket Size.
            if (PlayerInput.all.Count < 2)
                Debug.Log($"[HideAndSeek] Un seul joueur : écran partagé. {Core.SplitScreenCheatSpawner.JoinPlayer()}");

            StartRound(0);
        }

        // Another mode comes next, or a new match of this one.
        public static new void Cleanup()
        {
            HideAndSeekRuleSet rules = Current;
            Current = null;
            if (rules != null)
            {
                PoolBall.CueBallFirstContact -= rules.OnCueContact;
                rules.ReleaseHiders();
                // Turned off, not destroyed: Setup calls this then scatters the
                // balls in the same frame ("Rejouer", or a restart when a player
                // joins), and would pick up a component Destroy only removes at
                // the end of the frame, leaving the rules with dead balls.
                foreach (HideAndSeekBall ball in rules.tableBalls)
                    if (ball != null) ball.Release();
                rules.tableBalls.Clear();
                PoolMatchRules match = PoolMatchRules.Instance;
                if (match != null) match.ClearHeldPowers();
            }
            ReleaseCueBalls(ref tableCue, cues);
        }

        // The hiders' cameras gone, everyone back to full size.
        private void ReleaseHiders()
        {
            for (int p = 0; p < Max; p++)
            {
                StopRumble(p);
                if (cameraObjects[p] != null) Object.Destroy(cameraObjects[p]);
                cameraObjects[p] = null;
                hiddenBalls[p] = null;
                tinyBodies[p] = null;
                waitedFor[p] = null;
            }
            ClearPing();
            LocalPlayerShrink.RestoreAll();
        }

        private void StartRound(int index)
        {
            HideAndSeekSettings s = Settings;
            ReleaseHiders();
            round = index;
            roundWinner = -1;
            seekersWon = false;
            AssignRoles();
            swapAlertAt = swapAlertUntil = 0f;
            toRespot.Clear();
            for (int p = 0; p < Max; p++)
            {
                shotsLeft[p] = isSeeker[p] ? s.shotsPerRound : 0;
                swapsLeft[p] = isSeeker[p] ? 0 : s.swapsPerRound;
                fakesLeft[p] = isSeeker[p] ? 0 : s.fakesPerRound;
                foundAt[p] = -1f;
                forcedUntil[p] = 0f;
            }
            swapsTold = SwapsLeftInPlay;

            // The seekers' cue balls on the head spot, side by side.
            PoolBall first = cues[0];
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 across = surface.transform.rotation * Vector3.forward;
            for (int i = 0; i < cues.Count; i++)
            {
                if (i >= seekers.Count)
                {
                    cues[i].gameObject.SetActive(false);
                    continue;
                }
                cues[i].gameObject.SetActive(true);
                float offset = (i - (seekers.Count - 1) * 0.5f) * first.Radius * 5f;
                cues[i].MakePlayerCueBall(seekers[i], first.SpawnPosition + across * offset, Color.white);
            }

            // An unused power doesn't follow a seeker into the hider's role.
            PoolMatchRules match = PoolMatchRules.Instance;
            match.ClearHeldPowers();
            foreach (int p in seekers) match.GrantPower(p, s.seekerPower);

            // The small ones start around the foot spot, across the table
            // from the cue balls, spread across its width.
            Vector3 center = surface.transform.position;
            Vector3 along = surface.transform.rotation * Vector3.right;
            if (Vector3.Dot(first.SpawnPosition - center, along) > 0f) along = -along;
            Vector3 foot = center + along * (surface.HalfLength * 0.5f);
            for (int i = 0; i < hiders.Count; i++)
            {
                Vector3 spawn = foot + across * ((i - (hiders.Count - 1) * 0.5f) * surface.HalfWidth * 0.6f);
                spawn.y = feltY + 0.01f;
                tinySpawns[hiders[i]] = spawn;
            }

            ScatterBalls(surface);
            CurrentPhase = Phase.Hiding;
            phaseTime = 0f;
            if (s.debugLogs) Debug.Log($"[HideAndSeek] Manche {round + 1} : {Names(hiders)} se cache{(hiders.Count > 1 ? "nt" : "")}, {Names(seekers)} cherche{(seekers.Count > 1 ? "nt" : "")} ({s.shotsPerRound} tirs chacun).");
        }

        // At two, the roles swap; each for themselves, one seeker in turn; in
        // 2 v 2, one team then the other.
        private void AssignRoles()
        {
            seekers.Clear();
            hiders.Clear();
            int first = Settings.firstSeeker % players;
            int seekerSide = (PlayerBallGroups.SideOf(first) + round) % 2;
            int seeker = (first + round) % players;
            for (int p = 0; p < Max; p++)
            {
                isSeeker[p] = p < players && (teams ? PlayerBallGroups.SideOf(p) == seekerSide : p == seeker);
                if (p < players) (isSeeker[p] ? seekers : hiders).Add(p);
            }
        }

        private static string Names(List<int> list)
        {
            string names = "";
            foreach (int p in list) names += (names.Length > 0 ? ", " : "") + $"J{p + 1}";
            return names;
        }

        // Balls On Table balls at random spots, clear of the pockets, the cue
        // balls and the small ones' starts; the rest put away for the round.
        // The balls keep their rack spot as their spawn: "Rejouer" and the
        // next mode find them there.
        private void ScatterBalls(PoolTableSurface surface)
        {
            HideAndSeekSettings s = Settings;
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

            tableBalls.Clear();
            placed.Clear();
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (i >= s.ballsOnTable)
                {
                    ball.gameObject.SetActive(false);
                    continue;
                }
                ball.ResetToSpawn();
                Vector3 spot = FeltSpot(surface, ball.Radius, ball.SpawnPosition.y);
                placed.Add(spot);
                ball.PlaceAt(spot);
                if (!ball.TryGetComponent(out HideAndSeekBall driven)) driven = ball.gameObject.AddComponent<HideAndSeekBall>();
                driven.Prepare(ball, s.rollSpeed * tableScale, s.rollAcceleration * tableScale);
                tableBalls.Add(driven);
            }
        }

        private Vector3 FeltSpot(PoolTableSurface surface, float radius, float y)
        {
            Vector3 center = surface.transform.position;
            Vector3 along = surface.transform.rotation * Vector3.right;
            Vector3 across = surface.transform.rotation * Vector3.forward;
            float clearance = Settings.scatterClearance * tableScale;
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
            foreach (int h in hiders)
                if (Flat(p - tinySpawns[h]).sqrMagnitude < clearance * clearance) return false;
            foreach (PoolBall cue in cues)
                if (cue != null && cue.gameObject.activeSelf && Flat(p - cue.RespawnPosition).sqrMagnitude < clearance * clearance) return false;
            float apart = radius * 3f;
            foreach (Vector3 other in placed)
                if (Flat(p - other).sqrMagnitude < apart * apart) return false;
            return true;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // Swaps each hider's body for the small one, on the felt, as soon as
        // they're there and ready. A body just there is given two frames: its
        // puppet and controllers start up first.
        private void TryPlaceTinies()
        {
            foreach (int h in hiders)
            {
                if (tinyBodies[h] != null || hiddenBalls[h] != null) continue;
                PlayerInput player = PlayerInput.GetPlayerByIndex(h);
                if (player == null) continue;
                if (player != waitedFor[h])
                {
                    waitedFor[h] = player;
                    readyFrame[h] = Time.frameCount + 2;
                }
                if (Time.frameCount < readyFrame[h]) continue;
                PocketSizeSettings tiny = PocketSizeRuleSet.Settings;
                Vector3 spawn = tinySpawns[h];
                Vector3 look = Flat(cues[0].SpawnPosition - spawn);
                tinyBodies[h] = LocalPlayerShrink.Shrink(player, tiny.tinyPrefab, PlayerInputManager.instance.playerPrefab,
                    spawn, look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look) : Quaternion.identity,
                    LayerMask.GetMask("Poolball"), tiny.tinyNearClip, tiny.tinyJumpHeight, tiny.tinyFallStiffness, tiny.tinyFallGravity, tiny.tinyFallDamper);
                BindActions(h, tinyBodies[h].GetComponent<PlayerInput>());
            }
        }

        private void BindActions(int hider, PlayerInput player)
        {
            InputActionMap map = player.actions.FindActionMap("Player", throwIfNotFound: true);
            moveActions[hider] = map.FindAction("Move", throwIfNotFound: true);
            swapActions[hider] = map.FindAction("Interact", throwIfNotFound: true);
            // What launches a power elsewhere (LocalPoolPowerController).
            fakeActions[hider] = map.FindAction("Next", throwIfNotFound: true);
        }

        // ---------- Every frame (PoolMatchRules.Update) ----------

        public override void Tick(PoolMatchRules match)
        {
            if (match.GameOver || Current != this) return;
            HideAndSeekSettings s = Settings;
            phaseTime += Time.deltaTime;
            UpdateRumble();
            UpdateSwapAlert();
            if (pingZone != null && Time.time >= pingUntil) ClearPing();
            foreach (PoolBall ball in toRespot) match.RespotBall(ball);
            toRespot.Clear();

            switch (CurrentPhase)
            {
                case Phase.Hiding:
                    TryPlaceTinies();
                    foreach (int h in hiders) UpdateHiding(h);
                    if (phaseTime < s.hideTime) break;
                    // Time's up: wherever they are, they become the nearest free ball.
                    bool allHidden = true;
                    foreach (int h in hiders)
                    {
                        if (hiddenBalls[h] == null && tinyBodies[h] != null) TakeBall(h, NearestBall(tinyBodies[h].transform.position, float.PositiveInfinity));
                        if (hiddenBalls[h] == null) allHidden = false;
                    }
                    // A body wasn't there yet: nothing to look for.
                    if (!allHidden) break;
                    CurrentPhase = Phase.Search;
                    phaseTime = 0f;
                    // A swap while the seekers' eyes were shut sends no message.
                    swapsTold = SwapsLeftInPlay;
                    foreach (int h in hiders) ResetMoveClock(h);
                    nextDecoy = Time.time + Random.Range(s.decoyIntervalMin, s.decoyIntervalMax);
                    nextShake = Time.time + Random.Range(s.shakeIntervalMin, s.shakeIntervalMax);
                    if (s.debugLogs) Debug.Log($"[HideAndSeek] Recherche ! {Names(seekers)} peu{(seekers.Count > 1 ? "vent" : "t")} tirer.");
                    break;

                case Phase.Search:
                    foreach (int h in hiders)
                    {
                        if (!IsHidden(h)) continue;
                        UpdateMustMove(h);
                        UpdateHidden(h);
                    }
                    UpdateDecoys();
                    UpdateShake();
                    if (phaseTime >= s.searchTime) EndRound(false, "le temps est écoulé");
                    // The last shot has played out, without finding them all.
                    else if (NoShotLeft && Time.time > lastShotTime + LastShotGrace && !AnyCueRolling) EndRound(false, "plus de tirs");
                    break;

                case Phase.RoundOver:
                    if (phaseTime < s.roundOverTime) break;
                    if (players <= 2)
                    {
                        if (points[roundWinner] < s.pointsToWin)
                        {
                            StartRound(round + 1);
                            break;
                        }
                        if (s.debugLogs) Debug.Log($"[HideAndSeek] Victoire de J{roundWinner + 1} ({points[0]}-{points[1]}).");
                        ReleaseHiders();
                        match.Win(roundWinner);
                        break;
                    }
                    if (round + 1 < RoundCount)
                    {
                        StartRound(round + 1);
                        break;
                    }
                    ReleaseHiders();
                    match.Win(MatchWinner());
                    break;
            }
        }

        private bool NoShotLeft
        {
            get
            {
                foreach (int p in seekers) if (shotsLeft[p] > 0) return false;
                return true;
            }
        }

        private bool AnyCueRolling
        {
            get
            {
                foreach (PoolBall cue in cues) if (cue != null && cue.gameObject.activeInHierarchy && cue.IsRolling) return true;
                return false;
            }
        }

        // The swaps the hiders still in play have left: what a seeker can know.
        private int SwapsLeftInPlay
        {
            get
            {
                int left = 0;
                foreach (int h in hiders) if (foundAt[h] < 0f) left += swapsLeft[h];
                return left;
            }
        }

        private void UpdateHiding(int hider)
        {
            if (hiddenBalls[hider] != null)
            {
                UpdateHidden(hider);
                return;
            }
            LocalPlayerShrink body = tinyBodies[hider];
            if (body == null) return;
            // A body moved off the table, or fallen in a pocket, goes back on it.
            Vector3 p = body.transform.position;
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (p.y < feltY - PocketSizeRuleSet.Settings.fallDepth || Flat(surface.ClampToPlayArea(p, 0f) - p).sqrMagnitude > 0.05f * 0.05f)
                body.Teleport(tinySpawns[hider], body.transform.rotation);
            if (!swapActions[hider].WasPressedThisFrame()) return;
            HideAndSeekBall nearest = NearestBall(p, Settings.takeReach * tableScale);
            if (nearest != null) TakeBall(hider, nearest);
        }

        // Somebody's ball already, found or not: not to be taken, swapped
        // into or rolled as a decoy.
        private bool IsTaken(HideAndSeekBall ball)
        {
            foreach (int h in hiders) if (hiddenBalls[h] == ball) return true;
            return false;
        }

        private HideAndSeekBall NearestBall(Vector3 from, float reach)
        {
            HideAndSeekBall nearest = null;
            float best = reach * reach;
            foreach (HideAndSeekBall ball in tableBalls)
            {
                if (!ball.gameObject.activeInHierarchy || IsTaken(ball)) continue;
                float distance = Flat(ball.transform.position - from).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = ball;
            }
            return nearest;
        }

        // The small body stays for its PlayerInput and its Camera, which the
        // split screen and the HUD use, but does nothing and shows nothing, as in
        // Sumo Pool. Nothing to turn back on: LocalPlayerShrink swaps it for the
        // full-size one at the end of the round.
        private void TakeBall(int hider, HideAndSeekBall ball)
        {
            if (ball == null) return;
            hiddenBalls[hider] = ball;
            GameObject body = tinyBodies[hider].gameObject;
            Camera bodyCamera = body.GetComponentInChildren<Camera>(true);
            float aspect = bodyCamera != null && bodyCamera.aspect > 0f ? bodyCamera.aspect : 1f;
            foreach (MonoBehaviour script in body.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && SumoPoolBall.IsGameplayScript(script)) script.enabled = false;
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            if (body.TryGetComponent(out CharacterController controller)) controller.enabled = false;
            CreateCamera(hider, aspect);
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] J{hider + 1} prend la place de la bille {ball.Ball.Number}.", ball);
        }

        // Straight down, the length of the table across the view, the whole
        // table in it. Through Cinemachine on the player's channel, as in Sumo
        // Pool. No mark on the ball: the other players can see this part of
        // the screen.
        private void CreateCamera(int player, float aspect)
        {
            HideAndSeekSettings s = Settings;
            PoolTableSurface surface = PoolTableSurface.Instance;
            GameObject cameraObject = new GameObject($"Hide And Seek Camera J{player + 1}");
            cameraObjects[player] = cameraObject;
            CinemachineCamera cam = cameraObject.AddComponent<CinemachineCamera>();
            // The channel the body's Brain listens to (LocalFpsPlayerController).
            cam.OutputChannel = (OutputChannels)(1 << (player + 1));
            cam.Priority = 100;
            float tan = Mathf.Tan(s.cameraFov * 0.5f * Mathf.Deg2Rad);
            float height = Mathf.Max(surface.HalfWidth / tan, surface.HalfLength / (tan * aspect)) * s.cameraMargin;
            LensSettings lens = cam.Lens;
            lens.FieldOfView = s.cameraFov;
            lens.NearClipPlane = Mathf.Max(0.01f, height - s.cameraClearAbove * tableScale);
            cam.Lens = lens;
            cameraObject.transform.SetPositionAndRotation(
                surface.transform.position + Vector3.up * height,
                Quaternion.Euler(90f, surface.transform.eulerAngles.y, 0f));
        }

        private void UpdateSwapAlert()
        {
            if (swapAlertAt <= 0f || Time.time < swapAlertAt || CurrentPhase != Phase.Search) return;
            swapAlertAt = 0f;
            swapAlertUntil = Time.time + Settings.swapAlertTime;
            swapsTold = SwapsLeftInPlay;
        }

        // The stick in the table's axes, as seen from above.
        private Vector3 StickOnTable(int hider)
        {
            Vector2 stick = moveActions[hider].ReadValue<Vector2>();
            Transform table = PoolTableSurface.Instance.transform;
            Vector3 wish = Flat(table.right) * stick.x + Flat(table.forward) * stick.y;
            return Vector3.ClampMagnitude(wish, 1f);
        }

        private void UpdateHidden(int hider)
        {
            HideAndSeekBall hidden = hiddenBalls[hider];
            Vector3 stick = StickOnTable(hider);
            if (Time.time >= forcedUntil[hider]) hidden.Drive(stick);
            HideAndSeekSettings s = Settings;
            if (swapActions[hider].WasPressedThisFrame() && swapsLeft[hider] > 0)
            {
                HideAndSeekBall target = TargetFrom(hidden, stick, s.swapReach * tableScale);
                if (target == null) return;
                swapsLeft[hider]--;
                hidden.Stop();
                hiddenBalls[hider] = target;
                forcedUntil[hider] = 0f;
                ResetMoveClock(hider);
                // A second swap before the message: that one tells of both.
                if (CurrentPhase == Phase.Search && swapAlertAt <= 0f)
                    swapAlertAt = Time.time + Random.Range(s.swapAlertDelayMin, s.swapAlertDelayMax);
                StartRumble(hider);
                if (s.debugLogs) Debug.Log($"[HideAndSeek] J{hider + 1} passe dans la bille {target.Ball.Number} ({swapsLeft[hider]} changements restants).", target);
            }
            else if (fakeActions[hider].WasPressedThisFrame() && fakesLeft[hider] > 0)
            {
                HideAndSeekBall target = TargetFrom(hidden, stick, s.fakeReach * tableScale);
                if (target == null) return;
                fakesLeft[hider]--;
                // No direction given: away from the hider.
                Vector3 heading = stick.sqrMagnitude >= StickDeadZone * StickDeadZone
                    ? stick.normalized
                    : Flat(target.transform.position - hidden.transform.position).normalized;
                target.Drive(heading, s.fakeRollTime);
                StartRumble(hider);
                if (s.debugLogs) Debug.Log($"[HideAndSeek] J{hider + 1} : fausse piste, bille {target.Ball.Number} ({fakesLeft[hider]} restantes).", target);
            }
        }

        // The nearest free ball within reach in the stick's direction, or the
        // nearest of all with the stick at rest. Nothing to aim with on the
        // screen: a pointer would show the others where the hider is.
        private HideAndSeekBall TargetFrom(HideAndSeekBall hidden, Vector3 stick, float reach)
        {
            Vector3 from = hidden.transform.position;
            if (stick.sqrMagnitude < StickDeadZone * StickDeadZone) return NearestBall(from, reach);
            Vector3 direction = stick.normalized;
            HideAndSeekBall nearest = null;
            float best = reach * reach;
            foreach (HideAndSeekBall ball in tableBalls)
            {
                if (!ball.gameObject.activeInHierarchy || IsTaken(ball)) continue;
                Vector3 offset = Flat(ball.transform.position - from);
                float distance = offset.sqrMagnitude;
                if (distance >= best || distance < 1e-6f || Vector3.Dot(offset.normalized, direction) < AimCone) continue;
                best = distance;
                nearest = ball;
            }
            return nearest;
        }

        // Now and then a ball sets off towards a random point of the felt, at the
        // speeds a player would use: a ball moving proves nothing.
        private void UpdateDecoys()
        {
            if (Time.time < nextDecoy) return;
            HideAndSeekSettings s = Settings;
            nextDecoy = Time.time + Random.Range(s.decoyIntervalMin, s.decoyIntervalMax);
            HideAndSeekBall ball = tableBalls[Random.Range(0, tableBalls.Count)];
            if (IsTaken(ball) || ball.IsDriven || !ball.gameObject.activeInHierarchy) return;
            RollToRandomPoint(ball, Random.Range(s.decoyRollMin, s.decoyRollMax));
        }

        // Towards a random point of the felt, as any decoy.
        private static void RollToRandomPoint(HideAndSeekBall ball, float duration)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 center = surface.transform.position;
            Vector3 point = center
                + surface.transform.right * (Random.Range(-1f, 1f) * surface.HalfLength)
                + surface.transform.forward * (Random.Range(-1f, 1f) * surface.HalfWidth);
            Vector3 toPoint = Flat(point - ball.transform.position);
            if (toPoint.sqrMagnitude < 1e-6f) return;
            ball.Drive(toPoint.normalized * Random.Range(0.5f, 1f), duration);
        }

        // ---------- The hiders have to keep moving ----------

        private void ResetMoveClock(int hider)
        {
            if (hiddenBalls[hider] == null) return;
            moveAnchor[hider] = hiddenBalls[hider].transform.position;
            moveDeadline[hider] = Time.time + Settings.mustMoveTime;
            moveWarned[hider] = false;
        }

        // Ground covered, not the stick: pushing against a cushion or jiggling
        // on the spot doesn't count. Rolled off like a decoy, nothing about the
        // roll itself gives the ball away.
        private void UpdateMustMove(int hider)
        {
            HideAndSeekSettings s = Settings;
            if (s.mustMoveTime <= 0f || Time.time < forcedUntil[hider]) return;
            HideAndSeekBall hidden = hiddenBalls[hider];
            float distance = s.mustMoveDistance * tableScale;
            if (Flat(hidden.transform.position - moveAnchor[hider]).sqrMagnitude >= distance * distance)
            {
                ResetMoveClock(hider);
                return;
            }
            if (!moveWarned[hider] && MustMoveSoon(hider))
            {
                moveWarned[hider] = true;
                StartRumble(hider);
            }
            if (Time.time < moveDeadline[hider]) return;
            forcedUntil[hider] = Time.time + s.forcedRollTime;
            RollToRandomPoint(hidden, s.forcedRollTime);
            StartRumble(hider);
            ResetMoveClock(hider);
            if (s.debugLogs) Debug.Log($"[HideAndSeek] J{hider + 1} n'a pas bougé : la bille {hidden.Ball.Number} part toute seule.", hidden);
        }

        private bool MustMoveSoon(int hider) =>
            CurrentPhase == Phase.Search && IsHidden(hider) && Settings.mustMoveTime > 0f && Time.time >= forcedUntil[hider]
            && Time.time >= moveDeadline[hider] - Settings.mustMoveWarning;

        // Every ball gets a knock, the hiders' too: the moment to go far, and
        // to lose track of one's own ball.
        private void UpdateShake()
        {
            if (Time.time < nextShake) return;
            HideAndSeekSettings s = Settings;
            nextShake = Time.time + Random.Range(s.shakeIntervalMin, s.shakeIntervalMax);
            float speed = s.shakeSpeed * tableScale;
            foreach (HideAndSeekBall ball in tableBalls)
            {
                if (!ball.gameObject.activeInHierarchy) continue;
                Vector2 knock = Random.insideUnitCircle * speed;
                ball.Kick(new Vector3(knock.x, 0f, knock.y));
            }
            LocalPoolPowerEffectReceiver.PlayImpact(-1, s.shakeCamera, s.shakeCameraTime, Color.clear, 0f, 0f);
            if (s.debugLogs) Debug.Log("[HideAndSeek] Secousse.");
        }

        // ---------- The seekers' power ----------

        public bool CanPing(int player)
        {
            if (CurrentPhase != Phase.Search || !IsSeeker(player)) return false;
            foreach (int h in hiders) if (IsHidden(h)) return true;
            return false;
        }

        // A zone with one hider's ball (picked at random among those still
        // hidden) somewhere inside, not at its centre: it narrows the search
        // down to a few balls, it doesn't name one.
        public void Ping(float radius, float duration, Color color)
        {
            int count = 0, pick = -1;
            foreach (int h in hiders)
                if (IsHidden(h) && Random.Range(0, ++count) == 0) pick = h;
            if (pick < 0) return;
            HideAndSeekBall hidden = hiddenBalls[pick];
            ClearPing();
            float size = radius * tableScale;
            Vector2 off = Random.insideUnitCircle * (size * PingOffCentre);
            Vector3 center = hidden.transform.position + new Vector3(off.x, 0f, off.y);
            center.y = feltY + PingAboveFelt;
            pingZone = ShooterPowerFx.Shape(PrimitiveType.Cylinder, ShooterPowerFx.Glass(color, "Hide And Seek Sonar"), "Hide And Seek Sonar");
            pingZone.transform.position = center;
            pingZone.transform.localScale = new Vector3(size * 2f, PingAboveFelt * 0.5f, size * 2f);
            pingUntil = Time.time + duration;
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] Sonar autour de la bille {hidden.Ball.Number} de J{pick + 1} ({size:0.##} m, {duration:0.#} s).", hidden);
        }

        private void ClearPing()
        {
            if (pingZone != null) Object.Destroy(pingZone);
            pingZone = null;
        }

        // ---------- Rumble: tells a hider a trick worked, unseen by the others ----------

        private void StartRumble(int hider)
        {
            HideAndSeekSettings s = Settings;
            if (s.rumbleTime <= 0f || tinyBodies[hider] == null) return;
            StopRumble(hider);
            foreach (InputDevice device in tinyBodies[hider].GetComponent<PlayerInput>().devices)
            {
                if (!(device is Gamepad pad)) continue;
                pad.SetMotorSpeeds(s.rumbleStrength, s.rumbleStrength);
                rumbling[hider] = pad;
                rumbleUntil[hider] = Time.unscaledTime + s.rumbleTime;
                return;
            }
        }

        private void UpdateRumble()
        {
            for (int p = 0; p < Max; p++)
                if (rumbling[p] != null && Time.unscaledTime >= rumbleUntil[p]) StopRumble(p);
        }

        private void StopRumble(int hider)
        {
            if (rumbling[hider] != null) rumbling[hider].SetMotorSpeeds(0f, 0f);
            rumbling[hider] = null;
        }

        // ---------- Shots and pots ----------

        // PoolMatchRules.NotifyShotFired.
        public void OnShot(int player)
        {
            if (CurrentPhase != Phase.Search || !IsSeeker(player)) return;
            shotsLeft[player]--;
            lastShotTime = Time.time;
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] Tir de J{player + 1} ({shotsLeft[player]} restants).");
        }

        private int HiderOf(PoolBall ball)
        {
            foreach (int h in hiders) if (IsHidden(h) && hiddenBalls[h].Ball == ball) return h;
            return -1;
        }

        private void OnCueContact(PoolBall cueBall, PoolBall other)
        {
            if (CurrentPhase != Phase.Search) return;
            int hider = HiderOf(other);
            if (hider >= 0) Found(hider, $"la blanche de J{cueBall.CueOwner + 1} a touché la bille {other.Number}");
        }

        // Called as a ball goes in, before OnPocketed parks it. A cue ball comes
        // back through PoolMatchRules (Speed Pool's respawn delay).
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (ball.IsCueBall) return;
            toRespot.Add(ball);
            if (CurrentPhase != Phase.Search) return;
            int hider = HiderOf(ball);
            if (hider >= 0) Found(hider, $"la bille {ball.Number} est tombée");
        }

        // Out of the round, watching from above; the last one found ends it.
        private void Found(int hider, string why)
        {
            foundAt[hider] = phaseTime;
            hiddenBalls[hider].Stop();
            StopRumble(hider);
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] J{hider + 1} trouvé : {why} ({phaseTime:0.0} s).");
            foreach (int h in hiders) if (foundAt[h] < 0f) return;
            EndRound(true, "tous trouvés");
        }

        private void EndRound(bool allFound, string why)
        {
            if (CurrentPhase == Phase.RoundOver) return;
            HideAndSeekSettings s = Settings;
            seekersWon = allFound;
            CurrentPhase = Phase.RoundOver;
            foreach (int h in hiders) totalHidden[h] += foundAt[h] >= 0f ? foundAt[h] : phaseTime;
            phaseTime = 0f;
            ClearPing();
            foreach (HideAndSeekBall ball in tableBalls) ball.Stop();

            if (players <= 2)
            {
                // The point to the seeker who found, or to the hider.
                roundWinner = allFound ? seekers[0] : hiders[0];
                points[roundWinner]++;
            }
            else if (allFound)
            {
                foreach (int p in seekers) points[p] += s.seekerWinPoints;
            }
            else
            {
                // Never found first (as long as the round), then the later found.
                foreach (int h in hiders)
                {
                    int rank = 0;
                    foreach (int other in hiders) if (HiddenFor(other) > HiddenFor(h) + 0.01f) rank++;
                    points[h] += RankPoints(rank);
                }
            }
            if (s.debugLogs) Debug.Log($"[HideAndSeek] Fin de la manche {round + 1} : {why} → {DescribePoints()}.");
        }

        private float HiddenFor(int hider) => foundAt[hider] >= 0f ? foundAt[hider] : float.MaxValue;

        private static int RankPoints(int rank)
        {
            int[] table = Settings.hiderRankPoints;
            if (table == null || table.Length == 0) return 0;
            return table[Mathf.Min(rank, table.Length - 1)];
        }

        private string DescribePoints()
        {
            string text = "";
            for (int p = 0; p < players; p++) text += (p > 0 ? ", " : "") + $"J{p + 1} {points[p]}";
            return text;
        }

        // A side's points: the player's, or the team's (no delegate: the
        // plate asks every frame).
        private int PointsOfSide(int side)
        {
            if (!teams) return side >= 0 && side < Max ? points[side] : 0;
            int total = 0;
            for (int p = 0; p < players; p++) if (PlayerBallGroups.SideOf(p) == side) total += points[p];
            return total;
        }

        private float HiddenOfSide(int side)
        {
            if (!teams) return totalHidden[side];
            float total = 0f;
            for (int p = 0; p < players; p++) if (PlayerBallGroups.SideOf(p) == side) total += totalHidden[p];
            return total;
        }

        private int SideOf(int player) => teams ? PlayerBallGroups.SideOf(player) : player;

        // Most points; level, the longer hidden over the match, then a draw.
        // In 2 v 2 the side is J1's or J2's team: J1 or J2 wins it.
        private int MatchWinner()
        {
            int sides = teams ? 2 : players;
            var level = new List<int>();
            int best = int.MinValue;
            for (int side = 0; side < sides; side++) best = Mathf.Max(best, PointsOfSide(side));
            for (int side = 0; side < sides; side++) if (PointsOfSide(side) == best) level.Add(side);
            string why = "le plus de points";
            if (level.Count > 1)
            {
                float longest = 0f;
                foreach (int side in level) longest = Mathf.Max(longest, HiddenOfSide(side));
                level.RemoveAll(side => HiddenOfSide(side) < longest - 0.05f);
                why = level.Count == 1 ? $"caché le plus longtemps ({longest:0.0} s)" : "égalité parfaite, tiré au sort";
            }
            int winner = level[Random.Range(0, level.Count)];
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] Victoire de {(teams ? $"l'équipe de J{winner + 1}" : $"J{winner + 1}")} : {why} ({DescribePoints()}).");
            return winner;
        }

        // ---------- HUD ----------

        public string RoleLabel(int player)
        {
            HideAndSeekSettings s = Settings;
            if (CurrentPhase == Phase.RoundOver)
            {
                bool found = IsSeeker(player) ? seekersWon : player >= 0 && player < Max && foundAt[player] >= 0f;
                return found ? s.foundText : s.notFoundText;
            }
            if (IsSeeker(player))
                return CurrentPhase == Phase.Hiding ? s.closeEyesLabel : SwapAlertUp ? s.swapAlertLabel : s.seekerLabel;
            if (player < 0 || player >= Max) return s.hiderLabel;
            if (foundAt[player] >= 0f) return s.foundText;
            if (MustMoveSoon(player)) return s.mustMoveLabel;
            return hiddenBalls[player] == null ? s.hideLabel : s.hiderLabel;
        }

        private bool SwapAlertUp => Time.time < swapAlertUntil;
        private int HideLeft => Mathf.CeilToInt(Mathf.Max(0f, Settings.hideTime - phaseTime));
        private int SearchLeft => Mathf.CeilToInt(Mathf.Max(0f, Settings.searchTime - phaseTime));

        // The player's points (their team's) against the best of the others.
        private int BestOtherPoints(int player)
        {
            int own = SideOf(player), best = 0;
            for (int side = 0; side < (teams ? 2 : players); side++)
                if (side != own) best = Mathf.Max(best, PointsOfSide(side));
            return best;
        }

        public string DescribeRound(int player)
        {
            HideAndSeekSettings s = Settings;
            if (player < 0 || player >= Max) return "";
            string score = string.Format(s.scoreFormat, PointsOfSide(SideOf(player)), BestOtherPoints(player));
            string state;
            switch (CurrentPhase)
            {
                case Phase.Hiding:
                    state = string.Format(IsSeeker(player) ? s.seekerWaitFormat : hiddenBalls[player] == null ? s.hideFormat : s.hiddenFormat, HideLeft);
                    break;
                case Phase.Search:
                    string clock = $"{SearchLeft / 60}:{SearchLeft % 60:00}";
                    state = IsSeeker(player) ? string.Format(s.searchFormat, clock, shotsLeft[player], swapsTold)
                        : foundAt[player] >= 0f ? clock
                        : string.Format(s.hiderFormat, clock, swapsLeft[player], fakesLeft[player]);
                    break;
                default:
                    return score;
            }
            return $"{score}  ·  {state}";
        }

        // Changes whenever the texts above would.
        public int PlateKey(int player)
        {
            if (player < 0 || player >= Max) return 0;
            bool seeker = IsSeeker(player);
            int time = CurrentPhase == Phase.Hiding ? HideLeft : CurrentPhase == Phase.Search ? SearchLeft : 0;
            int counts = Mathf.Min(shotsLeft[player], 7) * 64 + Mathf.Min(seeker ? swapsTold : swapsLeft[player], 7) * 8 + Mathf.Min(fakesLeft[player], 7);
            int state = (int)CurrentPhase * 8 + (hiddenBalls[player] != null ? 4 : 0) + (foundAt[player] >= 0f || (seeker && seekersWon) ? 2 : 0)
                + ((seeker ? SwapAlertUp : MustMoveSoon(player)) ? 1 : 0);
            int score = Mathf.Min(PointsOfSide(SideOf(player)), 31) * 32 + Mathf.Min(BestOtherPoints(player), 31);
            return unchecked(((score * 32 + state) * 512 + counts) * 1024 + time * 2 + (seeker ? 1 : 0));
        }

        public override string DescribePlayer(int player) => $"{RoleLabel(player)} · {DescribeRound(player)}";
    }
}
