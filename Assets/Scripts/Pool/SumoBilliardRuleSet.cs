using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Sumo Billard: Sumo Pool's table and balls, but what counts is the rack.
    // J1 has the solids, J2 the stripes, no 8; the first with all of theirs in a
    // pocket wins (sending in one of the other's helps them). A player who falls
    // only comes back after a moment, and one of their potted balls comes back
    // at once: pushing the other in still pays. No rounds, so the countdown only
    // opens the match.
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
        private readonly float[] comeBackAt = new float[2];

        public override bool ShowsPlate => CurrentPhase == Phase.Countdown;

        // The copies are Speed Pool's.
        protected override bool KeepsTableBall(PoolBall ball) => !ball.IsExtra && OwnerOf(ball) >= 0;

        protected override void PlaceTableBalls(Vector3 center, Quaternion rotation, float scale)
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

        private void PlaceTableBall(List<PoolBall> group, int index, Vector3 center, Quaternion rotation, Vector2 spot, float scale)
        {
            if (index >= group.Count) return;
            PoolBall ball = group[index];
            float spacing = ball.Radius * 2f * Settings.rackSpacing;
            ball.SetSpawn(center + rotation * new Vector3(spot.x, 0f, spot.y) * spacing + Vector3.up * ball.Radius);
            ball.ResetToSpawn();
            ball.RollingFrictionOverride = Settings.tableBallFriction * scale;
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
            for (int p = 0; p < 2; p++)
            {
                if (comeBackAt[p] <= 0f || Time.time < comeBackAt[p]) continue;
                comeBackAt[p] = 0f;
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
            pottedByPlayer[owner].Add(ball);
            if (Settings.debugLogs) Debug.Log($"[SumoPool] Bille {ball.Number} rentrée, pour J{owner + 1}.", ball);
            if (!IsGroupCleared(GroupOf(owner), ball)) return;
            if (Settings.debugLogs) Debug.Log($"[SumoPool] Victoire de J{owner + 1}.");
            SetControllable(false);
            match.Win(owner);
        }

        private void PlayerFell(PoolMatchRules match, PoolBall ball)
        {
            int p = ball.CueOwner;
            if (p < 0 || p > 1) return;
            if (ball.LastPocket != null) ball.FallIntoTableHole(ball.LastPocket.transform.position, Settings.sinkTime);
            comeBackAt[p] = Time.time + Mathf.Max(Settings.fallRespawnDelay, Settings.sinkTime);
            if (Settings.debugLogs) Debug.Log($"[SumoPool] J{p + 1} tombe.", ball);
            if (Settings.fallReturnsBall) RespotLastPotted(match, p);
        }

        // ---------- HUD ----------

        public override string DescribeScore(int player) => player == 0 ? Settings.solidsLabel : Settings.stripesLabel;

        public override string DescribeRound() =>
            CurrentPhase == Phase.Countdown ? base.DescribeRound() : Settings.billiardGoal;
    }
}
