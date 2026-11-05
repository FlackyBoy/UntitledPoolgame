using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Hide and seek: while the seeker keeps their eyes shut (nothing enforces it,
    // the player next to them is trusted), the hider, shrunk on the felt, takes
    // the place of a ball. They then roll it from a view from above with no mark
    // on it, among decoys that roll the same way, and the table shakes now and
    // then. The seeker has a few shots and a clock: the cue ball's first contact
    // with the hider's ball finds it, and a fall into a pocket gives it away.
    // A swap of ball reaches the seeker's plate after a random delay, or the
    // message would show which ball had just set off.
    // The roles swap every round, first to Points To Win.
    //
    // Derived from SpeedPoolRuleSet for a cue ball owned by a player
    // (PoolBall.CueOwner, which LocalPoolAimController aims), with its own table
    // as in Pocket Size. Speed Pool only, no powers (PoolMatchSetup).
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

        // The match under way, null in other modes.
        public static HideAndSeekRuleSet Current { get; private set; }

        private static HideAndSeekSettings cfg;
        public static HideAndSeekSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<HideAndSeekSettings>("HideAndSeekSettings");

        public Phase CurrentPhase { get; private set; }
        public int Seeker => round % 2 == 0 ? Settings.firstSeeker : 1 - Settings.firstSeeker;
        public int Hider => 1 - Seeker;

        private int round;
        private float phaseTime;
        private readonly int[] points = new int[2];
        private int shotsLeft, swapsLeft, fakesLeft;
        private float lastShotTime;
        private int roundWinner = -1;

        private PoolBall cue, spareCue;
        private float tableScale = 1f;
        private readonly List<HideAndSeekBall> tableBalls = new List<HideAndSeekBall>();
        private readonly List<Vector3> placed = new List<Vector3>();
        // Off the table: OnPocketed turns it off just after HandlePocketed.
        private readonly List<PoolBall> toRespot = new List<PoolBall>();

        // The hider: their small body, once placed, then their ball.
        private LocalPlayerShrink tinyBody;
        private PlayerInput waitedFor;
        private int readyFrame;
        private Vector3 tinySpawn;
        private float feltY;
        private HideAndSeekBall hiddenBall;
        private InputAction moveAction, swapAction, fakeAction;
        private GameObject cameraObject;

        // What the seeker knows of the swaps: when the next message comes, 0 =
        // none on its way, when the one up goes, and the swaps left as last told.
        private float swapAlertAt, swapAlertUntil;
        private int swapsTold;

        private float nextDecoy, nextShake;
        private Gamepad rumbling;
        private float rumbleUntil;

        public bool CanShoot(int player) => CurrentPhase == Phase.Search && player == Seeker && shotsLeft > 0;

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
            if (!FindPlayerCueBalls(out cue, out spareCue)) return;
            spareCue.gameObject.SetActive(false);
            Current = this;
            tableScale = surface.TableScale;
            feltY = surface.FeltTopY;
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
            if (rules == null) return;
            Current = null;
            PoolBall.CueBallFirstContact -= rules.OnCueContact;
            rules.ReleaseHider();
            foreach (HideAndSeekBall ball in rules.tableBalls)
                if (ball != null) Object.Destroy(ball);
            rules.tableBalls.Clear();
            if (rules.spareCue != null)
            {
                // Inactive first: the next mode's Setup, this same frame, would
                // still find it.
                rules.spareCue.gameObject.SetActive(false);
                Object.Destroy(rules.spareCue.gameObject);
            }
            if (rules.cue != null) rules.cue.MakeSharedCueBall();
        }

        // The hider's camera gone, everyone back to full size.
        private void ReleaseHider()
        {
            StopRumble();
            if (cameraObject != null) Object.Destroy(cameraObject);
            cameraObject = null;
            hiddenBall = null;
            tinyBody = null;
            waitedFor = null;
            LocalPlayerShrink.RestoreAll();
        }

        private void StartRound(int index)
        {
            HideAndSeekSettings s = Settings;
            ReleaseHider();
            round = index;
            roundWinner = -1;
            shotsLeft = s.shotsPerRound;
            swapsLeft = s.swapsPerRound;
            fakesLeft = s.fakesPerRound;
            swapsTold = swapsLeft;
            swapAlertAt = swapAlertUntil = 0f;
            toRespot.Clear();

            cue.MakePlayerCueBall(Seeker, cue.SpawnPosition, Color.white);

            // The small one starts on the foot spot, across the table from the cue ball.
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 center = surface.transform.position;
            Vector3 along = surface.transform.rotation * Vector3.right;
            if (Vector3.Dot(cue.SpawnPosition - center, along) > 0f) along = -along;
            tinySpawn = center + along * (surface.HalfLength * 0.5f);
            tinySpawn.y = feltY + 0.01f;

            ScatterBalls(surface);
            CurrentPhase = Phase.Hiding;
            phaseTime = 0f;
            if (s.debugLogs) Debug.Log($"[HideAndSeek] Manche {round + 1} : J{Hider + 1} se cache, J{Seeker + 1} cherche ({shotsLeft} tirs).");
        }

        // Balls On Table balls at random spots, clear of the pockets, the cue
        // ball and the small one's start; the rest put away for the round. The
        // balls keep their rack spot as their spawn: "Rejouer" and the next mode
        // find them there.
        private void ScatterBalls(PoolTableSurface surface)
        {
            HideAndSeekSettings s = Settings;
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
            if (Flat(p - tinySpawn).sqrMagnitude < clearance * clearance) return false;
            if (Flat(p - cue.SpawnPosition).sqrMagnitude < clearance * clearance) return false;
            float apart = radius * 3f;
            foreach (Vector3 other in placed)
                if (Flat(p - other).sqrMagnitude < apart * apart) return false;
            return true;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // Swaps the hider's body for the small one, on the felt, as soon as
        // they're there and ready. A body just there is given two frames: its
        // puppet and controllers start up first.
        private void TryPlaceTiny()
        {
            if (tinyBody != null || hiddenBall != null) return;
            PlayerInput player = PlayerInput.GetPlayerByIndex(Hider);
            if (player == null) return;
            if (player != waitedFor)
            {
                waitedFor = player;
                readyFrame = Time.frameCount + 2;
            }
            if (Time.frameCount < readyFrame) return;
            PocketSizeSettings tiny = PocketSizeRuleSet.Settings;
            Vector3 look = Flat(cue.SpawnPosition - tinySpawn);
            tinyBody = LocalPlayerShrink.Shrink(player, tiny.tinyPrefab, PlayerInputManager.instance.playerPrefab,
                tinySpawn, look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look) : Quaternion.identity,
                LayerMask.GetMask("Poolball"), tiny.tinyNearClip, tiny.tinyJumpHeight, tiny.tinyFallStiffness, tiny.tinyFallGravity, tiny.tinyFallDamper);
            BindActions(tinyBody.GetComponent<PlayerInput>());
        }

        private void BindActions(PlayerInput player)
        {
            InputActionMap map = player.actions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            swapAction = map.FindAction("Interact", throwIfNotFound: true);
            // What launches a power elsewhere (LocalPoolPowerController).
            fakeAction = map.FindAction("Next", throwIfNotFound: true);
        }

        // ---------- Every frame (PoolMatchRules.Update) ----------

        public void Tick(PoolMatchRules match)
        {
            if (match.GameOver || Current != this) return;
            HideAndSeekSettings s = Settings;
            phaseTime += Time.deltaTime;
            UpdateRumble();
            UpdateSwapAlert();
            foreach (PoolBall ball in toRespot) match.RespotBall(ball);
            toRespot.Clear();

            switch (CurrentPhase)
            {
                case Phase.Hiding:
                    TryPlaceTiny();
                    UpdateHiding();
                    if (phaseTime < s.hideTime) break;
                    // Time's up: wherever they are, they become the nearest ball.
                    if (hiddenBall == null && tinyBody != null) TakeBall(NearestBall(tinyBody.transform.position, float.PositiveInfinity));
                    // Their body wasn't there yet: nothing to look for.
                    if (hiddenBall == null) break;
                    CurrentPhase = Phase.Search;
                    phaseTime = 0f;
                    // A swap while the seeker's eyes were shut sends no message.
                    swapsTold = swapsLeft;
                    nextDecoy = Time.time + Random.Range(s.decoyIntervalMin, s.decoyIntervalMax);
                    nextShake = Time.time + Random.Range(s.shakeIntervalMin, s.shakeIntervalMax);
                    if (s.debugLogs) Debug.Log($"[HideAndSeek] Recherche ! J{Seeker + 1} peut tirer.");
                    break;

                case Phase.Search:
                    UpdateHidden();
                    UpdateDecoys();
                    UpdateShake();
                    if (phaseTime >= s.searchTime) EndRound(Hider, "le temps est écoulé");
                    // The last shot has played out, without finding the ball.
                    else if (shotsLeft <= 0 && Time.time > lastShotTime + LastShotGrace && !cue.IsRolling) EndRound(Hider, "plus de tirs");
                    break;

                case Phase.RoundOver:
                    if (phaseTime < s.roundOverTime) break;
                    if (points[roundWinner] < s.pointsToWin)
                    {
                        StartRound(round + 1);
                        break;
                    }
                    int winner = roundWinner;
                    if (s.debugLogs) Debug.Log($"[HideAndSeek] Victoire de J{winner + 1} ({points[0]}-{points[1]}).");
                    ReleaseHider();
                    match.Win(winner);
                    break;
            }
        }

        private void UpdateHiding()
        {
            if (hiddenBall != null)
            {
                UpdateHidden();
                return;
            }
            if (tinyBody == null) return;
            // A body moved off the table, or fallen in a pocket, goes back on it.
            Vector3 p = tinyBody.transform.position;
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (p.y < feltY - PocketSizeRuleSet.Settings.fallDepth || Flat(surface.ClampToPlayArea(p, 0f) - p).sqrMagnitude > 0.05f * 0.05f)
                tinyBody.Teleport(tinySpawn, tinyBody.transform.rotation);
            if (!swapAction.WasPressedThisFrame()) return;
            HideAndSeekBall nearest = NearestBall(p, Settings.takeReach * tableScale);
            if (nearest != null) TakeBall(nearest);
        }

        private HideAndSeekBall NearestBall(Vector3 from, float reach)
        {
            HideAndSeekBall nearest = null;
            float best = reach * reach;
            foreach (HideAndSeekBall ball in tableBalls)
            {
                if (ball == hiddenBall || !ball.gameObject.activeInHierarchy) continue;
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
        private void TakeBall(HideAndSeekBall ball)
        {
            if (ball == null) return;
            hiddenBall = ball;
            GameObject body = tinyBody.gameObject;
            int player = tinyBody.GetComponent<PlayerInput>().playerIndex;
            Camera bodyCamera = body.GetComponentInChildren<Camera>(true);
            float aspect = bodyCamera != null && bodyCamera.aspect > 0f ? bodyCamera.aspect : 1f;
            foreach (MonoBehaviour script in body.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && SumoPoolBall.IsGameplayScript(script)) script.enabled = false;
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            if (body.TryGetComponent(out CharacterController controller)) controller.enabled = false;
            CreateCamera(player, aspect);
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] J{player + 1} prend la place de la bille {ball.Ball.Number}.", ball);
        }

        // Straight down, the length of the table across the view, the whole
        // table in it. Through Cinemachine on the player's channel, as in Sumo
        // Pool. No mark on the ball: the other player can see this half of the
        // screen.
        private void CreateCamera(int player, float aspect)
        {
            HideAndSeekSettings s = Settings;
            PoolTableSurface surface = PoolTableSurface.Instance;
            cameraObject = new GameObject($"Hide And Seek Camera J{player + 1}");
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
            swapsTold = swapsLeft;
        }

        // The stick in the table's axes, as seen from above.
        private Vector3 StickOnTable()
        {
            Vector2 stick = moveAction.ReadValue<Vector2>();
            Transform table = PoolTableSurface.Instance.transform;
            Vector3 wish = Flat(table.right) * stick.x + Flat(table.forward) * stick.y;
            return Vector3.ClampMagnitude(wish, 1f);
        }

        private void UpdateHidden()
        {
            Vector3 stick = StickOnTable();
            hiddenBall.Drive(stick);
            HideAndSeekSettings s = Settings;
            if (swapAction.WasPressedThisFrame() && swapsLeft > 0)
            {
                HideAndSeekBall target = TargetFrom(stick, s.swapReach * tableScale);
                if (target == null) return;
                swapsLeft--;
                hiddenBall.Stop();
                hiddenBall = target;
                // A second swap before the message: that one tells of both.
                if (CurrentPhase == Phase.Search && swapAlertAt <= 0f)
                    swapAlertAt = Time.time + Random.Range(s.swapAlertDelayMin, s.swapAlertDelayMax);
                StartRumble();
                if (s.debugLogs) Debug.Log($"[HideAndSeek] J{Hider + 1} passe dans la bille {target.Ball.Number} ({swapsLeft} changements restants).", target);
            }
            else if (fakeAction.WasPressedThisFrame() && fakesLeft > 0)
            {
                HideAndSeekBall target = TargetFrom(stick, s.fakeReach * tableScale);
                if (target == null) return;
                fakesLeft--;
                // No direction given: away from the hider.
                Vector3 heading = stick.sqrMagnitude >= StickDeadZone * StickDeadZone
                    ? stick.normalized
                    : Flat(target.transform.position - hiddenBall.transform.position).normalized;
                target.Drive(heading, s.fakeRollTime);
                StartRumble();
                if (s.debugLogs) Debug.Log($"[HideAndSeek] J{Hider + 1} : fausse piste, bille {target.Ball.Number} ({fakesLeft} restantes).", target);
            }
        }

        // The nearest ball within reach in the stick's direction, or the nearest
        // of all with the stick at rest. Nothing to aim with on the screen: a
        // pointer would show the other player where the hider is.
        private HideAndSeekBall TargetFrom(Vector3 stick, float reach)
        {
            Vector3 from = hiddenBall.transform.position;
            if (stick.sqrMagnitude < StickDeadZone * StickDeadZone) return NearestBall(from, reach);
            Vector3 direction = stick.normalized;
            HideAndSeekBall nearest = null;
            float best = reach * reach;
            foreach (HideAndSeekBall ball in tableBalls)
            {
                if (ball == hiddenBall || !ball.gameObject.activeInHierarchy) continue;
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
            if (ball == hiddenBall || ball.IsDriven || !ball.gameObject.activeInHierarchy) return;
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 center = surface.transform.position;
            Vector3 point = center
                + surface.transform.right * (Random.Range(-1f, 1f) * surface.HalfLength)
                + surface.transform.forward * (Random.Range(-1f, 1f) * surface.HalfWidth);
            Vector3 toPoint = Flat(point - ball.transform.position);
            if (toPoint.sqrMagnitude < 1e-6f) return;
            ball.Drive(toPoint.normalized * Random.Range(0.5f, 1f), Random.Range(s.decoyRollMin, s.decoyRollMax));
        }

        // Every ball gets a knock, the hider's too: the moment to go far, and to
        // lose track of one's own ball.
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

        // ---------- Rumble: tells the hider a trick worked, unseen by the other ----------

        private void StartRumble()
        {
            HideAndSeekSettings s = Settings;
            if (s.rumbleTime <= 0f || tinyBody == null) return;
            StopRumble();
            foreach (InputDevice device in tinyBody.GetComponent<PlayerInput>().devices)
            {
                if (!(device is Gamepad pad)) continue;
                pad.SetMotorSpeeds(s.rumbleStrength, s.rumbleStrength);
                rumbling = pad;
                rumbleUntil = Time.unscaledTime + s.rumbleTime;
                return;
            }
        }

        private void UpdateRumble()
        {
            if (rumbling != null && Time.unscaledTime >= rumbleUntil) StopRumble();
        }

        private void StopRumble()
        {
            if (rumbling != null) rumbling.SetMotorSpeeds(0f, 0f);
            rumbling = null;
        }

        // ---------- Shots and pots ----------

        // PoolMatchRules.NotifyShotFired.
        public void OnShot()
        {
            if (CurrentPhase != Phase.Search) return;
            shotsLeft--;
            lastShotTime = Time.time;
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] Tir de J{Seeker + 1} ({shotsLeft} restants).");
        }

        private void OnCueContact(PoolBall cueBall, PoolBall other)
        {
            if (CurrentPhase != Phase.Search || hiddenBall == null || other != hiddenBall.Ball) return;
            EndRound(Seeker, $"la blanche a touché la bille {other.Number}");
        }

        // Called as a ball goes in, before OnPocketed parks it. The cue ball comes
        // back through PoolMatchRules (Speed Pool's respawn delay).
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (ball.IsCueBall) return;
            toRespot.Add(ball);
            if (CurrentPhase == Phase.Search && hiddenBall != null && ball == hiddenBall.Ball)
                EndRound(Seeker, $"la bille {ball.Number} est tombée");
        }

        private void EndRound(int winner, string why)
        {
            if (CurrentPhase == Phase.RoundOver) return;
            roundWinner = winner;
            points[winner]++;
            CurrentPhase = Phase.RoundOver;
            phaseTime = 0f;
            foreach (HideAndSeekBall ball in tableBalls) ball.Stop();
            if (Settings.debugLogs) Debug.Log($"[HideAndSeek] Fin de la manche {round + 1} : {why} → point pour J{winner + 1} ({points[0]}-{points[1]}).");
        }

        // ---------- HUD ----------

        public string RoleLabel(int player)
        {
            HideAndSeekSettings s = Settings;
            if (CurrentPhase == Phase.RoundOver) return roundWinner == Seeker ? s.foundText : s.notFoundText;
            if (player == Seeker)
                return CurrentPhase == Phase.Hiding ? s.closeEyesLabel : SwapAlertUp ? s.swapAlertLabel : s.seekerLabel;
            return hiddenBall == null ? s.hideLabel : s.hiderLabel;
        }

        private bool SwapAlertUp => Time.time < swapAlertUntil;
        private int HideLeft => Mathf.CeilToInt(Mathf.Max(0f, Settings.hideTime - phaseTime));
        private int SearchLeft => Mathf.CeilToInt(Mathf.Max(0f, Settings.searchTime - phaseTime));

        public string DescribeRound(int player)
        {
            HideAndSeekSettings s = Settings;
            string score = string.Format(s.scoreFormat, points[player], points[1 - player]);
            string state;
            switch (CurrentPhase)
            {
                case Phase.Hiding:
                    state = string.Format(player == Seeker ? s.seekerWaitFormat : hiddenBall == null ? s.hideFormat : s.hiddenFormat, HideLeft);
                    break;
                case Phase.Search:
                    string clock = $"{SearchLeft / 60}:{SearchLeft % 60:00}";
                    state = player == Seeker
                        ? string.Format(s.searchFormat, clock, shotsLeft, swapsTold)
                        : string.Format(s.hiderFormat, clock, swapsLeft, fakesLeft);
                    break;
                default:
                    return score;
            }
            return $"{score}  ·  {state}";
        }

        // Changes whenever the texts above would.
        public int PlateKey(int player)
        {
            int time = CurrentPhase == Phase.Hiding ? HideLeft : CurrentPhase == Phase.Search ? SearchLeft : 0;
            int counts = shotsLeft * 64 + (player == Seeker ? swapsTold : swapsLeft) * 8 + fakesLeft;
            int state = (int)CurrentPhase * 4 + (hiddenBall != null ? 2 : 0) + (SwapAlertUp ? 1 : 0);
            return (((points[player] * 16 + points[1 - player]) * 16 + state) * 512 + counts) * 1024 + time * 2 + (player == Seeker ? 1 : 0);
        }

        public override string DescribePlayer(int player) => $"{RoleLabel(player)} · {DescribeRound(player)}";
    }
}
