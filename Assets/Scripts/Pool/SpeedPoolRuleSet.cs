using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // 8-ball in Speed Pool, and the base of the modes where each player has
    // their own cue ball (Hungry Hole, Snake Pool, Power Wash Pool). P1 (blue
    // cue ball) has the solids, P2 (red) the stripes; the 8 is off the table.
    // Whoever empties their group first wins, whoever potted the last ball:
    // sinking the opponent's ball only helps them. Each pot is judged as it
    // happens (HandlePocketed), whatever the match type. In Speed Pool no
    // shot is ever resolved, the table is never still for long. Turn based
    // (the subclasses), ResolveShot only passes the turn: the shooter plays
    // again if one of their balls scored and their cue ball stayed on the
    // table. Scratch: the cue ball is out (Cue Respawn Delay, or until the
    // shot is over in turns) and the player's last potted ball comes back.
    public class SpeedPoolRuleSet : IPoolRuleSet
    {
        private static readonly BallGroup[] PlayerGroup = { BallGroup.Solid, BallGroup.Stripe };

        // Each group's potted balls, last one on top: a scratch brings it back.
        protected readonly List<PoolBall>[] pottedByPlayer = { new List<PoolBall>(), new List<PoolBall>() };
        private SpeedPoolSettings settings;
        // Turn based: whose balls scored during the current shot.
        private readonly bool[] scoredThisShot = new bool[2];

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

            if (match.TraceShots)
                Debug.Log($"[SpeedPool] Départ : blanche J1 {sharedCue.transform.position}, J2 {secondCue.transform.position} ({secondCue.name}), " +
                          $"{CountGroup(BallGroup.Solid)} pleines, {CountGroup(BallGroup.Stripe)} rayées, {PoolBall.Active.Count} billes actives.");
        }

        // Turn based only (Speed Pool never resolves a shot, see
        // PoolMatchRules.Update). Called when a shot is struck.
        public void BeginShot()
        {
            scoredThisShot[0] = scoredThisShot[1] = false;
        }

        // One of the player's balls did what the mode wants (potted, swallowed,
        // joined the chain): in turns, they play again.
        protected void MarkScored(int player)
        {
            if (player >= 0) scoredThisShot[player] = true;
        }

        // Turn based only. cueBallPocketed: the shooter's own cue ball.
        public void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
        {
            if (match.GameOver) return;
            int player = match.CurrentPlayer;
            bool again = scoredThisShot[player] && !cueBallPocketed;
            if (match.TraceShots)
                Debug.Log($"[ShotTrace] {GetType().Name}, J{player + 1} : {(cueBallPocketed ? "sa blanche est rentrée" : scoredThisShot[player] ? "une de ses billes a marqué" : "aucune de ses billes n'a marqué")} → " +
                          (again ? "il rejoue" : "au tour de l'adversaire"));
            if (!again) match.SwitchTurn();
        }

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
            MarkScored(owner);
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
