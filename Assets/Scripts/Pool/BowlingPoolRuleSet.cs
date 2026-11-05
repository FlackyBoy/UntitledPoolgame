using System.Collections.Generic;
using UnityEngine;
using UntitledPoolGame.Core;

namespace UntitledPoolGame.Pool
{
    // Bowling Pool: ten real pins racked at the far end of the table, the cue
    // ball thrown from the head spot. Each player plays a whole frame in turn,
    // scored as in bowling (BowlingScoreSheet). The rails and pockets are still
    // there: a pin in a pocket is down, a bank shot is fair, a cue ball in a
    // pocket simply comes back. A tie is settled by one throw each, until
    // someone wins.
    //
    // Turns only, no powers (PoolMatchSetup). PoolMatchRules waits for the pins
    // too (PinsMoving), then calls ResolveShot; the pins stay as they fell for
    // Sweep Delay before Tick clears them and passes the turn. Nothing is
    // shootable between a throw and that moment (CanShoot).
    public class BowlingPoolRuleSet : IPoolRuleSet
    {
        private enum Phase { Ready, Rolling, Sweeping }

        private const int Rows = 4;
        private const int RenderSides = 16;
        // A convex collider: the neck is filled in, a few sides are enough.
        private const int ColliderSides = 8;

        // The match under way, null in other modes (the HUD looks it up).
        public static BowlingPoolRuleSet Current { get; private set; }

        private static BowlingPoolSettings cfg;
        public static BowlingPoolSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<BowlingPoolSettings>("BowlingPoolSettings");

        // Everything made at runtime for the pins, destroyed by Cleanup.
        private static readonly List<Object> created = new List<Object>();

        private readonly BowlingScoreSheet[] sheets = new BowlingScoreSheet[2];
        private readonly List<BowlingPin> pins = new List<BowlingPin>();
        private PoolBall cueBall;
        private Phase phase;
        private float phaseStart;
        private BowlingRoll lastRoll;
        private int pinsStanding;
        private float feltY;
        private float pinRadius;
        private float cosTiltToFall;

        private bool rollOff;
        private int rollOffRound;
        private readonly int[] rollOffPins = new int[2];

        // Between throws only: the pins are still falling otherwise.
        public bool CanShoot => phase == Phase.Ready;

        public void Setup(PoolMatchRules match)
        {
            Cleanup();
            cfg = PoolSettingsLoader.LoadOrDefault<BowlingPoolSettings>("BowlingPoolSettings");
            Current = this;
            sheets[0] = new BowlingScoreSheet(cfg.frames);
            sheets[1] = new BowlingScoreSheet(cfg.frames);
            cosTiltToFall = Mathf.Cos(cfg.tiltToFall * Mathf.Deg2Rad);

            // Copy: deactivating a ball removes it from Active.
            foreach (PoolBall ball in new List<PoolBall>(PoolBall.Active))
                if (!ball.IsCueBall) ball.gameObject.SetActive(false);

            cueBall = PoolBall.FindCueBall();
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (cueBall == null || surface == null)
            {
                Debug.LogError("[Bowling] Il manque la blanche ou la table (PoolTableSurface) : pas de quilles.");
                return;
            }
            feltY = surface.FeltTopY;
            BuildPins(surface);
            RackPins();
            if (cfg.debugLogs) Debug.Log($"[Bowling] {cfg.frames} frames, {pins.Count} quilles, la première à {pins[0].RackSpot}, blanche à {cueBall.RespawnPosition}.");
        }

        // Another mode comes next, or a new match: no pin left on the table.
        public static void Cleanup()
        {
            Current = null;
            foreach (Object made in created)
                if (made != null) Object.Destroy(made);
            created.Clear();
        }

