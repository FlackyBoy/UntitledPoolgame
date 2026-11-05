using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Shooter Billard: the Shooter miniature's fight, but what counts is the
    // balls. J1 has the solids, J2 the stripes, no 8; the first to have all of
    // theirs in a pocket wins (sending in one of the other's helps them).
    // Pushed there with kicks and shots.
    //
    // No health and no elimination: being hit only costs time. Each hit slows
    // the player for a moment; Hits To Fall in a row knock them down. A ball
    // off the table comes back, only a pocket counts.
    public class ShooterPoolRuleSet : ShooterMiniRuleSet
    {
        private readonly int[] ballsLeft = new int[2];

        protected override int BallsKept => int.MaxValue;
        protected override bool KeepsBall(PoolBall ball) => ball.Group != BallGroup.Eight;

        protected override void OnBallsPlaced()
        {
            ballsLeft[0] = ballsLeft[1] = 0;
            foreach (PoolBall ball in PoolBall.Active)
            {
                int owner = OwnerOf(ball);
                if (owner >= 0) ballsLeft[owner]++;
            }
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

        public override string HealthLabel(int player) => player == 0 ? Settings.solidsLabel : Settings.stripesLabel;

        public override string DescribeScore(int player)
        {
            ShooterMiniSettings s = Settings;
            return CurrentPhase == Phase.Countdown
                ? string.Format(s.countdownFormat, CountdownLeft)
                : string.Format(s.ballsLeftFormat, ballsLeft[player], ballsLeft[1 - player]);
        }

        public override int PlateKey(int player) =>
            ((ballsLeft[player] * 32 + ballsLeft[1 - player]) * 16 + (CurrentPhase == Phase.Countdown ? CountdownLeft : 0)) * 2 + player;
    }
}
