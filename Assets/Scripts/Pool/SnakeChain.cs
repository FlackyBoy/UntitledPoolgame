using System;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Snake Pool: on a player's cue ball (the head), the balls it collected,
    // following its path like a snake. Links are kinematic: they push the
    // free balls aside and the opponent's cue ball bounces off them, which
    // cuts the chain there (that link and everything behind fall off). The
    // head goes through its own links.
    [DisallowMultipleComponent]
    public class SnakeChain : MonoBehaviour
    {
        public static readonly List<SnakeChain> All = new List<SnakeChain>();

        // (owner, point, new length): a ball joined the chain.
        public static event Action<int, Vector3, int> Grew;
        // (attacker, victim, point, links lost): the chain was cut.
        public static event Action<int, int, Vector3, int> Cut;

        // Which chain each linked ball is in.
        private static readonly Dictionary<PoolBall, SnakeChain> chainOf = new Dictionary<PoolBall, SnakeChain>();

        private class Link
        {
            public PoolBall ball;
            public Vector3 position;
            public Vector3 velocity;
            // Flying to its place: from, progress 0..1, hop height.
            public Vector3 joinFrom;
            public float joinT;
            public float hop;
        }

        public int Owner { get; private set; }
        public PoolBall Head { get; private set; }
        public int Length => links.Count;
        // Before this step's physics: what the head hit with.
        public Vector3 HeadVelocity { get; private set; }
        private Vector3 headSpin;
        private bool headWasInPlay;

        // Finale: the head went in a pocket with the 8 at the tail, the chain
        // pours in after it along its path. swallowed: those already in.
        private bool finale;
        private Vector3 finalePocket;
        private float finaleReel;
        private readonly List<PoolBall> swallowed = new List<PoolBall>();
        public bool InFinale => finale;
        // The 8 at the tail (it only joins a complete chain; any cut drops it).
        public bool HasEight => links.Count > 0 && links[links.Count - 1].ball.Group == BallGroup.Eight;

        private SnakePoolSettings settings;
        private SnakePoolRuleSet rules;
        private readonly List<Link> links = new List<Link>();
        // The head's past positions, oldest first, one every sampleStep.
        private readonly List<Vector3> trail = new List<Vector3>();
        private float spacing;
        private float sampleStep;
        // Pocket triggers: links never fall in (they only follow the head's path).
        private Collider[] pocketTriggers;

        public static bool IsLinked(PoolBall ball) => chainOf.ContainsKey(ball);

        public void Init(SnakePoolSettings snakeSettings, SnakePoolRuleSet ruleSet, int owner)
        {
            Clear();
            settings = snakeSettings;
            rules = ruleSet;
            Owner = owner;
            Head = GetComponent<PoolBall>();
            spacing = Head.Radius * (2f + settings.linkGap);
            sampleStep = Head.Radius * 0.25f;
            var triggers = new List<Collider>();
            foreach (PoolPocket pocket in PoolPocket.Active)
                foreach (Collider c in pocket.GetComponentsInChildren<Collider>())
                    if (c.isTrigger) triggers.Add(c);
            pocketTriggers = triggers.ToArray();
            enabled = true;
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable() => All.Remove(this);

        private void OnEnable()
        {
            if (rules != null && !All.Contains(this)) All.Add(this);
        }

        // Lets every link go, still, where it is (another mode, Rejouer: the
        // balls may already be back on their rack spots).
        public void Clear()
        {
            ReleaseFrom(0, null, Vector3.zero, keepSpeed: false);
            trail.Clear();
            // Rejouer already put the swallowed balls back.
            finale = false;
            swallowed.Clear();
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                All[i].Clear();
                All[i].enabled = false;
            }
            // Balls destroyed with their scene would stay listed.
            chainOf.Clear();
        }

        public static SnakeChain Of(int owner)
        {
            foreach (SnakeChain chain in All)
                if (chain.Owner == owner) return chain;
            return null;
        }

        // In hand after a scratch: can't collect, its links wait.
        public bool HeadInPlay => Head != null && Head.gameObject.activeInHierarchy && !Head.Rigidbody.isKinematic;

        // The ball joins the end of the chain, flying there from where it is.
        // hop: arcs up on the way (a ball coming out of a pocket).
        public void Attach(PoolBall ball, bool hop)
        {
            if (ball == null || chainOf.ContainsKey(ball)) return;
            Rigidbody body = ball.Rigidbody;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            SetIgnored(ball, true);
            chainOf[ball] = this;
            Vector3 from = ball.transform.position;
            links.Add(new Link { ball = ball, position = from, joinFrom = from, joinT = 0f, hop = hop ? settings.pocketHop * ball.Radius : 0f });
            Grew?.Invoke(Owner, from, links.Count);
            rules.OnJoined(this, ball);
        }

        // The opponent's cue ball hit this link: it and those behind fall off.
        // During the finale, the whole chain: the finale fails.
        private void CutAt(int index, SnakeChain attacker, Vector3 attackVelocity)
        {
            if (finale) index = 0;
            int lost = links.Count - index;
            Vector3 point = links[index].position;
            ReleaseFrom(index, links[index].ball, attackVelocity * settings.breakKick);
            if (settings.debugLogs)
                Debug.Log($"[SnakePool] J{attacker.Owner + 1} brise la chaîne de J{Owner + 1} ({attackVelocity.magnitude:0.00} m/s) : {lost} bille(s) lâchée(s), il en reste {links.Count}" +
                          $"{(finale ? $", finale ratée ({swallowed.Count} déjà rentrée(s) ressortent)" : "")}.", this);
            if (finale) FailFinale();
            rules.OnCut(this);
            Cut?.Invoke(attacker.Owner, Owner, point, lost);
        }

        // The head went in this pocket with the 8 in tow.
        public void BeginFinale(Vector3 pocketCenter)
        {
            finale = true;
            finaleReel = 0f;
            swallowed.Clear();
            finalePocket = pocketCenter;
            finalePocket.y = trail.Count > 0 ? trail[trail.Count - 1].y : Head.transform.position.y;
            trail.Add(finalePocket);
        }

        // Cut while pouring in: those already in come back to their rack spots.
        private void FailFinale()
        {
            finale = false;
            PoolMatchRules match = PoolMatchRules.Instance;
            foreach (PoolBall ball in swallowed)
                if (ball != null && match != null) match.RespotBall(ball);
            swallowed.Clear();
        }

        // Each link goes on along the path at Finale Speed and goes in when it
        // reaches the pocket; the last one in wins.
        private void PourIntoPocket(float dt)
        {
            finaleReel += settings.finaleSpeed * dt;
            for (int i = 0; i < links.Count; i++)
            {
                Link link = links[i];
                float distance = spacing * (i + 1 + swallowed.Count) - finaleReel;
                if (distance <= 0f)
                {
                    links.RemoveAt(i--);
                    chainOf.Remove(link.ball);
                    SetIgnored(link.ball, false);
                    link.ball.gameObject.SetActive(false);
                    swallowed.Add(link.ball);
                    continue;
                }
                Vector3 target = PointBehind(finalePocket, distance);
                target.y = finalePocket.y;
                // Sinks over its last spacing.
                if (distance < spacing) target.y -= link.ball.Radius * 2f * (1f - distance / spacing);
                MoveLink(link, Blend(link, target, dt), dt);
            }
            if (links.Count > 0) return;
            finale = false;
            rules.OnFinaleDone(this, swallowed.Count);
        }

        // A link still flying to its place (just joined, or the head came back).
        private Vector3 Blend(Link link, Vector3 target, float dt)
        {
            if (link.joinT >= 1f) return target;
            link.joinT = Mathf.Min(1f, link.joinT + dt / settings.joinTime);
            float k = link.joinT * link.joinT * (3f - 2f * link.joinT);
            return Vector3.Lerp(link.joinFrom, target, k) + Vector3.up * (link.hop * Mathf.Sin(Mathf.PI * link.joinT));
        }

        // Every link from index on goes back to being a free ball, keeping its
        // speed; kicked gets the hit's velocity on top.
        public void ReleaseFrom(int index, PoolBall kicked, Vector3 kick, bool keepSpeed = true)
        {
            for (int i = links.Count - 1; i >= index; i--)
            {
                Link link = links[i];
                links.RemoveAt(i);
                chainOf.Remove(link.ball);
                if (link.ball == null) continue;
                SetIgnored(link.ball, false);
                Rigidbody body = link.ball.Rigidbody;
                body.isKinematic = false;
                Vector3 v = keepSpeed ? link.velocity * settings.releasedSpeed : Vector3.zero;
                if (link.ball == kicked) v += kick;
                v.y = 0f;
                body.linearVelocity = v;
                // Already rolling, not sliding: v = w x r.
                body.angularVelocity = Vector3.Cross(Vector3.up, v) / link.ball.Radius;
            }
        }

        private void SetIgnored(PoolBall ball, bool ignore)
        {
            if (Head != null) Physics.IgnoreCollision(ball.BodyCollider, Head.BodyCollider, ignore);
            if (pocketTriggers == null) return;
            foreach (Collider trigger in pocketTriggers)
                if (trigger != null) Physics.IgnoreCollision(ball.BodyCollider, trigger, ignore);
        }

        private void FixedUpdate()
        {
            if (finale)
            {
                PourIntoPocket(Time.fixedDeltaTime);
                return;
            }
            if (!HeadInPlay)
            {
                headWasInPlay = false;
                return;
            }
            HeadVelocity = Head.Rigidbody.linearVelocity;
            headSpin = Head.Rigidbody.angularVelocity;
            Vector3 head = Head.Rigidbody.position;

            // Back from a scratch, elsewhere: a new path, the links fly to it.
            if (!headWasInPlay)
            {
                headWasInPlay = true;
                trail.Clear();
                foreach (Link link in links) { link.joinFrom = link.position; link.joinT = 0f; }
            }
            if (trail.Count == 0 || (head - trail[trail.Count - 1]).sqrMagnitude >= sampleStep * sampleStep) trail.Add(head);
            PruneTrail(head);

            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < links.Count; i++)
            {
                Link link = links[i];
                Vector3 target = PointBehind(head, spacing * (i + 1));
                target.y = head.y;
                MoveLink(link, Blend(link, target, dt), dt);
            }
        }

        private static void MoveLink(Link link, Vector3 target, float dt)
        {
            Vector3 delta = target - link.position;
            link.velocity = delta / dt;
            link.position = target;
            Rigidbody body = link.ball.Rigidbody;
            body.MovePosition(target);
            // Rolls along the ground.
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float distance = flat.magnitude;
            if (distance > 0.00001f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, flat / distance);
                body.MoveRotation(Quaternion.AngleAxis(distance / link.ball.Radius * Mathf.Rad2Deg, axis) * body.rotation);
            }
        }

        // The point at this distance back along the path; past its end, on
        // along its last direction (a fresh, short path), kept on the felt.
        private Vector3 PointBehind(Vector3 head, float distance)
        {
            Vector3 from = head;
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                Vector3 to = trail[i];
                float step = Vector3.Distance(from, to);
                if (step >= distance && step > 0f) return Vector3.Lerp(from, to, distance / step);
                distance -= step;
                from = to;
            }
            Vector3 dir = Vector3.zero;
            if (trail.Count >= 2) dir = trail[0] - trail[1];
            else if (from != head) dir = from - head;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.000001f)
            {
                // No path yet: opposite its motion, or toward the nearest end
                // of the table (along its length).
                dir = -HeadVelocity;
                dir.y = 0f;
                PoolTableSurface table = PoolTableSurface.Instance;
                if (dir.sqrMagnitude < 0.0001f && table != null)
                {
                    Vector3 along = table.transform.right;
                    dir = Vector3.Dot(head - table.transform.position, along) >= 0f ? along : -along;
                }
                if (dir.sqrMagnitude < 0.0001f) dir = Vector3.back;
            }
            Vector3 point = from + dir.normalized * distance;
            PoolTableSurface surface = PoolTableSurface.Instance;
            return surface != null ? surface.ClampToPlayArea(point, Head.Radius) : point;
        }

        // Drops the path older than the chain needs.
        private void PruneTrail(Vector3 head)
        {
            float needed = spacing * (links.Count + 1) + sampleStep * 2f;
            float length = 0f;
            Vector3 from = head;
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                length += Vector3.Distance(from, trail[i]);
                from = trail[i];
                if (length > needed)
                {
                    if (i > 0) trail.RemoveRange(0, i);
                    return;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (rules == null || !HeadInPlay) return;
            if (!collision.gameObject.TryGetComponent(out PoolBall other) || other.IsCueBall) return;

            // The opponent's chain: cut it, if hit hard enough.
            if (chainOf.TryGetValue(other, out SnakeChain victim))
            {
                if (victim == this || HeadVelocity.magnitude < settings.minBreakSpeed) return;
                int index = victim.IndexOf(other);
                if (index >= 0) victim.CutAt(index, this, HeadVelocity);
                return;
            }

            // Its balls; the 8 once they're all in the chain (before, it bounces).
            bool wanted = other.Group == BallGroup.Eight ? rules.IsComplete(this) : other.Group == rules.GroupOf(Owner);
            if (!wanted) return;
            // Eats it without losing speed: the snake goes on.
            if (settings.keepSpeedOnCollect)
            {
                Head.Rigidbody.linearVelocity = HeadVelocity;
                Head.Rigidbody.angularVelocity = headSpin;
            }
            Attach(other, hop: false);
        }

        private int IndexOf(PoolBall ball)
        {
            for (int i = 0; i < links.Count; i++)
                if (links[i].ball == ball) return i;
            return -1;
        }
    }
}