        private void BuildPins(PoolTableSurface surface)
        {
            float diameter = cueBall.Radius * 2f;
            float height = cfg.pinHeight * diameter;
            pinRadius = cfg.pinWidth * diameter * 0.5f;
            Mesh mesh = BowlingPin.BuildMesh(height, pinRadius, RenderSides);
            Mesh colliderMesh = BowlingPin.BuildMesh(height, pinRadius, ColliderSides);
            Material body = ShooterPowerFx.Solid(cfg.pinColor, "BowlingPin Body");
            Material collar = ShooterPowerFx.Solid(cfg.collarColor, "BowlingPin Collar");
            var physics = new PhysicsMaterial("BowlingPin")
            {
                staticFriction = cfg.pinFriction,
                dynamicFriction = cfg.pinFriction,
                bounciness = cfg.pinBounciness,
            };
            var root = new GameObject("BowlingPins");
            created.Add(root);
            created.Add(mesh);
            created.Add(colliderMesh);
            created.Add(body);
            created.Add(collar);
            created.Add(physics);

            // From the table's centre towards the end opposite the cue ball.
            Vector3 center = surface.transform.position;
            Vector3 along = surface.transform.rotation * Vector3.right;
            Vector3 across = surface.transform.rotation * Vector3.forward;
            if (Vector3.Dot(cueBall.SpawnPosition - center, along) > 0f) along = -along;
            Vector3 head = center + along * (surface.HalfLength * cfg.rackPosition);
            float spacing = cfg.pinSpacing * diameter;
            float rowDepth = spacing * Mathf.Sqrt(3f) * 0.5f;

            var materials = new[] { body, collar };
            float mass = cueBall.Rigidbody.mass * cfg.pinMass;
            for (int row = 0; row < Rows; row++)
            {
                for (int i = 0; i <= row; i++)
                {
                    BowlingPin pin = BowlingPin.Create(root.transform, mesh, colliderMesh, materials, physics, mass, height * cfg.pinCenterOfMass, cfg);
                    Vector3 spot = head + along * (row * rowDepth) + across * ((i - row * 0.5f) * spacing);
                    spot.y = feltY;
                    pin.RackSpot = spot;
                    pins.Add(pin);
                }
            }
        }

        private void RackPins()
        {
            foreach (BowlingPin pin in pins) pin.StandAt(pin.RackSpot);
            pinsStanding = pins.Count;
        }

        // ---------- The throw ----------

        // From PoolMatchRules.NotifyShotFired.
        public void OnThrow()
        {
            phase = Phase.Rolling;
            phaseStart = Time.time;
        }

        // The shot isn't over while one is still falling or rolling, up to
        // Settle Timeout: a pin rocking for ever can't hold up the match.
        public bool PinsMoving
        {
            get
            {
                if (phase != Phase.Rolling || Time.time - phaseStart > cfg.settleTimeout) return false;
                foreach (BowlingPin pin in pins)
                    if (pin.gameObject.activeSelf && pin.IsMoving(cfg.settleSpeed, cfg.settleSpin)) return true;
                return false;
            }
        }

        // Every frame (PoolMatchRules.Update).
        public void Tick(PoolMatchRules match)
        {
            switch (phase)
            {
                case Phase.Rolling:
                    // Off the table: gone, before it counts as moving all the way down.
                    foreach (BowlingPin pin in pins)
                        if (pin.gameObject.activeSelf && IsBelowFelt(pin)) pin.gameObject.SetActive(false);
                    // A throw too weak to ever count as moving ends no shot
                    // (PoolMatchRules waits for movement): the player throws again.
                    if (Time.time - phaseStart > cfg.settleTimeout + 1f && !PoolBall.AnyMoving()) phase = Phase.Ready;
                    break;
                case Phase.Sweeping:
                    if (Time.time - phaseStart >= cfg.sweepDelay) EndSweep(match);
                    break;
            }
        }

        private bool IsBelowFelt(BowlingPin pin) => pin.transform.position.y < feltY - cueBall.Radius;

        private bool IsDown(BowlingPin pin) => !pin.gameObject.activeSelf || IsBelowFelt(pin) || pin.IsTippedOver(cosTiltToFall);

        public void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
        {
            int player = match.CurrentPlayer;
            int standing = 0;
            foreach (BowlingPin pin in pins)
                if (!IsDown(pin)) standing++;
            int fell = pinsStanding - standing;
            pinsStanding = standing;

            if (rollOff)
            {
                rollOffPins[player] = fell;
                lastRoll = new BowlingRoll { Strike = fell == BowlingScoreSheet.PinCount, FrameOver = true, Rerack = true };
            }
            else lastRoll = sheets[player].AddRoll(fell);

            phase = Phase.Sweeping;
            phaseStart = Time.time;
            Announce(fell);
            if (cfg.debugLogs)
                Debug.Log($"[Bowling] J{player + 1} : {fell} quille(s){(cueBallPocketed ? ", blanche rentrée" : "")}{(lastRoll.Strike ? ", strike" : lastRoll.Spare ? ", spare" : "")}. Score : {sheets[player].Describe()} = {sheets[player].Total}.");
        }

