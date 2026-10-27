using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Speed Pool: no turns, both players shoot whenever their own cue ball is
    // still. P1 (blue cue ball) has the solids, P2 (red) the stripes; the 8 is
    // off the table. Whoever empties their group first wins, whoever potted
    // the last ball: sinking the opponent's ball only helps them. Judged on
    // each pot (PoolMatchRules.IsSimultaneous), never on a settled table,
    // since the table is never still for long. Scratch: the cue ball is out
    // for Cue Respawn Delay and the player's last potted ball comes back.
    public class SpeedPoolRuleSet : IPoolRuleSet
    {
        private static readonly BallGroup[] PlayerGroup = { BallGroup.Solid, BallGroup.Stripe };

        // Each group's potted balls, last one on top: a scratch brings it back.
        protected readonly List<PoolBall>[] pottedByPlayer = { new List<PoolBall>(), new List<PoolBall>() };
        private SpeedPoolSettings settings;

        // Hungry Hole plays with the 8.
        protected virtual bool KeepsEightBall => false;

        public virtual void Setup(PoolMatchRules match)
        {
            settings = PoolSettingsLoader.LoadOrDefault<SpeedPoolSettings>("SpeedPoolSettings");

            // Copy: deactivating a ball removes it from Active.
            PoolBall sharedCue = null, secondCue = null;
            foreach (PoolBall ball in new List<PoolBall>(PoolBall.Active))
            {
                if (ball.Group == BallGroup.Eight) { if (!KeepsEightBall) ball.gameObject.SetActive(false); }
                else if (ball.IsCueBall && ball.CueOwner == 1) secondCue = ball;
                else if (ball.IsCueBall && sharedCue == null) sharedCue = ball;
            }
            if (sharedCue == null)
            {
                Debug.LogWarning("[SpeedPool] Pas de bille blanche sur la table.");
                return;
            }

            // The table has one cue ball: the second is a clone, kept by
            // "Rejouer" (which resets it to its own spawn spot).
            if (secondCue == null)
            {
                secondCue = Object.Instantiate(sharedCue, sharedCue.transform.parent);
                secondCue.name = "CueBall_P2 (Speed Pool)";
            }

            // Side by side on the head string, across the table's width.
            Vector3 spot = sharedCue.SpawnPosition;
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 across = surface != null ? surface.transform.rotation * Vector3.forward : Vector3.forward;
            float offset = (surface != null ? surface.HalfWidth : 0.3f) * settings.cueSpread;
            sharedCue.MakePlayerCueBall(0, spot - across * offset, settings.player1CueColor);
            secondCue.MakePlayerCueBall(1, spot + across * offset, settings.player2CueColor);

            SetupExtraBalls();

            if (match.TraceShots)
                Debug.Log($"[SpeedPool] Départ : blanche J1 {sharedCue.transform.position}, J2 {secondCue.transform.position} ({secondCue.name}), " +
                          $"{CountGroup(BallGroup.Solid)} pleines, {CountGroup(BallGroup.Stripe)} rayées, {PoolBall.Active.Count} billes actives.");
        }

        // Shots are never resolved in this mode (see PoolMatchRules.Update).
        public void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact) { }

        // Called as a ball goes in, while it's still in PoolBall.Active.
        public virtual void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (ball.IsCueBall)
            {
                if (settings.respotOwnBallOnScratch && ball.CueOwner >= 0) RespotLastPotted(match, ball.CueOwner);
                return;
            }
            int owner = OwnerOf(ball);
            if (owner >= 0) pottedByPlayer[owner].Add(ball);
            bool cleared = owner >= 0 && IsGroupCleared(PlayerGroup[owner], ball);
            if (match.TraceShots)
                Debug.Log($"[SpeedPool] Bille {ball.Number} rentrée à {ball.transform.position} (bille de J{owner + 1}), " +
                          $"il en reste {CountGroup(ball.Group) - 1} dans son groupe{(cleared ? " → victoire" : "")}.", ball);
            if (cleared) match.Win(owner);
        }

        protected void RespotLastPotted(PoolMatchRules match, int player)
        {
            List<PoolBall> potted = pottedByPlayer[player];
            if (potted.Count == 0) return;
            PoolBall ball = potted[potted.Count - 1];
            potted.RemoveAt(potted.Count - 1);
            match.RespotBall(ball);
            if (match.TraceShots) Debug.Log($"[SpeedPool] Blanche de J{player + 1} rentrée : la bille {ball.Number} ressort.", ball);
        }

        // Copies of each group's balls up to Extra Balls Per Player. Kept by
        // "Rejouer" (back at their own spawn), only the missing ones are made.
        private void SetupExtraBalls()
        {
            var originals = new[] { new List<PoolBall>(), new List<PoolBall>() };
            var extras = new[] { new List<PoolBall>(), new List<PoolBall>() };
            foreach (PoolBall ball in PoolBall.Active)
            {
                int owner = OwnerOf(ball);
                if (owner >= 0) (ball.IsExtra ? extras : originals)[owner].Add(ball);
            }

            int wanted = settings.extraBallsPerPlayer;
            originals[0].Sort((a, b) => a.Number.CompareTo(b.Number));
            originals[1].Sort((a, b) => a.Number.CompareTo(b.Number));
            // Worked out before any copy exists: a new copy sits on its model
            // until placed, and would count as part of the rack.
            Vector3 rackCenter = RackCenter();
            var spots = new List<Vector3>();
            int missing = Mathf.Max(0, wanted - extras[0].Count) + Mathf.Max(0, wanted - extras[1].Count);
            IEnumerator<Vector3> rows = RackExtensionSpots();
            while (spots.Count < missing && rows.MoveNext()) spots.Add(rows.Current);
            int nextSpot = 0;
            for (int i = 0; i < Mathf.Max(wanted, Mathf.Max(extras[0].Count, extras[1].Count)); i++)
            {
                // Alternated, so the added rows mix both groups like the rack.
                for (int p = 0; p < 2; p++)
                {
                    if (i < extras[p].Count)
                    {
                        // Fewer wanted than last match: this one sits out.
                        if (i >= wanted) extras[p][i].gameObject.SetActive(false);
                        continue;
                    }
                    if (i >= wanted || originals[p].Count == 0) continue;
                    PoolBall model = originals[p][i % originals[p].Count];
                    PoolBall extra = Object.Instantiate(model, model.transform.parent);
                    extra.name = $"{model.name} (Speed Pool +{i + 1})";
                    extra.MarkExtra(nextSpot < spots.Count ? spots[nextSpot++] : PoolMatchRules.FindFreeSpot(rackCenter, extra));
                }
            }
        }

        private static Vector3 RackCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.IsCueBall || ball.IsExtra) continue;
                sum += ball.SpawnPosition;
                count++;
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        // Rows behind the rack's back row, one ball wider each time, until
        // a whole row would be off the felt or in a pocket.
        private static IEnumerator<Vector3> RackExtensionSpots()
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            var rack = new List<PoolBall>();
            foreach (PoolBall ball in PoolBall.Active)
                if (!ball.IsCueBall && !ball.IsExtra) rack.Add(ball);
            if (surface == null || rack.Count == 0) yield break;

            float radius = rack[0].Radius;
            float diameter = radius * 2f;
            Vector3 center = surface.transform.position;
            Vector3 across = surface.transform.rotation * Vector3.forward;
            Vector3 along = surface.transform.rotation * Vector3.right;
            if (Vector3.Dot(RackCenter() - center, along) < 0f) along = -along;

            float back = float.MinValue;
            foreach (PoolBall ball in rack) back = Mathf.Max(back, Vector3.Dot(ball.SpawnPosition - center, along));
            int backCount = 0;
            float acrossSum = 0f;
            foreach (PoolBall ball in rack)
            {
                if (Mathf.Abs(Vector3.Dot(ball.SpawnPosition - center, along) - back) > radius * 0.5f) continue;
                backCount++;
                acrossSum += Vector3.Dot(ball.SpawnPosition - center, across);
            }
            float acrossMid = acrossSum / backCount;
            float y = rack[0].SpawnPosition.y;
            const float RowSpacing = 0.8660254f; // sqrt(3)/2: balls touching in a triangle

            for (int row = 1; ; row++)
            {
                int n = backCount + row;
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = center + along * (back + diameter * RowSpacing * row)
                              + across * (acrossMid + (i - (n - 1) * 0.5f) * diameter);
                    p.y = y;
                    Vector3 onFelt = surface.ClampToPlayArea(p, radius);
                    onFelt.y = y;
                    Vector3 clear = PoolPocket.AvoidAllPockets(onFelt, radius);
                    clear.y = y;
                    if ((onFelt - p).sqrMagnitude > 1e-6f || (clear - p).sqrMagnitude > 1e-6f) continue;
                    any = true;
                    yield return p;
                }
                if (!any) yield break;
            }
        }

        private static int CountGroup(BallGroup group)
        {
            int count = 0;
            foreach (PoolBall ball in PoolBall.Active)
                if (ball.Group == group) count++;
            return count;
        }

        // Player whose group the ball is in, -1 for none (the 8, a cue ball).
        public static int OwnerOf(PoolBall ball) => ball.Group == BallGroup.Solid ? 0 : ball.Group == BallGroup.Stripe ? 1 : -1;

        public BallGroup GroupOf(int player) => PlayerGroup[player];

        // leaving: the ball being potted, not off the table yet.
        protected static bool IsGroupCleared(BallGroup group, PoolBall leaving)
        {
            foreach (PoolBall ball in PoolBall.Active)
                if (ball != leaving && ball.Group == group) return false;
            return true;
        }

        public virtual string DescribePlayer(int player) => player == 0 ? "Blanche bleue · Pleines (1-7)" : "Blanche rouge · Rayées (9-15)";
    }
}
