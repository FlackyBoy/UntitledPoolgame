using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Hungry Hole, both players at once: Speed Pool (each their own cue ball,
    // P1 solids, P2 stripes, no turns), but each cue ball is a hole
    // (HungryHoleCue) and the balls' sizes follow their number, the 8 the
    // biggest. Swallowing one of your balls grows your hole, one of the
    // opponent's shrinks it (and counts for them). Win: your group gone, then
    // the 8 swallowed by your hole; the 8 swallowed before that loses. The 8
    // in a pocket comes back. Scratch (the hole's centre over a pocket): the
    // Speed Pool penalty.
    public class HungryHoleRuleSet : SpeedPoolRuleSet
    {
        private HungryHoleSettings hungry;
        // Whose hole swallowed a ball still sinking (it counts once it's down).
        private readonly Dictionary<PoolBall, int> swallowedBy = new Dictionary<PoolBall, int>();
        // Last hole to touch each ball: credit for a ball it sends in a pocket.
        private readonly Dictionary<PoolBall, int> lastTouchedBy = new Dictionary<PoolBall, int>();
        // A cue ball whose hole was eaten: its "pot" isn't a scratch.
        private PoolBall eatenCue;

        protected override bool KeepsEightBall => true;

        public override void Setup(PoolMatchRules match)
        {
            hungry = PoolSettingsLoader.LoadOrDefault<HungryHoleSettings>("HungryHoleSettings");
            swallowedBy.Clear();
            lastTouchedBy.Clear();
            base.Setup(match);
            ApplyBallSizes(hungry);

            float unit = 0f;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (!ball.IsCueBall || ball.CueOwner < 0) continue;
                if (!ball.TryGetComponent(out HungryHoleCue cue)) cue = ball.gameObject.AddComponent<HungryHoleCue>();
                cue.Init(hungry, this, ball.CueOwner);
                unit = ball.Radius / ball.SizeScale;
            }
            propUnit = unit;
            SpawnProps(hungry, unit);
        }

        // A normal ball's radius, for props spawned later.
        private float propUnit;

        private IEnumerator RespawnPropLater(PoolMatchRules match, int generation, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (match == null || generation != match.MatchGeneration || match.GameOver || HungryHoleCue.All.Count == 0) yield break;
            HungryHoleProp prop = SpawnOneProp(hungry, propUnit);
            if (prop != null) prop.PopIn(hungry.propPopTime);
            if (hungry.debugLogs) Debug.Log(prop != null ? $"[HungryHole] {prop.name} réapparaît." : "[HungryHole] Pas de place libre pour faire réapparaître un objet.");
        }

        public void OnPropEaten(HungryHoleCue cue, HungryHoleProp prop)
        {
            // Another one comes back somewhere else after a while.
            PoolMatchRules match = PoolMatchRules.Instance;
            if (match != null && hungry.propRespawnDelay.y > 0f)
                match.StartCoroutine(RespawnPropLater(match, match.MatchGeneration, Random.Range(hungry.propRespawnDelay.x, Mathf.Max(hungry.propRespawnDelay.x, hungry.propRespawnDelay.y))));
            float before = cue.TargetRadius;
            cue.Grow(prop.Size * hungry.growPerPropSize);
            if (hungry.debugLogs)
                Debug.Log($"[HungryHole] Le trou de J{cue.Owner + 1} mange {prop.name} (taille {prop.Size:0.0}) : trou {before * 100f:0.0} → {cue.TargetRadius * 100f:0.0} cm.", cue);
        }

        // Props from the settings' list (weighted), each on a free spot of the
        // felt: away from the balls, the pockets and each other.
        private static void SpawnProps(HungryHoleSettings settings, float unit)
        {
            HungryHoleProp.ClearAll();
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null || unit <= 0f || settings.props == null || settings.props.Length == 0) return;
            float totalWeight = 0f;
            foreach (HungryHolePropEntry entry in settings.props)
                if (entry != null && entry.prefab != null) totalWeight += entry.weight;
            if (totalWeight <= 0f) return;

            int placed = 0;
            for (int n = 0; n < settings.propCount; n++)
                if (SpawnOneProp(settings, unit) != null) placed++;
            if (settings.debugLogs) Debug.Log($"[HungryHole] {placed}/{settings.propCount} objets posés sur le tapis.");
        }

        // One prop from the list (weighted) on a free spot; null if none.
        private static HungryHoleProp SpawnOneProp(HungryHoleSettings settings, float unit)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null || unit <= 0f || settings.props == null) return null;
            float totalWeight = 0f;
            foreach (HungryHolePropEntry entry in settings.props)
                if (entry != null && entry.prefab != null) totalWeight += entry.weight;
            if (totalWeight <= 0f) return null;
            HungryHolePropEntry pick = Pick(settings.props, totalWeight);
            float reach = (pick.size + settings.propClearance) * unit;
            if (!TryFindPropSpot(surface, reach, out Vector3 spot)) return null;
            return HungryHoleProp.Spawn(pick.prefab, spot, pick.size, settings.propMaxHeight, unit);
        }

        private static HungryHolePropEntry Pick(HungryHolePropEntry[] entries, float totalWeight)
        {
            float roll = Random.value * totalWeight;
            HungryHolePropEntry last = null;
            foreach (HungryHolePropEntry entry in entries)
            {
                if (entry == null || entry.prefab == null) continue;
                last = entry;
                roll -= entry.weight;
                if (roll <= 0f) return entry;
            }
            return last;
        }

        private static bool TryFindPropSpot(PoolTableSurface surface, float reach, out Vector3 spot)
        {
            float x = Mathf.Max(0f, surface.HalfLength - reach), z = Mathf.Max(0f, surface.HalfWidth - reach);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                spot = surface.transform.position + surface.transform.rotation * new Vector3(Random.Range(-x, x), 0f, Random.Range(-z, z));
                spot.y = surface.FeltTopY;
                if (IsFree(spot, reach)) return true;
            }
            spot = default;
            return false;
        }

        // reach: the prop's radius plus the clearance kept around it.
        private static bool IsFree(Vector3 spot, float reach)
        {
            foreach (PoolPocket pocket in PoolPocket.Active)
                if (Flat(spot - pocket.transform.position) < reach + pocket.Radius) return false;
            foreach (PoolBall ball in PoolBall.Active)
                if (Flat(spot - ball.transform.position) < reach + ball.Radius) return false;
            foreach (HungryHoleProp prop in HungryHoleProp.All)
                if (Flat(spot - prop.Center) < reach + prop.Radius) return false;
            // Not right under a hole: it would be eaten as it appears.
            foreach (HungryHoleCue cue in HungryHoleCue.All)
                if (Flat(spot - cue.transform.position) < reach + cue.Radius * 1.5f) return false;
            return true;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // Called by a hole as it swallows a ball (before the ball has sunk).
        // True for a good bite: one of its balls, or the 8 once its group is gone.
        public bool OnSwallowed(HungryHoleCue cue, PoolBall ball)
        {
            swallowedBy[ball] = cue.Owner;
            int owner = OwnerOf(ball);
            float before = cue.TargetRadius;
            if (owner == cue.Owner) cue.Grow(hungry.growPerBall);
            else if (owner >= 0) cue.Grow(-hungry.shrinkPerBall);
            if (hungry.debugLogs)
                Debug.Log($"[HungryHole] Le trou de J{cue.Owner + 1} avale la {ball.Number} (rayon bille {ball.Radius * 100f:0.0} cm, " +
                          $"{(owner == cue.Owner ? "à lui" : owner >= 0 ? "à l'adversaire" : "la 8")}) : trou {before * 100f:0.0} → {cue.TargetRadius * 100f:0.0} cm.", cue);
            if (owner >= 0) return owner == cue.Owner;
            return IsGroupCleared(GroupOf(cue.Owner), null);
        }

        public void NoteTouch(PoolBall ball, int owner) => lastTouchedBy[ball] = owner;

        // The bigger hole ate the other: it grows, the other comes back shrunk
        // after the Speed Pool delay (no ball respotted, it isn't a scratch).
        public void OnHoleEaten(HungryHoleCue eater, HungryHoleCue eaten)
        {
            float before = eater.TargetRadius;
            eater.Grow(hungry.growPerHoleEaten);
            float eatenBefore = eaten.TargetRadius;
            eaten.ShrinkBy(hungry.eatenSizeFactor);
            if (hungry.debugLogs)
                Debug.Log($"[HungryHole] Le trou de J{eater.Owner + 1} mange celui de J{eaten.Owner + 1} : J{eater.Owner + 1} {before * 100f:0.0} → {eater.TargetRadius * 100f:0.0} cm, " +
                          $"J{eaten.Owner + 1} {eatenBefore * 100f:0.0} → {eaten.TargetRadius * 100f:0.0} cm.", eater);
            eatenCue = eaten.Ball;
            eaten.Ball.OnPocketed();
            eatenCue = null;
        }

        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (ball.IsCueBall)
            {
                if (ball != eatenCue) base.HandlePocketed(match, ball);
                return;
            }
            int eater = swallowedBy.TryGetValue(ball, out int e) ? e : -1;
            swallowedBy.Remove(ball);
            int pusher = lastTouchedBy.TryGetValue(ball, out int t) ? t : -1;
            lastTouchedBy.Remove(ball);

            if (ball.Group == BallGroup.Eight)
            {
                if (eater < 0)
                {
                    // In a pocket: not a hole, it comes back (once it's gone).
                    match.StartCoroutine(RespotNextFrame(match, ball, match.MatchGeneration));
                    return;
                }
                bool ready = IsGroupCleared(GroupOf(eater), null);
                if (hungry.debugLogs) Debug.Log($"[HungryHole] La 8 avalée par J{eater + 1} : {(ready ? "groupe vidé → victoire" : "trop tôt → défaite")}.");
                match.Win(ready ? eater : 1 - eater);
                return;
            }

            // Counts for its owner whoever swallowed it; no win yet, the 8 is
            // still to take.
            int owner = OwnerOf(ball);
            if (owner >= 0) pottedByPlayer[owner].Add(ball);

            // One of yours sent in a pocket by your hole: grows it like a swallow.
            if (eater < 0 && owner >= 0 && pusher == owner)
            {
                HungryHoleCue cue = CueOf(owner);
                if (cue == null) return;
                float before = cue.TargetRadius;
                cue.Grow(hungry.growPerBall);
                if (hungry.debugLogs)
                    Debug.Log($"[HungryHole] J{owner + 1} rentre sa {ball.Number} en poche : trou {before * 100f:0.0} → {cue.TargetRadius * 100f:0.0} cm.", cue);
            }
        }

        private static HungryHoleCue CueOf(int owner)
        {
            foreach (HungryHoleCue cue in HungryHoleCue.All)
                if (cue.Owner == owner) return cue;
            return null;
        }

        // OnPocketed hides the ball right after this event: put back after.
        private static IEnumerator RespotNextFrame(PoolMatchRules match, PoolBall ball, int generation)
        {
            yield return null;
            if (match == null || generation != match.MatchGeneration || ball == null) yield break;
            match.RespotBall(ball);
        }

        public override string DescribePlayer(int player) =>
            player == 0 ? "Trou bleu · Pleines (1-7), puis la 8" : "Trou rouge · Rayées (9-15), puis la 8";

        // Each object ball at its size, the rack spread around its apex (the
        // ball nearest the cue ball's spot) so the biggest still fit.
        private static void ApplyBallSizes(HungryHoleSettings settings)
        {
            var balls = new List<PoolBall>();
            PoolBall cue = null;
            float biggest = 1f;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.IsCueBall) { if (cue == null) cue = ball; continue; }
                balls.Add(ball);
                biggest = Mathf.Max(biggest, settings.BallScale(ball));
            }
            if (balls.Count == 0) return;

            Vector3 apex = balls[0].SpawnPosition;
            if (cue != null)
            {
                float nearest = float.MaxValue;
                foreach (PoolBall ball in balls)
                {
                    float d = (ball.SpawnPosition - cue.SpawnPosition).sqrMagnitude;
                    if (d < nearest) { nearest = d; apex = ball.SpawnPosition; }
                }
            }

            PoolTableSurface surface = PoolTableSurface.Instance;
            float spread = biggest * 1.02f;
            foreach (PoolBall ball in balls)
            {
                float normalRadius = ball.Radius / ball.SizeScale;
                float scale = settings.BallScale(ball);
                ball.SetSize(scale, settings.massExponent);
                Vector3 spot = apex + (ball.SpawnPosition - apex) * spread;
                if (surface != null) spot = surface.ClampToPlayArea(spot, ball.Radius);
                spot.y = surface != null ? surface.FeltTopY + ball.Radius : ball.SpawnPosition.y + normalRadius * (scale - 1f);
                ball.SetSpawn(spot);
                ball.PlaceAt(spot);
            }
        }

        // Every ball back to normal and no hole left (another mode starts).
        public static void Cleanup()
        {
            for (int i = HungryHoleCue.All.Count - 1; i >= 0; i--) HungryHoleCue.All[i].Remove();
            HungryHoleProp.ClearAll();
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                if (Mathf.Approximately(ball.SizeScale, 1f)) continue;
                ball.ResetSize();
                ball.ResetToSpawn();
            }
        }
    }
}