        private void Announce(int fell)
        {
            if (lastRoll.Strike) MatchHud.ShowShout(-1, cfg.strikeShout, null, true);
            else if (lastRoll.Spare) MatchHud.ShowShout(-1, cfg.spareShout, null, true);
            else if (fell == 0) MatchHud.ShowShout(-1, cfg.gutterShout, cfg.gutterColor);
            else MatchHud.ShowShout(-1, string.Format(cfg.pinsShoutFormat, fell));
        }

        // Fallen pins cleared (or the rack redone), the cue ball back on its
        // spot, then whoever is next.
        private void EndSweep(PoolMatchRules match)
        {
            phase = Phase.Ready;
            if (lastRoll.Rerack) RackPins();
            else ClearFallenPins();
            cueBall.ResetToSpawn();
            if (!lastRoll.FrameOver) return;

            int player = match.CurrentPlayer;
            if (rollOff)
            {
                // Player 1 throws first: the round is over after player 2.
                if (player == 1)
                {
                    rollOffRound++;
                    if (rollOffPins[0] != rollOffPins[1])
                    {
                        Finish(match, rollOffPins[0] > rollOffPins[1] ? 0 : 1);
                        return;
                    }
                    MatchHud.ShowShout(-1, cfg.tieShout);
                }
                match.SwitchTurn();
                return;
            }

            if (sheets[0].Finished && sheets[1].Finished)
            {
                if (sheets[0].Total != sheets[1].Total)
                {
                    Finish(match, sheets[0].Total > sheets[1].Total ? 0 : 1);
                    return;
                }
                rollOff = true;
                MatchHud.ShowShout(-1, cfg.tieShout);
            }
            match.SwitchTurn();
        }

        private void ClearFallenPins()
        {
            foreach (BowlingPin pin in pins)
            {
                if (!pin.gameObject.activeSelf) continue;
                if (IsDown(pin))
                {
                    pin.gameObject.SetActive(false);
                    continue;
                }
                // Pushed onto the cue ball's spot: back to its own, or the ball
                // would come back inside it.
                Vector3 offset = pin.transform.position - cueBall.RespawnPosition;
                offset.y = 0f;
                float apart = cueBall.Radius + pinRadius;
                if (offset.sqrMagnitude < apart * apart) pin.StandAt(pin.RackSpot);
            }
        }

        private void Finish(PoolMatchRules match, int winner)
        {
            if (cfg.debugLogs)
                Debug.Log($"[Bowling] Victoire de J{winner + 1} : {sheets[0].Total} à {sheets[1].Total}{(rollOff ? $", mort subite {rollOffPins[0]} à {rollOffPins[1]}" : "")}.");
            match.Win(winner);
        }

        // ---------- HUD ----------

        public int TotalOf(int player) => sheets[player].Total;

        // Changes whenever DescribeProgress would.
        public int PlateKey(int player) =>
            ((sheets[player].RollCount * 64 + sheets[1 - player].RollCount) * 64 + rollOffRound) * 2 + (rollOff ? 1 : 0);

        // "Frame 2/5  ·  X  7/  ·  l'autre : 34"
        public string DescribeProgress(int player)
        {
            BowlingScoreSheet sheet = sheets[player];
            string stage = rollOff ? cfg.rollOffText
                : sheet.Finished ? cfg.finishedText
                : string.Format(cfg.frameFormat, sheet.Frame + 1, sheet.Frames);
            string rolls = sheet.Describe();
            return $"{stage}{(rolls.Length > 0 ? "  ·  " + rolls : "")}  ·  {string.Format(cfg.opponentFormat, sheets[1 - player].Total)}";
        }

        public string DescribePlayer(int player) => $"{sheets[player].Total} · {DescribeProgress(player)}";
    }
}
