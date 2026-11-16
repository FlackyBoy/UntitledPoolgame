using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Table holes (TableHolePower, Catch the Hole), and the balls that fall in them.
    public partial class PoolMatchRules
    {
        // ---------- Table holes (TableHolePower) ----------

        private class TableHoleEntry
        {
            public TableHole hole;
            public int owner;
            public TableHoleDuration duration;
            public int turnsLeft;
            // Opened while a shot was rolling: "next shot" means the one
            // after it, not the one already under way.
            public bool skipNextResolve;
        }

        private readonly List<TableHoleEntry> tableHoles = new List<TableHoleEntry>();
        private readonly List<PoolBall> ballsInTableHoles = new List<PoolBall>();
        private readonly Dictionary<PoolBall, Vector3> shotStartPositions = new Dictionary<PoolBall, Vector3>();

        // Bumped by every restart: a meteor still falling from the previous
        // match lands without opening anything (TableHoleMeteor).
        public int MatchGeneration { get; private set; }

        // Holes launched during the launcher's own turn: spots marked by a
        // ring, meteors fall at SwitchTurn.
        private class PendingTableHoles
        {
            public TableHolePower power;
            public List<Vector3> centers;
            public int owner;
            public readonly List<GameObject> markers = new List<GameObject>();
        }

        private readonly List<PendingTableHoles> pendingTableHoles = new List<PendingTableHoles>();

        public void QueueTableHoles(TableHolePower power, List<Vector3> centers, int owner)
        {
            // Already the opponent's turn (split screen), or Speed Pool: right now.
            if (IsSimultaneous || CurrentPlayer != owner)
            {
                power.LaunchMeteors(centers, owner, MatchGeneration);
                return;
            }

            var pending = new PendingTableHoles { power = power, centers = centers, owner = owner };
            foreach (Vector3 center in centers)
            {
                LineRenderer ring = RingLine.Create(null, "TableHoleTarget", power.RimWidth, power.RimColor);
                RingLine.Set(ring, center + Vector3.up * 0.003f, power.HoleRadius);
                pending.markers.Add(ring.gameObject);
            }
            pendingTableHoles.Add(pending);
        }

        // From SwitchTurn, CurrentPlayer being the player whose turn starts.
        private void LaunchPendingTableHoles()
        {
            for (int i = pendingTableHoles.Count - 1; i >= 0; i--)
            {
                PendingTableHoles pending = pendingTableHoles[i];
                if (CurrentPlayer == pending.owner) continue;
                pending.power.LaunchMeteors(pending.centers, pending.owner, MatchGeneration);
                DestroyMarkers(pending);
                pendingTableHoles.RemoveAt(i);
            }
        }

        private static void DestroyMarkers(PendingTableHoles pending)
        {
            foreach (GameObject marker in pending.markers)
                if (marker != null) Destroy(marker);
        }

        public void AddTableHole(TableHole hole, int owner, TableHolePower power)
        {
            tableHoles.Add(new TableHoleEntry
            {
                hole = hole,
                owner = owner,
                duration = power.Duration,
                turnsLeft = power.Turns,
                skipNextResolve = IsSimultaneous ? rollingCueBall[owner] != null : shotInProgress,
            });
        }

        // A ball fell in a table hole. The cue ball is a foul (like a
        // scratch). Others sink and come back to where they were once the
        // shot is over. Outside a shot, the ball goes back on the hole's edge.
        public void HandleBallFellInTableHole(PoolBall ball, TableHole hole)
        {
            if (!MatchStarted || GameOver) return;
            if (hole.Living != null)
            {
                HandleBallCaughtByLivingHole(ball, hole);
                return;
            }
            if (!shotInProgress)
            {
                Vector3 away = ball.transform.position - hole.transform.position;
                away.y = 0f;
                Vector3 direction = away.sqrMagnitude > 1e-8f ? away.normalized : Vector3.forward;
                Vector3 edge = hole.transform.position + direction * (hole.RadiusToward(direction) + ball.Radius + 0.005f);
                ball.Rigidbody.linearVelocity = Vector3.zero;
                ball.PlaceAt(new Vector3(edge.x, ball.transform.position.y, edge.z));
                return;
            }
            if (ball.IsCueBall)
            {
                ball.OnPocketed();
                return;
            }
            ball.FallIntoTableHole(hole.transform.position, hole.Power != null ? hole.Power.SinkTime : 0.35f);
            ballsInTableHoles.Add(ball);
        }

        // Catch the Hole: balls a living hole potted during this shot (the 8
        // counts there without a call: a moving hole can't be called).
        private readonly HashSet<PoolBall> caughtByLivingHoleThisShot = new HashSet<PoolBall>();
        public bool WasCaughtByLivingHole(PoolBall ball) => caughtByLivingHoleThisShot.Contains(ball);

        // A living hole (Catch the Hole) reached a ball:
        //   not played (at rest, or between shots): it pops up on the nearest
        //   free spot, the hole came to it;
        //   rolling cue ball: a scratch;
        //   rolling 8 while the shooter isn't on it: back to its spot after
        //   the shot, like the Trou power;
        //   any other rolling ball: potted once it has sunk.
        private void HandleBallCaughtByLivingHole(PoolBall ball, TableHole hole)
        {
            float sinkTime = hole.Power != null ? hole.Power.SinkTime : 0.35f;
            string decision;
            // Read before the fall stops the ball. Speed Pool has no shot loop:
            // any rolling ball is in play.
            bool fellIn = (shotInProgress || IsSimultaneous) && ball.IsRolling;
            if (!fellIn)
            {
                Vector3 spot = FreeSpotNear(ball.transform.position, ball);
                ball.Rigidbody.linearVelocity = Vector3.zero;
                ball.Rigidbody.angularVelocity = Vector3.zero;
                ball.PlaceAt(new Vector3(spot.x, ball.transform.position.y, spot.z));
                decision = TableHole.Overlaps(spot, 0f) ? "non jouée, AUCUNE place libre : reste sur le trou" : "non jouée : réapparaît à côté";
            }
            else if (ball.IsCueBall)
            {
                ball.OnPocketed();
                decision = "blanche qui roule : faute";
            }
            else if (IsSimultaneous)
            {
                // Potted once sunk and judged then like any pot; nothing waits
                // for it (no shot to settle).
                ball.FallIntoTableHole(hole.transform.position, sinkTime, pocket: true, follow: hole.transform);
                decision = "empochée (Speed Pool)";
            }
            else if (ball.Group == BallGroup.Eight && !IsShootingForEightBall(CurrentPlayer))
            {
                ball.FallIntoTableHole(hole.transform.position, sinkTime, follow: hole.transform);
                ballsInTableHoles.Add(ball);
                decision = "la 8 avant son tour : revient à sa place après le tir";
            }
            else
            {
                ball.FallIntoTableHole(hole.transform.position, sinkTime, pocket: true, follow: hole.transform);
                // Listed so the shot waits for the end of the fall (AnyBallSinking).
                ballsInTableHoles.Add(ball);
                caughtByLivingHoleThisShot.Add(ball);
                decision = "empochée";
            }
            // Every ball that really fell in grows it (not one popped aside).
            if (fellIn) hole.Living.OnSwallowed(ball);

            if (hole.Living.Settings.debugLogs)
                Debug.Log($"[CatchTheHole] {hole.name} (rayon {hole.MaxRadius * 100f:0.0} cm) attrape {(ball.IsCueBall ? "la blanche" : $"la {ball.Number}")} : " +
                          $"vitesse {ball.HorizontalSpeed:0.000} m/s, {(fellIn ? "en jeu" : "entre deux tirs")} → {decision}.");
        }

        private bool AnyBallSinking()
        {
            foreach (PoolBall ball in ballsInTableHoles)
                if (ball != null && ball.IsSinking) return true;
            return false;
        }

        private void ReturnBallsFromTableHoles()
        {
            foreach (PoolBall ball in ballsInTableHoles)
            {
                if (ball == null || !ball.InTableHole) continue;
                Vector3 target = shotStartPositions.TryGetValue(ball, out Vector3 before) ? before : ball.transform.position;
                ball.ReturnFromTableHole(FreeSpotNear(target, ball));
            }
            ballsInTableHoles.Clear();
        }


        // Ends the holes whose time is up. CurrentPlayer is the player whose
        // turn is ending, when called from SwitchTurn.
        private void CloseTableHoles(bool turnEnding, bool shotResolved)
        {
            for (int i = tableHoles.Count - 1; i >= 0; i--)
            {
                TableHoleEntry entry = tableHoles[i];
                bool close = entry.hole == null;
                switch (entry.duration)
                {
                    case TableHoleDuration.OpponentTurn:
                        close |= turnEnding && CurrentPlayer != entry.owner;
                        break;
                    case TableHoleDuration.Turns:
                        if (turnEnding && --entry.turnsLeft <= 0) close = true;
                        break;
                    case TableHoleDuration.NextShot:
                        if (shotResolved)
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

        private void ClearTableHoles()
        {
            foreach (TableHoleEntry entry in tableHoles)
                if (entry.hole != null) entry.hole.Close(instant: true);
            tableHoles.Clear();
            ballsInTableHoles.Clear();
            foreach (PendingTableHoles pending in pendingTableHoles) DestroyMarkers(pending);
            pendingTableHoles.Clear();
        }
    }
}
