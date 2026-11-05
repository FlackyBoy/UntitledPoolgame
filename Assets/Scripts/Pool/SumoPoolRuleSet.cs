using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // Sumo Pool: each player is a ball on a small square table with six
    // pockets, pushing the other in. A ball that falls is a point for the other
    // player, however it fell; both go back to their start for the next round,
    // first to Points To Win.
    //
    // Derived from SpeedPoolRuleSet for a cue ball each and because
    // PoolMatchRules sends the pots to its HandlePocketed. The other balls are
    // put away. The real table stays under the new one for its felt, but hidden
    // and with its pockets off (SumoPoolTable). Speed Pool only (no turns), no
    // powers (PoolMatchSetup). PoolMatchRules calls Tick every frame.
    public class SumoPoolRuleSet : SpeedPoolRuleSet
    {
        public enum Phase { Countdown, Play, RoundEnd, Over }

        // The match under way, null in other modes.
        public static SumoPoolRuleSet Current { get; private set; }

        private static SumoPoolSettings cfg;
        public static SumoPoolSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<SumoPoolSettings>("SumoPoolSettings");

        protected readonly SumoPoolBall[] balls = new SumoPoolBall[2];
        private readonly int[] points = new int[2];
        private readonly PlayerInput[] waitedFor = new PlayerInput[2];
        private readonly int[] readyFrame = new int[2];
        // What's put away for the match, to bring back.
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private readonly List<Cue> hiddenCues = new List<Cue>();
        private SumoPoolTable table;
        private float phaseTime;
        private int winner;
        private bool ready;

        public Phase CurrentPhase { get; private set; }
        // The HUD uses the score plate below rather than one of its own.
        public virtual bool ShowsPlate => true;
        // What a mode built on this one changes (SumoBilliardRuleSet).
        protected virtual bool KeepsTableBall(PoolBall ball) => false;
        protected virtual void PlaceTableBalls(Vector3 center, Quaternion rotation, float scale) { }
        protected virtual void UpdateRules(PoolMatchRules match) { }

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

            // Two players or nothing, as in Pocket Size.
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

            Vector3 center = table.transform.position;
            Vector3 along = surface.transform.rotation * Vector3.right;
            float offset = table.HalfSide * 0.5f;
            balls[0] = PrepareBall(first, 0, center - along * offset, s.player1Color, center, surface.TableScale);
            balls[1] = PrepareBall(second, 1, center + along * offset, s.player2Color, center, surface.TableScale);
            balls[0].Opponent = balls[1];
            balls[1].Opponent = balls[0];
            PlaceTableBalls(center, surface.transform.rotation, surface.TableScale);

            CurrentPhase = Phase.Countdown;
            ready = true;
            for (int p = 0; p < 2; p++) TryBind(p);
        }

        private static SumoPoolBall PrepareBall(PoolBall ball, int player, Vector3 onFelt, Color color, Vector3 center, float scale)
        {
            SumoPoolSettings s = Settings;
            ball.SetSize(s.ballScale, 0f);
            // Its slowing down is all SumoPoolBall's: this one would add a constant
            // on top, down to a sudden stop.
            ball.RollingFrictionOverride = 0f;
            ball.MakePlayerCueBall(player, onFelt + Vector3.up * ball.Radius, color);
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
            foreach (SumoPoolBall sumo in balls)
            {
                if (sumo == null) continue;
                sumo.Release();
                PoolBall ball = sumo.Ball;
                Object.Destroy(sumo);
                ball.RollingFrictionOverride = -1f;
                ball.ResetSize();
                ball.ResetToSpawn();
            }
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

        public void Tick(PoolMatchRules match)
        {
            if (!ready || match.GameOver) return;
            SumoPoolSettings s = Settings;
            phaseTime += Time.deltaTime;
            for (int p = 0; p < 2; p++) TryBind(p);
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
                    if (s.debugLogs) Debug.Log($"[SumoPool] Victoire de J{winner + 1} ({points[0]}-{points[1]}).");
                    match.Win(winner);
                    break;
            }
        }

        private void Enter(Phase phase)
        {
            CurrentPhase = phase;
            phaseTime = 0f;
        }

        protected void SetControllable(bool controllable)
        {
            balls[0].Controllable = controllable;
            balls[1].Controllable = controllable;
        }

        private void StartRound()
        {
            SetControllable(false);
            balls[0].BackToStart();
            balls[1].BackToStart();
            Enter(Phase.Countdown);
        }

        // Called as a ball goes in, before OnPocketed parks it: its fall is
        // shown from where it really was. Fallen off the table (no pocket), it
        // just stays parked. A second ball falling just after the first still
        // falls, but scores nothing.
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            int p = ball.CueOwner;
            if (p < 0 || p > 1) return;
            if (ball.LastPocket != null) ball.FallIntoTableHole(ball.LastPocket.transform.position, Settings.sinkTime);
            if (CurrentPhase != Phase.Play) return;

            points[1 - p]++;
            if (Settings.debugLogs) Debug.Log($"[SumoPool] J{p + 1} tombe : {points[0]}-{points[1]}.", ball);
            if (points[1 - p] < Settings.pointsToWin)
            {
                Enter(Phase.RoundEnd);
                return;
            }
            winner = 1 - p;
            SetControllable(false);
            Enter(Phase.Over);
        }

        // ---------- HUD ----------

        public virtual string DescribeScore(int player) =>
            string.Format(Settings.scoreFormat, points[player], points[1 - player]);

        public virtual string DescribeRound() => CurrentPhase == Phase.Countdown
            ? string.Format(Settings.countdownFormat, CountdownLeft)
            : string.Format(Settings.goalFormat, Settings.pointsToWin);

        // Changes whenever the texts above would.
        public int PlateKey(int player)
        {
            int countdown = CurrentPhase == Phase.Countdown ? CountdownLeft : 0;
            return ((points[player] * 32 + points[1 - player]) * 16 + countdown) * 2 + (CurrentPhase == Phase.Countdown ? 1 : 0);
        }

        public override string DescribePlayer(int player) => $"{DescribeScore(player)} · {DescribeRound()}";
    }
}
