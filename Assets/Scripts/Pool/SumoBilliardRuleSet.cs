using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Sumo Billard: Sumo Pool's table and balls, but what counts is the rack.
    // The balls are shared out as in 8-ball Speed Pool (PlayerBallGroups): J1
    // the solids, J2 the stripes; at three, 1-5, 6-11 and 12-15; at four, 2 v 2
    // or each for themselves with a second rack. No 8. The first side with all
    // of theirs in a pocket wins (sending in someone else's helps them). A
    // player who falls only comes back after a moment, and one of their potted
    // balls comes back at once: pushing the others in still pays. No rounds,
    // so the countdown only opens the match. A player who joins restarts it
    // (SharesBallsOut).
    public class SumoBilliardRuleSet : SumoPoolRuleSet
    {
        private const float RowSpacing = 0.8660254f; // sqrt(3)/2: balls touching in a triangle

        // Half of the rack, rows across the line between the players (2, 3, 4,
        // 3, 2 balls) in ball diameters; the other half is its mirror through the
        // centre. A solid on one side and a stripe on the other, so that neither
        // player starts nearer their balls.
        private static readonly Vector2[] HalfRack =
        {
            new Vector2(-2f * RowSpacing, -0.5f), new Vector2(-2f * RowSpacing, 0.5f),
            new Vector2(-RowSpacing, -1f), new Vector2(-RowSpacing, 0f), new Vector2(-RowSpacing, 1f),
            new Vector2(0f, -1.5f), new Vector2(0f, -0.5f),
        };

        // Every ball racked here, potted or not, to give back to the table.
        private readonly List<PoolBall> racked = new List<PoolBall>();
        // Off the table: OnPocketed turns it off just after HandlePocketed.
        private readonly List<PoolBall> toRespot = new List<PoolBall>();
        // When each player comes back after a fall, 0 = on the table.
        private readonly float[] comeBackAt = new float[PoolMatchRules.MaxPlayers];

        public override bool ShowsPlate => CurrentPhase == Phase.Countdown;
        // The balls are shared out for however many players there are.
        public override bool SharesBallsOut => true;
        protected override bool ShowsOwnerMarkers => true;

        // The copies are Speed Pool's.
        protected override bool KeepsTableBall(PoolBall ball) => !ball.IsExtra && OwnerOf(ball) >= 0;

        protected override float TableBallFriction(float scale) => Settings.tableBallFriction * scale;

        protected override void PlaceTableBalls(Vector3 center, Quaternion rotation, float scale)
        {
            System.Array.Clear(comeBackAt, 0, comeBackAt.Length);
            AddSecondRack();
            if (PlayerBallGroups.Sides > 2) PlaceMixedBalls(center, rotation, scale);
            else PlaceSolidsAndStripes(center, rotation, scale);
            AddOwnerLooks();
        }

        // Two sides: the diamond above, mirrored.
        private void PlaceSolidsAndStripes(Vector3 center, Quaternion rotation, float scale)
        {
            var solids = new List<PoolBall>();
            var stripes = new List<PoolBall>();
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.IsCueBall) continue;
                (OwnerOf(ball) == 0 ? solids : stripes).Add(ball);
            }
            Shuffle(solids);
            Shuffle(stripes);

            for (int i = 0; i < HalfRack.Length; i++)
            {
                bool solidFirst = Random.value < 0.5f;
                PlaceTableBall(solidFirst ? solids : stripes, i, center, rotation, HalfRack[i], scale);
                PlaceTableBall(solidFirst ? stripes : solids, i, center, rotation, -HalfRack[i], scale);
            }
        }

        // Three or four sides: the balls packed in a round cluster in the
        // middle, dealt one each in turn from the centre out (players and
        // balls in a random order), so nobody has all theirs on the edge.
        private void PlaceMixedBalls(Vector3 center, Quaternion rotation, float scale)
        {
            int sides = PlayerBallGroups.Sides;
            var bySide = new List<PoolBall>[sides];
            for (int s = 0; s < sides; s++) bySide[s] = new List<PoolBall>();
            int count = 0;
            foreach (PoolBall ball in PoolBall.Active)
            {
                int side = OwnerOf(ball);
                if (ball.IsCueBall || side < 0) continue;
                bySide[side].Add(ball);
                count++;
            }

            // Hexagonal packing (balls touching), nearest the middle first.
            var spots = new List<Vector2>();
            for (int ring = 0; spots.Count < count * 3 && ring < 12; ring++)
                for (int r = -ring; r <= ring; r++)
                    for (int q = -ring; q <= ring; q++)
                    {
                        if (Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(q + r)) != ring) continue;
                        spots.Add(new Vector2(q + r * 0.5f, r * RowSpacing));
                    }
            spots.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));

            var order = new int[sides];
            for (int s = 0; s < sides; s++)
            {
                order[s] = s;
                Shuffle(bySide[s]);
            }
            for (int s = sides - 1; s > 0; s--)
            {
                int k = Random.Range(0, s + 1);
                (order[s], order[k]) = (order[k], order[s]);
            }
            int next = 0;
            for (int round = 0; next < count && next < spots.Count; round++)
                foreach (int s in order)
                    if (round < bySide[s].Count && next < spots.Count) PlaceTableBall(bySide[s], round, center, rotation, spots[next++], scale);
        }

        private void PlaceTableBall(List<PoolBall> group, int index, Vector3 center, Quaternion rotation, Vector2 spot, float scale)
        {
            if (index >= group.Count) return;
            PoolBall ball = group[index];
            float spacing = ball.Radius * 2f * Settings.rackSpacing;
            ball.SetSpawn(center + rotation * new Vector3(spot.x, 0f, spot.y) * spacing + Vector3.up * ball.Radius);
            ball.ResetToSpawn();
            ball.RollingFrictionOverride = TableBallFriction(scale);
            racked.Add(ball);
        }

        private static void Shuffle(List<PoolBall> balls)
        {
            for (int i = balls.Count - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                (balls[i], balls[other]) = (balls[other], balls[i]);
            }
        }

        protected override void TearDown()
        {
            foreach (PoolBall ball in racked)
            {
                if (ball == null) continue;
                ball.RollingFrictionOverride = -1f;
                ball.SetSpawn(ball.SpawnPosition);
                ball.ResetToSpawn();
            }
            racked.Clear();
            base.TearDown();
        }

        protected override void UpdateRules(PoolMatchRules match)
        {
            foreach (PoolBall ball in toRespot) match.RespotBall(ball);
            toRespot.Clear();
            for (int p = 0; p < comeBackAt.Length; p++)
            {
                if (comeBackAt[p] <= 0f || Time.time < comeBackAt[p]) continue;
                comeBackAt[p] = 0f;
                if (balls[p] == null) continue;
                PoolBall ball = balls[p].Ball;
                // Another ball may have rolled onto its start.
                Vector3 spot = PoolMatchRules.FindFreeSpot(ball.RespawnPosition, ball);
                balls[p].BackToStart();
                ball.PlaceAt(spot);
            }
        }

        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (ball.IsCueBall)
            {
                PlayerFell(match, ball);
                return;
            }
            int owner = OwnerOf(ball);
            // Off the table, or a stray ball: back on the felt.
            if (ball.LastPocket == null || owner < 0 || CurrentPhase != Phase.Play)
            {
                toRespot.Add(ball);
                return;
            }
            // owner: the side (in 2 v 2, the team's first player).
            pottedByPlayer[owner].Add(ball);
            if (Settings.debugLogs) Debug.Log($"[SumoPool] Bille {ball.Number} rentrée, pour J{owner + 1}.", ball);
            if (!IsSideCleared(owner, ball)) return;
            if (Settings.debugLogs) Debug.Log($"[SumoPool] Victoire de J{owner + 1}{(PlayerBallGroups.Teams ? " et de son équipier" : "")}.");
            SetControllable(false);
            match.Win(owner);
        }

        private void PlayerFell(PoolMatchRules match, PoolBall ball)
        {
            int p = ball.CueOwner;
            if (p < 0 || p >= comeBackAt.Length || balls[p] == null) return;
            if (ball.LastPocket != null) ball.FallIntoTableHole(ball.LastPocket.transform.position, Settings.sinkTime);
            comeBackAt[p] = Time.time + Mathf.Max(Settings.fallRespawnDelay, Settings.sinkTime);
            if (Settings.debugLogs) Debug.Log($"[SumoPool] J{p + 1} tombe.", ball);
            if (Settings.fallReturnsBall) RespotLastPotted(match, PlayerBallGroups.SideOf(p));
        }

        // ---------- HUD ----------

        // Solids or stripes (the second rack's too); at three, the numbers.
        public override string DescribeScore(int player)
        {
            int side = PlayerBallGroups.SideOf(player);
            if (PlayerBallGroups.Split != GroupSplit.Thirds) return side % 2 == 0 ? Settings.solidsLabel : Settings.stripesLabel;
            int[] numbers = PlayerBallGroups.Numbers(side);
            return numbers.Length > 0 ? $"{numbers[0]}-{numbers[numbers.Length - 1]}" : "";
        }

        public override string DescribeRound() =>
            CurrentPhase == Phase.Countdown ? base.DescribeRound() : Settings.billiardGoal;
    }
}
