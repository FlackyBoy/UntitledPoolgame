using System.Collections;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Snake Pool: each player their own cue ball (SpeedPoolRuleSet: P1
    // solids, P2 stripes, at once or in turns), but no ball leaves the table. Your
    // cue ball touching one of your balls, or one of your balls going in a
    // pocket, adds it to the chain trailing behind your cue ball (SnakeChain).
    // The opponent's cue ball hitting your chain cuts it there. Win: your
    // whole group in the chain, then the 8 (it joins the tail), then call a
    // pocket and send your cue ball in it: the whole chain pours in after
    // it, and a cut on the way spoils it. Another pocket, or none called:
    // the chain falls off. The 8 in a pocket comes back; scratch without the
    // 8: the chain falls off (and the Speed Pool respawn delay).
    public class SnakePoolRuleSet : SpeedPoolRuleSet
    {
        private SnakePoolSettings snake;
        // Balls in each group at the start: a chain this long is complete.
        private readonly int[] groupSize = new int[2];
        // Each player's called pocket, ringed for both players to see. Static
        // so Cleanup can take the rings down once the rule set is gone.
        private static readonly PoolPocket[] calledPocket = new PoolPocket[2];

        protected override bool KeepsEightBall => true;

        public override void Setup(PoolMatchRules match)
        {
            snake = PoolSettingsLoader.LoadOrDefault<SnakePoolSettings>("SnakePoolSettings");
            Cleanup();
            base.Setup(match);

            groupSize[0] = groupSize[1] = 0;
            foreach (PoolBall ball in PoolBall.Active)
            {
                int owner = OwnerOf(ball);
                if (owner >= 0) groupSize[owner]++;
                if (!ball.IsCueBall || ball.CueOwner < 0) continue;
                if (!ball.TryGetComponent(out SnakeChain chain)) chain = ball.gameObject.AddComponent<SnakeChain>();
                chain.Init(snake, this, ball.CueOwner);
            }
        }

        public bool IsComplete(SnakeChain chain) => chain.Length >= groupSize[chain.Owner];

        public void OnJoined(SnakeChain chain, PoolBall ball)
        {
            MarkScored(chain.Owner);
            if (!snake.debugLogs) return;
            if (ball.Group == BallGroup.Eight)
                Debug.Log($"[SnakePool] J{chain.Owner + 1} attrape la 8 : à lui d'annoncer sa poche.", ball);
            else
                Debug.Log($"[SnakePool] La {ball.Number} rejoint la chaîne de J{chain.Owner + 1} : {chain.Length}/{groupSize[chain.Owner]}" +
                          $"{(IsComplete(chain) ? " → complète, à la 8 !" : "")}.", ball);
        }

        // The 8 at the tail and no pocket called yet: aiming waits for the
        // call (LocalPoolAimController.HandleCallPocket).
        public bool NeedsPocketCall(int player)
        {
            SnakeChain chain = SnakeChain.Of(player);
            return chain != null && chain.HasEight && !chain.InFinale && calledPocket[player] == null;
        }

        public bool IsGoingForPocket(int player)
        {
            SnakeChain chain = SnakeChain.Of(player);
            return chain != null && chain.HasEight;
        }

        public PoolPocket CalledPocket(int player) => calledPocket[player];

        public void CallPocket(int player, PoolPocket pocket)
        {
            SetCall(player, pocket);
            if (snake.debugLogs && pocket != null) Debug.Log($"[SnakePool] J{player + 1} annonce sa poche : {pocket.DescribeLocation()}.", pocket);
        }

        private static void SetCall(int player, PoolPocket pocket)
        {
            if (calledPocket[player] == pocket) return;
            if (calledPocket[player] != null) calledPocket[player].SetTargeted(false);
            calledPocket[player] = pocket;
            if (pocket != null) pocket.SetTargeted(true);
        }

        // A cut drops the 8 (it's the tail): the call goes with it.
        public void OnCut(SnakeChain victim)
        {
            if (!victim.HasEight) SetCall(victim.Owner, null);
        }

        // The whole chain, the 8 last, followed the head into the pocket.
        public void OnFinaleDone(SnakeChain chain, int pottedCount)
        {
            PoolMatchRules match = PoolMatchRules.Instance;
            if (match == null || match.GameOver) return;
            if (snake.debugLogs) Debug.Log($"[SnakePool] La chaîne de J{chain.Owner + 1} est rentrée en entier ({pottedCount} billes, la 8 en dernier) → victoire.", chain);
            match.Win(chain.Owner);
        }

        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (ball.IsCueBall)
            {
                // No base call: no ball to respot, the chain is the penalty.
                SnakeChain chain = ball.CueOwner >= 0 ? SnakeChain.Of(ball.CueOwner) : null;
                PoolPocket pocket = NearestPocket(ball.transform.position);
                if (chain != null && chain.HasEight)
                {
                    PoolPocket called = calledPocket[chain.Owner];
                    if (pocket != null && pocket == called)
                    {
                        if (snake.debugLogs) Debug.Log($"[SnakePool] Blanche de J{chain.Owner + 1} rentrée dans la poche annoncée avec la 8 au bout : la chaîne s'engouffre.", ball);
                        chain.BeginFinale(pocket.transform.position);
                        return;
                    }
                    // Not the called pocket: the 8 falls off with the rest.
                    if (snake.debugLogs)
                        Debug.Log($"[SnakePool] Blanche de J{chain.Owner + 1} rentrée {(called == null ? "sans poche annoncée" : $"dans la mauvaise poche (annoncée : {called.DescribeLocation()})")} : la chaîne tombe, 8 comprise.", ball);
                    SetCall(chain.Owner, null);
                    chain.ReleaseFrom(0, null, Vector3.zero);
                    return;
                }
                if (chain != null && snake.scratchDropsChain)
                {
                    if (snake.debugLogs && chain.Length > 0) Debug.Log($"[SnakePool] Blanche de J{chain.Owner + 1} rentrée : sa chaîne de {chain.Length} tombe.", ball);
                    chain.ReleaseFrom(0, null, Vector3.zero);
                }
                return;
            }
            // Out of the pocket next frame (OnPocketed hides it right after
            // this event): onto its owner's chain, the 8 back on its spot.
            match.StartCoroutine(BackOnTable(match, ball, ball.transform.position, match.MatchGeneration));
        }

        // The pocket the cue ball went in (the event doesn't say which).
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

        private IEnumerator BackOnTable(PoolMatchRules match, PoolBall ball, Vector3 pocketPoint, int generation)
        {
            yield return null;
            if (match == null || generation != match.MatchGeneration || ball == null || match.GameOver) yield break;
            int owner = OwnerOf(ball);
            SnakeChain chain = owner >= 0 ? SnakeChain.Of(owner) : null;
            if (chain == null || !chain.HeadInPlay)
            {
                // The 8, or a head in hand after a scratch.
                match.RespotBall(ball);
                if (snake.debugLogs) Debug.Log($"[SnakePool] La {ball.Number} rentrée revient à sa place.", ball);
                yield break;
            }
            ball.ResetToSpawn();
            pocketPoint.y = chain.Head.transform.position.y;
            ball.PlaceAt(pocketPoint);
            if (snake.debugLogs) Debug.Log($"[SnakePool] La {ball.Number} rentrée ressort de la poche vers la chaîne de J{owner + 1}.", ball);
            chain.Attach(ball, hop: true);
        }

        public override string DescribePlayer(int player)
        {
            SnakeChain chain = SnakeChain.Of(player);
            int length = chain != null ? chain.Length : 0;
            string colour = player == 0 ? "Serpent bleu · Pleines" : "Serpent rouge · Rayées";
            if (chain == null) return $"{colour} 0/{groupSize[player]}";
            if (chain.InFinale) return $"{colour}, la file rentre !";
            if (chain.HasEight)
                return calledPocket[player] != null
                    ? $"{colour} + la 8, rentre ta blanche : {calledPocket[player].DescribeLocation()} !"
                    : $"{colour} + la 8, annonce ta poche !";
            return IsComplete(chain) ? $"{colour} {length}/{groupSize[player]}, attrape la 8 !" : $"{colour} {length}/{groupSize[player]}";
        }

        // No chain nor call left (another mode starts, Rejouer).
        public static void Cleanup()
        {
            SnakeChain.ClearAll();
            SetCall(0, null);
            SetCall(1, null);
        }
    }
}
