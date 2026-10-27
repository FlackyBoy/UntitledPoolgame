using System.Collections;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Power Wash Pool, both players at once: Speed Pool (each their own cue
    // ball, P1 solids, P2 stripes), on a felt caked in grime (PowerWashDirt).
    // Your balls wash the felt as they roll, in your colour; going over the
    // opponent's washed felt takes it. One of your balls potted washes a
    // splash around the pocket and comes back to its spot. The 8 is a mud ball:
    // it dirties where it rolls, and potting it ends the match at once (its
    // last toucher gets a bigger splash first). Otherwise the clock decides:
    // most felt washed wins, overtime on a tie. A scratch only costs the
    // Speed Pool respawn delay (no ball is ever kept potted).
    public class PowerWashPoolRuleSet : SpeedPoolRuleSet
    {
        private PowerWashPoolSettings wash;
        private PowerWashMudBall mudBall;

        protected override bool KeepsEightBall => true;

        public override void Setup(PoolMatchRules match)
        {
            wash = PoolSettingsLoader.LoadOrDefault<PowerWashPoolSettings>("PowerWashPoolSettings");
            Cleanup();
            base.Setup(match);

            SpeedPoolSettings speed = PoolSettingsLoader.LoadOrDefault<SpeedPoolSettings>("SpeedPoolSettings");
            PowerWashDirt.Create(wash, speed.player1CueColor, speed.player2CueColor);

            ShowCueBalls();
            // The splash is the only pot effect here.
            PoolPocket.DefaultPotEffects = false;
            mudBall = null;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.Group != BallGroup.Eight) continue;
                if (!ball.TryGetComponent(out mudBall)) mudBall = ball.gameObject.AddComponent<PowerWashMudBall>();
                mudBall.Clear();
            }
        }

        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            if (ball.IsCueBall)
            {
                // Gone from sight until it comes back.
                if (dirt != null) dirt.HideCue(ball);
                base.HandlePocketed(match, ball);
                return;
            }
            if (dirt == null) return;
            dirt.Forget(ball);
            // The pocket's centre: the ball is still on its rim.
            PoolPocket pocket = NearestPocket(ball.transform.position);
            Vector3 point = pocket != null ? pocket.transform.position : ball.transform.position;

            if (ball.Group == BallGroup.Eight)
            {
                int toucher = mudBall != null ? mudBall.LastToucher : -1;
                dirt.Splash(point, wash.eightSplashRadius, toucher);
                if (wash.debugLogs)
                    Debug.Log($"[PowerWash] La 8 est rentrée{(toucher >= 0 ? $" (touchée en dernier par J{toucher + 1}, qui gagne le jet)" : "")} : fin de la partie.", ball);
                dirt.EndNow();
                return;
            }

            int owner = OwnerOf(ball);
            dirt.Splash(point, wash.pocketSplashRadius, owner);
            if (wash.debugLogs)
                Debug.Log($"[PowerWash] La {ball.Number} rentrée lave autour de la poche pour J{owner + 1} " +
                          $"(J1 {dirt.WashedShare(0):P0}, J2 {dirt.WashedShare(1):P0}), elle ressort.", ball);
            // Back next frame: OnPocketed hides it right after this event.
            match.StartCoroutine(BackOnTable(match, ball, match.MatchGeneration));
        }

        // The pocket the ball went in (the event doesn't say which).
        private static PoolPocket NearestPocket(Vector3 point)
        {
            PoolPocket best = null;
            float bestSqr = float.MaxValue;
            foreach (PoolPocket pocket in PoolPocket.Active)
            {
                Vector3 p = pocket.transform.position;
                float sqr = new Vector2(p.x - point.x, p.z - point.z).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = pocket; }
            }
            return best;
        }

        private static IEnumerator BackOnTable(PoolMatchRules match, PoolBall ball, int generation)
        {
            yield return null;
            if (match == null || generation != match.MatchGeneration || ball == null || match.GameOver) yield break;
            match.RespotBall(ball);
        }

        public override string DescribePlayer(int player)
        {
            PowerWashDirt dirt = PowerWashDirt.Instance;
            string colour = player == 0 ? "Blanche bleue · Pleines" : "Blanche rouge · Rayées";
            return dirt == null ? colour : $"{colour} · {dirt.WashedShare(player):P0} lavé";
        }

        // No grime left (another mode starts, Rejouer: Setup builds a fresh one).
        public static void Cleanup()
        {
            if (PowerWashDirt.Instance != null) Object.Destroy(PowerWashDirt.Instance.gameObject);
            ShowCueBalls();
            PoolPocket.DefaultPotEffects = true;
        }

        // A cue ball hidden in a pocket when the match was restarted or left.
        private static void ShowCueBalls()
        {
            foreach (PoolBall ball in PoolBall.Active)
                if (ball.IsCueBall) ball.SetMeshesVisible(true);
        }
    }
}
