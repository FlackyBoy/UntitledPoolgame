using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // Sumo Pool: each player is a ball on a small square table with six
    // pockets, pushing the others in. Two to four players: a ball that falls is
    // out of the round, however it fell, and the last one on the table scores
    // (with two, the other player). Everyone goes back to their start for the
    // next round, first to Points To Win. A player who joins during the match
    // gets a ball and a new round starts.
    //
    // Derived from SpeedPoolRuleSet for a cue ball each and because
    // PoolMatchRules sends the pots to its HandlePocketed. The other balls are
    // put away. The real table stays under the new one for its felt, but hidden
    // and with its pockets off (SumoPoolTable). Speed Pool only (no turns), no
    // powers (PoolMatchSetup). PoolMatchRules calls Tick every frame.
    public class SumoPoolRuleSet : SpeedPoolRuleSet, IMatchScoreboard
    {
        public enum Phase { Countdown, Play, RoundEnd, Over }

        // The match under way, null in other modes.
        public static SumoPoolRuleSet Current { get; private set; }

        private static SumoPoolSettings cfg;
        public static SumoPoolSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<SumoPoolSettings>("SumoPoolSettings");

        protected readonly SumoPoolBall[] balls = new SumoPoolBall[PoolMatchRules.MaxPlayers];
        private readonly int[] points = new int[PoolMatchRules.MaxPlayers];
        private readonly bool[] fallen = new bool[PoolMatchRules.MaxPlayers];
        private readonly PlayerInput[] waitedFor = new PlayerInput[PoolMatchRules.MaxPlayers];
        private readonly int[] readyFrame = new int[PoolMatchRules.MaxPlayers];
        // Balls in play (players 0 to ballCount - 1).
        private int ballCount;
        private PoolBall cueTemplate;
        // Where the balls start, kept for a player who joins later.
        private Vector3 tableCenter, alongTable;
        private float startRadius, tableScale;
        // What's put away for the match, to bring back.
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private readonly List<Cue> hiddenCues = new List<Cue>();
        private SumoPoolTable table;
        private float phaseTime;
        private int winner;
        private int lastScorer = -1;
        private bool ready;

        public Phase CurrentPhase { get; private set; }
        // No groups: a player who joins gets a ball and a new round (AddLatePlayers).
        public override bool SharesBallsOut => false;
        // The HUD uses the score plate below rather than one of its own.
        public virtual bool ShowsPlate => true;
        // What a mode built on this one changes (SumoBilliardRuleSet).
        protected virtual bool KeepsTableBall(PoolBall ball) => false;
        protected virtual void PlaceTableBalls(Vector3 center, Quaternion rotation, float scale) { }
        protected virtual void UpdateRules(PoolMatchRules match) { }
        // The table balls' rolling friction (m/s², RollingFrictionOverride),
        // which a player's ball gets too with Pool Ball Physics. -1 = the
        // normal table's.
        protected virtual float TableBallFriction(float scale) => -1f;

        private int CountdownLeft => Mathf.CeilToInt(Mathf.Max(0f, Settings.countdown - phaseTime));

        public override void Setup(PoolMatchRules match)
        {
            // "Revanche" comes straight back here, with the same mode.
            Cleanup();
            cfg = PoolSettingsLoader.LoadOrDefault<SumoPoolSettings>("SumoPoolSettings");
            PoolTableSurface surface = PoolTableSurface.Instance;
            // Before the balls change: the second cue ball is a clone of the first.
            if (surface == null || !FindPlayerCueBalls(out PoolBall first, out PoolBall second))
            {
                Debug.LogError("[SumoPool] Il manque la table ou sa blanche : pas de partie.");
                return;
            }
            Current = this;

            // Two players at least, as in Pocket Size.
            if (PlayerInput.all.Count < 2)
                Debug.Log($"[SumoPool] Un seul joueur : écran partagé. {Core.SplitScreenCheatSpawner.JoinPlayer()}");

            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
                if (!ball.IsCueBall && !KeepsTableBall(ball)) ball.gameObject.SetActive(false);

            SumoPoolSettings s = Settings;
            // Before the new pockets exist, this is for the real table's.
            PoolPocket.SetAllCatching(false);
            Transform tableRoot = TableRootOf(surface);
            HideTable(tableRoot);
            HideCues();
            table = SumoPoolTable.Build(surface, tableRoot, s, first.Radius * s.ballScale);

            tableCenter = table.transform.position;
            alongTable = surface.transform.rotation * Vector3.right;
            startRadius = table.HalfSide * 0.5f;
            tableScale = surface.TableScale;
            // Copied before the first ball is changed (size, colour, its
            // SumoPoolBall): what players 3 and 4 are cloned from.
            cueTemplate = Object.Instantiate(first, first.transform.parent);
            cueTemplate.name = "CueBall_Template (Sumo Pool)";
            cueTemplate.gameObject.SetActive(false);
            balls[0] = PrepareBall(first, 0, tableCenter, tableScale);
            balls[1] = PrepareBall(second, 1, tableCenter, tableScale);
            ballCount = 2;
            for (int p = 2; p < match.PlayerCount; p++) AddBall(p);
            PlaceStarts();
            PlaceTableBalls(tableCenter, surface.transform.rotation, tableScale);

            CurrentPhase = Phase.Countdown;
            ready = true;
            for (int p = 0; p < ballCount; p++) TryBind(p);
        }

        // Players 3 and 4: a clone of the table's cue ball, gone with the
        // match (TearDown).
        private void AddBall(int player)
        {
            PoolBall clone = Object.Instantiate(cueTemplate, cueTemplate.transform.parent);
            clone.gameObject.SetActive(true);
            clone.name = $"CueBall_P{player + 1} (Sumo Pool)";
            balls[player] = PrepareBall(clone, player, tableCenter, tableScale);
            ballCount = player + 1;
        }

        // Evenly round the middle, J1 on one side: with two, J1 and J2 face
        // each other across the table, as before.
        private void PlaceStarts()
        {
            for (int p = 0; p < ballCount; p++)
            {
                Vector3 dir = Quaternion.AngleAxis(360f * p / ballCount, Vector3.up) * -alongTable;
                PoolBall ball = balls[p].Ball;
                ball.SetSpawn(tableCenter + dir * startRadius + Vector3.up * ball.Radius);
                balls[p].BackToStart();
            }
        }

        // Its start is set by PlaceStarts.
        private SumoPoolBall PrepareBall(PoolBall ball, int player, Vector3 center, float scale)
        {
            SumoPoolSettings s = Settings;
            ball.SetSize(s.ballScale, 0f);
            // Pool physics: the felt slows it as it does the table's balls.
            // Arcade: its slowing down is all SumoPoolBall's, this one would add
            // a constant on top, down to a sudden stop.
            ball.RollingFrictionOverride = s.poolBallPhysics ? TableBallFriction(scale) : 0f;
            ball.MakePlayerCueBall(player, center + Vector3.up * ball.Radius, s.PlayerColor(player));
            SumoPoolBall sumo = ball.gameObject.AddComponent<SumoPoolBall>();
            sumo.Prepare(ball, s, scale, center);
            return sumo;
        }

        // The model around PoolPhysics, or the placeholder table on its own
        // (Tools > Pool > Build Table): not the top of the hierarchy, a level
        // may keep its table under something else.
        private static Transform TableRootOf(PoolTableSurface surface)
        {
            Transform physics = surface.transform.parent;
            if (physics == null) return surface.transform;
            return physics.name == "PoolPhysics" && physics.parent != null ? physics.parent : physics;
        }

        // The balls are under the table too.
        private void HideTable(Transform root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r.GetComponentInParent<PoolBall>(true) != null) continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }

        // Lying around where the table was. Called again as a body is taken: it
        // only lets go of its cue then. Nobody needs one, and "in play" means
        // active for a cue.
        private void HideCues()
        {
            foreach (Cue cue in Cue.All)
            {
                if (cue == null || !cue.IsFree) continue;
                cue.gameObject.SetActive(false);
                hiddenCues.Add(cue);
            }
        }

        // Another mode comes next, or a new match of this one.
        public static new void Cleanup()
        {
            SumoPoolRuleSet rules = Current;
            if (rules == null) return;
            Current = null;
            rules.TearDown();
        }

        protected virtual void TearDown()
        {
            for (int p = 0; p < balls.Length; p++)
            {
                SumoPoolBall sumo = balls[p];
                if (sumo == null) continue;
                balls[p] = null;
                sumo.Release();
                PoolBall ball = sumo.Ball;
                Object.Destroy(sumo);
                // The clones of players 3 and 4 (inactive first: the next
                // mode's Setup, this same frame, would still find them).
                if (p >= 2)
                {
                    ball.gameObject.SetActive(false);
                    Object.Destroy(ball.gameObject);
                    continue;
                }
                ball.RollingFrictionOverride = -1f;
                ball.ResetSize();
                ball.ResetToSpawn();
            }
            ballCount = 0;
            if (cueTemplate != null) Object.Destroy(cueTemplate.gameObject);
            cueTemplate = null;
            if (table != null)
            {
                // Inactive first: the next mode's Setup, this same frame, would
                // still find its pockets.
                table.gameObject.SetActive(false);
                Object.Destroy(table.gameObject);
            }
            PoolPocket.SetAllCatching(true);
            foreach (Renderer r in hiddenRenderers)
                if (r != null) r.enabled = true;
            foreach (Cue cue in hiddenCues)
                if (cue != null) cue.gameObject.SetActive(true);
            hiddenRenderers.Clear();
            hiddenCues.Clear();
        }

        // Gives a player their ball as soon as they're there and ready. A body
        // just there is given two frames: its puppet and controllers start up
        // first (it may have just come back from another mode's small one).
        private void TryBind(int p)
        {
            if (balls[p] == null || balls[p].IsBound) return;
            PlayerInput player = PlayerInput.GetPlayerByIndex(p);
            if (player == null) return;
            if (player != waitedFor[p])
            {
                waitedFor[p] = player;
                readyFrame[p] = Time.frameCount + 2;
            }
            if (Time.frameCount < readyFrame[p]) return;
            balls[p].Bind(player);
            HideCues();
            if (Settings.debugLogs) Debug.Log($"[SumoPool] J{p + 1} prend sa bille.", player);
        }

        // ---------- Every frame (PoolMatchRules.Update) ----------

        // The players are the balls: no cue to strike with.
        public override bool AllowsShot(PoolMatchRules match, int playerIndex) => false;

        // The cue balls come back with the next round.
        public override bool RespawnsPottedCueBall => false;

        public override void Tick(PoolMatchRules match)
        {
            if (!ready || match.GameOver) return;
            SumoPoolSettings s = Settings;
            phaseTime += Time.deltaTime;
            if (match.PlayerCount > ballCount && CurrentPhase != Phase.Over) AddLatePlayers(match);
            for (int p = 0; p < ballCount; p++) TryBind(p);
            UpdateRules(match);

            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    if (phaseTime < s.countdown) break;
                    Enter(Phase.Play);
                    SetControllable(true);
                    break;

                case Phase.RoundEnd:
                    if (phaseTime < s.roundEndDelay) break;
                    StartRound();
                    break;

                case Phase.Over:
                    if (phaseTime < s.endDelay) break;
                    if (s.debugLogs) Debug.Log($"[SumoPool] Victoire de J{winner + 1} ({DescribePoints()}).");
                    match.Win(winner);
                    break;
            }
        }

        private void Enter(Phase phase)
        {
            CurrentPhase = phase;
            phaseTime = 0f;
        }

        // Someone joined (C+P, a free gamepad): their ball, everyone back to a
        // start spread for the new count, and a new round from the countdown.
        private void AddLatePlayers(PoolMatchRules match)
        {
            for (int p = ballCount; p < match.PlayerCount; p++) AddBall(p);
            if (Settings.debugLogs) Debug.Log($"[SumoPool] {ballCount} joueurs : nouvelle manche.");
            StartRound();
        }

        protected void SetControllable(bool controllable)
        {
            for (int p = 0; p < ballCount; p++) balls[p].Controllable = controllable;
        }

        private void StartRound()
        {
            SetControllable(false);
            System.Array.Clear(fallen, 0, fallen.Length);
            PlaceStarts();
            Enter(Phase.Countdown);
        }

        // Called as a ball goes in, before OnPocketed parks it: its fall is
        // shown from where it really was. Fallen off the table (no pocket), it
        // just stays parked. A second ball falling just after the first still
        // falls, but scores nothing.
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            int p = ball.CueOwner;
            if (p < 0 || p >= ballCount) return;
            if (ball.LastPocket != null) ball.FallIntoTableHole(ball.LastPocket.transform.position, Settings.sinkTime);
            if (CurrentPhase != Phase.Play || fallen[p]) return;
            fallen[p] = true;

            // Out of the round, which goes on while two or more are left.
            int standing = -1, left = 0;
            for (int q = 0; q < ballCount; q++)
                if (!fallen[q]) { left++; standing = q; }
            if (left > 1)
            {
                if (Settings.debugLogs) Debug.Log($"[SumoPool] J{p + 1} tombe : encore {left} sur la table.", ball);
                return;
            }
            if (standing < 0) return;

            points[standing]++;
            lastScorer = standing;
            if (Settings.debugLogs) Debug.Log($"[SumoPool] J{p + 1} tombe : point pour J{standing + 1} ({DescribePoints()}).", ball);
            if (points[standing] < Settings.pointsToWin)
            {
                Enter(Phase.RoundEnd);
                return;
            }
            winner = standing;
            SetControllable(false);
            Enter(Phase.Over);
        }

        // "2-1" with two players, "2-1-0-1" with four (console).
        private string DescribePoints()
        {
            string text = points[0].ToString();
            for (int q = 1; q < ballCount; q++) text += "-" + points[q];
            return text;
        }

        // The best score among the others: with two, the other player's.
        private int BestOtherPoints(int player)
        {
            int best = 0;
            for (int q = 0; q < ballCount; q++)
                if (q != player) best = Mathf.Max(best, points[q]);
            return best;
        }

        // ---------- HUD ----------

        // The free quarter (IMatchScoreboard).
        public int ScoreboardPlayers => ballCount;
        public int Points(int player) => player >= 0 && player < ballCount ? points[player] : 0;
        public int PointsToWin => Settings.pointsToWin;
        public bool IsOut(int player) => player >= 0 && player < ballCount && fallen[player];
        public int LastScorer => lastScorer;
        int IMatchScoreboard.CountdownLeft => CountdownLeft;
        public ScoreboardMoment Moment => CurrentPhase switch
        {
            Phase.Countdown => ScoreboardMoment.Countdown,
            Phase.RoundEnd => ScoreboardMoment.RoundEnd,
            Phase.Over => ScoreboardMoment.Over,
            _ => ScoreboardMoment.Play,
        };

        // The small square table, not the real one under it.
        public override bool GetOverview(out Vector3 center, out Vector2 halfSize, out Quaternion rotation)
        {
            if (table == null) return base.GetOverview(out center, out halfSize, out rotation);
            center = tableCenter;
            halfSize = new Vector2(table.HalfSide, table.HalfSide);
            rotation = Quaternion.LookRotation(Vector3.Cross(alongTable, Vector3.up), Vector3.up);
            return true;
        }

        public virtual string DescribeScore(int player) =>
            string.Format(Settings.scoreFormat, points[player], BestOtherPoints(player));

        public virtual string DescribeRound() => CurrentPhase == Phase.Countdown
            ? string.Format(Settings.countdownFormat, CountdownLeft)
            : string.Format(Settings.goalFormat, Settings.pointsToWin);

        // Changes whenever the texts above would.
        public int PlateKey(int player)
        {
            int countdown = CurrentPhase == Phase.Countdown ? CountdownLeft : 0;
            return ((points[player] * 32 + BestOtherPoints(player)) * 16 + countdown) * 2 + (CurrentPhase == Phase.Countdown ? 1 : 0);
        }

        public override string DescribePlayer(int player) => $"{DescribeScore(player)} · {DescribeRound()}";
    }
}
