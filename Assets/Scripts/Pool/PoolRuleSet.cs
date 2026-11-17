using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Base of every rule set: the IPoolRuleSet contract, plus what a mode may
    // change in the match without PoolMatchRules naming it. Each answer here
    // is the plain 8-ball one; a mode overrides only what it does differently.
    public abstract class PoolRuleSet : IPoolRuleSet
    {
        public abstract void Setup(PoolMatchRules match);

        public abstract void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact);

        public abstract string DescribePlayer(int player);

        // Every frame of the match, before the shot loop (rounds, clocks, falls).
        public virtual void Tick(PoolMatchRules match) { }

        // Each player has their own balls (PlayerBallGroups, Speed Pool): a
        // player joining mid-match restarts it, to share them out again.
        // False when the mode has no groups or makes room itself (Sumo Pool).
        public virtual bool SharesBallsOut => false;

        // Whether this player may strike now, on top of the turn rules
        // (PoolMatchRules.CanPlayerShoot).
        public virtual bool AllowsShot(PoolMatchRules match, int playerIndex) => true;

        // A shot was struck (strokes, throws, the seeker's shots), in turns or not.
        // cueBall: the ball struck, null if unknown.
        public virtual void OnShotFired(PoolBall cueBall) { }

        // Something other than the balls still moving (pins falling): the shot
        // isn't judged before it settles.
        public virtual bool IsSettling => false;

        // Whether the power in hand may be launched now, on top of PoolPower's
        // own turn and rolling-balls rules.
        public virtual bool AllowsPower(int player) => true;

        // Multiplier on every shot's strength.
        public virtual float ShotPowerScale => 1f;

        // The player's group for the HUD and the magnet; Cue = none, or not decided yet.
        public virtual BallGroup GroupOf(int player) => BallGroup.Cue;

        // A score to show against a target (14.1, Play Anywhere's par).
        public virtual bool TryGetScore(int player, out int score, out int target)
        {
            score = target = 0;
            return false;
        }

        // What the view from above frames (the free quarter at three
        // players): the middle of the play area, its half extents along the
        // table's length (x) and width (y), and the table's rotation. False
        // without a table.
        public virtual bool GetOverview(out Vector3 center, out Vector2 halfSize, out Quaternion rotation)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null)
            {
                center = Vector3.zero; halfSize = Vector2.zero; rotation = Quaternion.identity;
                return false;
            }
            center = surface.transform.position;
            center.y = surface.FeltTopY;
            halfSize = new Vector2(surface.HalfLength, surface.HalfWidth);
            rotation = surface.transform.rotation;
            return true;
        }
    }
}
