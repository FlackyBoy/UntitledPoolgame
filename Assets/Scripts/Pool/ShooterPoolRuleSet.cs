using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Shooter Billard: the Shooter miniature's fight, but what counts is the
    // balls, shared out as in 8-ball Speed Pool (PlayerBallGroups): J1 the
    // solids, J2 the stripes; at three, 1-5, 6-11 and 12-15; at four, 2 v 2
    // or each for themselves with a second rack. No 8. The first side to have
    // all of theirs in a pocket wins (sending in someone else's helps them).
    // Pushed there with kicks and shots.
    //
    // No health and no elimination: being hit only costs time. Each hit slows
    // the player for a moment; Hits To Fall in a row knock them down. A ball
    // off the table comes back, only a pocket counts.
    public class ShooterPoolRuleSet : ShooterMiniRuleSet
    {
        // Per side (in 2 v 2, the team's first player).
        private readonly int[] ballsLeft = new int[PoolMatchRules.MaxPlayers];

        protected override int BallsKept => int.MaxValue;
        protected override bool KeepsBall(PoolBall ball) => ball.Group != BallGroup.Eight;
        // Patterns and halos as in 8-ball, from three players.
        protected override bool ShowsOwnerMarkers => true;

        protected override void BeforeBallsPlaced() => AddSecondRack();

        protected override void OnBallsPlaced()
        {
            System.Array.Clear(ballsLeft, 0, ballsLeft.Length);
            foreach (PoolBall ball in PoolBall.Active)
            {
                int owner = OwnerOf(ball);
                if (owner >= 0) ballsLeft[owner]++;
            }
            AddOwnerLooks();
        }

        // The most any other side has left: with two, the other player's.
        private int OthersLeft(int player)
        {
            int side = PlayerBallGroups.SideOf(player), most = 0;
            for (int q = 0; q < PlayerBallGroups.Sides; q++)
                if (q != side) most = Mathf.Max(most, ballsLeft[q]);
            return most;
        }

        protected override void UpdateEffects(Fighter f) =>
            f.body.SetSpeedMultiplier(Time.time < f.slowUntil ? Settings.hitSlowMultiplier : 1f);

        // knock, or the last of Hits To Fall in a row: down. Whatever the blow,
        // damage doesn't count here.
        protected override bool Hurt(int p, float damage, Vector3 where, Vector3 knock, string by, Vector3 push)
        {
            ShooterMiniSettings s = Settings;
            Fighter f = fighters[p];
            float now = Time.time;
            if (now < f.safeUntil) return false;
            f.hitsInARow = now - f.lastHitTime > s.hitWindow ? 1 : f.hitsInARow + 1;
            f.lastHitTime = now;
            f.slowUntil = now + s.hitSlowTime;
            bool falls = knock != Vector3.zero || f.hitsInARow >= s.hitsToFall;
            if (falls)
            {
                f.hitsInARow = 0;
                f.body.KnockDown(knock != Vector3.zero ? knock : push, false);
            }
            if (s.debugLogs) Debug.Log($"[ShooterMini] J{p + 1} touché par {by} à {where}{(falls ? " : à terre" : "")}, ralenti {s.hitSlowTime:0.#} s.", f.body);
            return true;
        }

        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (ball.IsCueBall) return;
            int owner = OwnerOf(ball);
            // Off the table, or a stray ball: back on the felt.
            if (ball.LastPocket == null || owner < 0 || CurrentPhase != Phase.Fight)
            {
                QueueRespot(ball);
                return;
            }
            ballsLeft[owner]--;
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] Bille {ball.Number} rentrée : il en reste {ballsLeft[owner]} à J{owner + 1}.", ball);
            if (ballsLeft[owner] <= 0) EndWith(owner);
        }

        // ---------- HUD ----------

        // Solids or stripes (the second rack's too); at three, the numbers.
        public override string HealthLabel(int player)
        {
            int side = PlayerBallGroups.SideOf(player);
            if (PlayerBallGroups.Split != GroupSplit.Thirds) return side % 2 == 0 ? Settings.solidsLabel : Settings.stripesLabel;
            int[] numbers = PlayerBallGroups.Numbers(side);
            return numbers.Length > 0 ? $"{numbers[0]}-{numbers[numbers.Length - 1]}" : "";
        }

        public override string DescribeScore(int player)
        {
            ShooterMiniSettings s = Settings;
            return CurrentPhase == Phase.Countdown
                ? string.Format(s.countdownFormat, CountdownLeft)
                : string.Format(s.ballsLeftFormat, ballsLeft[PlayerBallGroups.SideOf(player)], OthersLeft(player));
        }

        public override int PlateKey(int player) =>
            ((ballsLeft[PlayerBallGroups.SideOf(player)] * 32 + OthersLeft(player)) * 16 + (CurrentPhase == Phase.Countdown ? CountdownLeft : 0)) * 2 + player;
    }
}
