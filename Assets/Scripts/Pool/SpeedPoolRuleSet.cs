using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // 8-ball in Speed Pool, and the base of the modes where each player has
    // their own cue ball (Hungry Hole, Snake Pool, Power Wash Pool). P1 (blue
    // cue ball) has the solids, P2 (red) the stripes. Up to four
    // (PlayerBallGroups): at three 1-5, 6-11 and 12-15, at four 2 against 2
    // or a second rack for J3 and J4. Once their group is empty, the first
    // to pot the 8 wins; potting it before loses (at three or four each for
    // themselves: that player is out, the 8 comes back, the last one left
    // wins). Sinking someone else's ball only helps them. Each pot is judged as it
    // happens (HandlePocketed), whatever the match type. In Speed Pool no
    // shot is ever resolved, the table is never still for long. Turn based
    // (the subclasses), ResolveShot only passes the turn: the shooter plays
    // again if one of their balls scored and their cue ball stayed on the
    // table. Scratch: the cue ball is out (Cue Respawn Delay, or until the
    // shot is over in turns) and the player's last potted ball comes back.
    public class SpeedPoolRuleSet : PoolRuleSet
    {
        // Each side's potted balls (PlayerBallGroups.OwnerOf), last one on
        // top: a scratch brings it back.
        protected readonly List<PoolBall>[] pottedByPlayer =
            { new List<PoolBall>(), new List<PoolBall>(), new List<PoolBall>(), new List<PoolBall>() };
        private SpeedPoolSettings settings;
        // Turn based: whose balls scored during the current shot.
        private readonly bool[] scoredThisShot = new bool[PoolMatchRules.MaxPlayers];
        private readonly List<PoolBall> cueBalls = new List<PoolBall>();

        // The balls are shared out per player: someone joining restarts the match.
        public override bool SharesBallsOut => true;

        // The 8 ends the match: 8-ball and Catch the Hole in Speed Pool, this
        // class itself. The modes built on it have their own rules for it.
        private bool EightBallFinishes => GetType() == typeof(SpeedPoolRuleSet);

        // Out of the match: potted the 8 too early at three or four.
        private readonly bool[] isOut = new bool[PoolMatchRules.MaxPlayers];
        // Potted too early: back on the table next frame (OnPocketed turns it
        // off just after HandlePocketed).
        private PoolBall eightToRespot;
        // Each owner's pattern on their balls (BallOwnerMarker).
        private bool ShowsOwnerMarkers => EightBallFinishes;

        // A potted cue ball comes back on its own spot (PoolMatchRules.RespawnCueBall).
        // False when the mode brings it back itself.
        public virtual bool RespawnsPottedCueBall => true;

        public override void Setup(PoolMatchRules match)
        {
            settings = PoolSettingsLoader.LoadOrDefault<SpeedPoolSettings>("SpeedPoolSettings");

            System.Array.Clear(isOut, 0, isOut.Length);
            eightToRespot = null;
            int players = PlayerBallGroups.Players;
            if (!FindPlayerCueBalls(players, cueBalls)) return;

            // Side by side on the head string, across the table's width, J1
            // on one side and the last player on the other.
            Vector3 spot = cueBalls[0].SpawnPosition;
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 across = surface != null ? surface.transform.rotation * Vector3.forward : Vector3.forward;
            float offset = (surface != null ? surface.HalfWidth : 0.3f) * settings.cueSpread;
            for (int p = 0; p < cueBalls.Count; p++)
            {
                float t = cueBalls.Count == 1 ? 0f : Mathf.Lerp(-1f, 1f, p / (float)(cueBalls.Count - 1));
                cueBalls[p].MakePlayerCueBall(p, spot + across * (offset * t), settings.PlayerCueColor(p));
            }

            SetupSecondRack();
            SetupExtraBalls();
            MixRack();
            if (ShowsOwnerMarkers && players >= settings.ownerMarkersFrom) AddOwnerMarkers();
            if (ShowsOwnerMarkers && players >= settings.ownBallHaloFrom) AddOwnBallHalos();

            if (match.TraceShots)
                Debug.Log($"[SpeedPool] Départ à {players} ({PlayerBallGroups.Split}{(PlayerBallGroups.Teams ? ", en équipes" : "")}) : " +
                          $"{CountGroup(BallGroup.Solid)} pleines, {CountGroup(BallGroup.Stripe)} rayées, {PoolBall.Active.Count} billes actives.");
        }

        // A match without a cue ball each comes next: the second player's
        // cue ball and the extra balls go away (inactive first, or the next
        // rule set's Setup, this same frame, would still count them), the
        // first cue ball is the shared one again.
        public static void Cleanup()
        {
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                if (ball.IsExtra || ball.InSecondRack || ball.CueOwner >= 1)
                {
                    ball.gameObject.SetActive(false);
                    Object.Destroy(ball.gameObject);
                }
                else if (ball.CueOwner == 0) ball.MakeSharedCueBall();
            }
        }

        // The table's cue ball, and the second player's: a clone made on the
        // first match, kept by "Rejouer" (which resets it to its own spawn
        // spot). False if the table has no cue ball.
        protected static bool FindPlayerCueBalls(out PoolBall first, out PoolBall second)
        {
            first = second = null;
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (!ball.IsCueBall) continue;
                if (ball.CueOwner == 1) second = ball;
                else if (first == null) first = ball;
            }
            if (first == null)
            {
                Debug.LogWarning("[SpeedPool] Pas de bille blanche sur la table.");
                return false;
            }
            if (second == null)
            {
                second = Object.Instantiate(first, first.transform.parent);
                second.name = "CueBall_P2 (Speed Pool)";
            }
            return true;
        }

        // Before every match, once the balls are shared out (PlayerBallGroups):
        // the cue balls of players no longer there and an unwanted second rack
        // go away. Here rather than in Setup: a mode that sets up its own
        // balls (Play Anywhere, Shooter...) would leave them on the table.
        public static void FitToPlayers(int players)
        {
            BallOwnerMarker.RemoveAll();
            OwnBallHalo.RemoveAll();
            bool secondRack = PlayerBallGroups.Split == GroupSplit.TwoRacks;
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                if (ball.CueOwner < players && (secondRack || !ball.InSecondRack))
                {
                    ball.BackFromMatchSpot();
                    continue;
                }
                // Inactive first: this frame's Setup would still find it.
                ball.gameObject.SetActive(false);
                Object.Destroy(ball.gameObject);
            }
        }

        // One cue ball per player, J1's the table's own: the others are
        // clones made on the first match and kept by "Rejouer"; those of
        // players no longer there go away. False if the table has no cue ball.
        protected static bool FindPlayerCueBalls(int count, List<PoolBall> cues)
        {
            cues.Clear();
            PoolBall first = null;
            var owned = new PoolBall[PoolMatchRules.MaxPlayers];
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (!ball.IsCueBall) continue;
                if (ball.CueOwner >= 1 && ball.CueOwner < owned.Length) owned[ball.CueOwner] = ball;
                else if (first == null) first = ball;
            }
            if (first == null)
            {
                Debug.LogWarning("[SpeedPool] Pas de bille blanche sur la table.");
                return false;
            }
            cues.Add(first);
            // Those of players no longer there went in FitToPlayers.
            for (int p = 1; p < count && p < owned.Length; p++)
            {
                PoolBall cue = owned[p];
                if (cue == null)
                {
                    cue = Object.Instantiate(first, first.transform.parent);
                    cue.name = $"CueBall_P{p + 1} (Speed Pool)";
                }
                cues.Add(cue);
            }
            return true;
        }

        // Turn based only (Speed Pool never resolves a shot, see
        // PoolMatchRules.Update). Called when a shot is struck.
        public void BeginShot()
        {
            System.Array.Clear(scoredThisShot, 0, scoredThisShot.Length);
        }

        // One of the player's balls did what the mode wants (potted, swallowed,
        // joined the chain): in turns, they play again.
        protected void MarkScored(int player)
        {
            if (player >= 0) scoredThisShot[player] = true;
        }

        // Turn based only. cueBallPocketed: the shooter's own cue ball.
        public override void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
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
                if (settings.respotOwnBallOnScratch && ball.CueOwner >= 0) RespotLastPotted(match, PlayerBallGroups.SideOf(ball.CueOwner));
                return;
            }
            if (ball.Group == BallGroup.Eight && EightBallFinishes)
            {
                HandleEightBall(match, ball);
                return;
            }
            int owner = OwnerOf(ball);
            if (owner >= 0) pottedByPlayer[owner].Add(ball);
            MarkScored(owner);
            bool cleared = owner >= 0 && IsSideCleared(owner, ball);
            if (match.TraceShots)
                Debug.Log($"[SpeedPool] Bille {ball.Number} rentrée à {ball.transform.position} (bille de J{owner + 1}" +
                          $"{(PlayerBallGroups.Teams ? " et J" + (owner + 3) : "")}){(cleared ? (EightBallFinishes ? " → groupe vidé, place à la 8" : " → victoire") : "")}.", ball);
            if (cleared && !EightBallFinishes) match.Win(owner);
        }

        // The 8: whoever's shot sent it in (ball.LastShooter) wins with their
        // group empty, loses before. Nobody's shot (a power, a nudge): back.
        private void HandleEightBall(PoolMatchRules match, PoolBall ball)
        {
            int shooter = ball.LastShooter;
            if (shooter < 0 || shooter >= PlayerBallGroups.Players || isOut[shooter])
            {
                eightToRespot = ball;
                return;
            }
            int side = PlayerBallGroups.SideOf(shooter);
            if (IsSideCleared(side, null))
            {
                if (match.TraceShots) Debug.Log($"[SpeedPool] J{shooter + 1} rentre la 8 après son groupe → victoire.", ball);
                match.Win(side);
                return;
            }
            // Two sides (two players, two teams): the other side wins.
            if (PlayerBallGroups.Sides == 2)
            {
                if (match.TraceShots) Debug.Log($"[SpeedPool] J{shooter + 1} rentre la 8 trop tôt → défaite.", ball);
                match.Win(1 - side);
                return;
            }
            // Each for themselves at three or four: out, the others go on.
            isOut[shooter] = true;
            eightToRespot = ball;
            int left = -1, count = 0;
            for (int p = 0; p < PlayerBallGroups.Players; p++)
                if (!isOut[p]) { left = p; count++; }
            if (match.TraceShots) Debug.Log($"[SpeedPool] J{shooter + 1} rentre la 8 trop tôt → éliminé, {count} en jeu.", ball);
            if (count == 1) match.Win(left);
        }

        public override void Tick(PoolMatchRules match)
        {
            if (eightToRespot == null) return;
            match.RespotBall(eightToRespot);
            eightToRespot = null;
        }

        // Potted the 8 too early, at three or four each for themselves.
        public bool IsEliminated(int player) => player >= 0 && player < isOut.Length && isOut[player];

        public override bool AllowsShot(PoolMatchRules match, int playerIndex) => !IsEliminated(playerIndex);

        // 8-ball's own rules (the 8 to finish, eliminations, patterns), not
        // the modes built on Speed Pool.
        public bool IsEightBall => EightBallFinishes;

        // The pattern a side's balls wear (BallOwnerMarker); false when they
        // keep their own solids and stripes. The HUD's plate draws the same.
        public bool TryGetPattern(int side, out BallOwnerMarker.Pattern pattern)
        {
            pattern = settings != null ? settings.PatternOf(side) : BallOwnerMarker.Pattern.Solid;
            return settings != null && side >= 0 && ShowsOwnerMarkers && PlayerBallGroups.Players >= settings.ownerMarkersFrom;
        }

        // The player's group is empty: the 8 is theirs to pot.
        public bool IsGoingForEight(int player) =>
            EightBallFinishes && player >= 0 && IsSideCleared(PlayerBallGroups.SideOf(player), null);

        // Every ball with an owner takes its owner's pattern; the last
        // match's went in FitToPlayers.
        private void AddOwnerMarkers()
        {
            foreach (PoolBall ball in PoolBall.Active)
            {
                int side = OwnerOf(ball);
                if (side < 0) continue;
                BallOwnerMarker.Mark(ball, settings.PatternOf(side));
            }
        }

        // Each player's balls ringed in their colour, in their own view only.
        private void AddOwnBallHalos()
        {
            var colours = new Color[PoolMatchRules.MaxPlayers];
            for (int p = 0; p < colours.Length; p++) colours[p] = settings.PlayerCueColor(p);
            foreach (PoolBall ball in PoolBall.Active)
            {
                int side = OwnerOf(ball);
                if (side >= 0) OwnBallHalo.Add(ball, side, settings.ownBallHaloSize, colours);
            }
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

        // Four each for themselves: a copy of every solid and stripe, in rows
        // behind the rack, tinted. Kept by "Rejouer" (back at their own spawn),
        // only the missing ones are made; gone in any other split (FitToPlayers).
        private void SetupSecondRack()
        {
            if (PlayerBallGroups.Split != GroupSplit.TwoRacks) return;
            var have = new HashSet<int>();
            foreach (PoolBall ball in PoolBall.Active)
                if (ball.InSecondRack && !ball.IsExtra) have.Add(ball.Number);

            var models = new List<PoolBall>();
            foreach (PoolBall ball in PoolBall.Active)
                if (!ball.IsCueBall && !ball.IsExtra && !ball.InSecondRack && OwnerOf(ball) >= 0 && !have.Contains(ball.Number))
                    models.Add(ball);
            if (models.Count == 0) return;
            // 1, 9, 2, 10...: the rows mix solids and stripes like the rack.
            models.Sort((a, b) => a.Number % 8 != b.Number % 8 ? (a.Number % 8).CompareTo(b.Number % 8) : a.Number.CompareTo(b.Number));
            // Worked out before any copy exists (see SetupExtraBalls).
            Vector3 rackCenter = RackCenter();
            var spots = new List<Vector3>();
            IEnumerator<Vector3> rows = RackExtensionSpots();
            while (spots.Count < models.Count && rows.MoveNext()) spots.Add(rows.Current);
            // Two racks means four players: the owners' patterns already tell
            // the copies apart, the tint is only for when they're turned off.
            Color tint = ShowsOwnerMarkers && settings.ownerMarkersFrom <= 4 ? Color.white : settings.secondRackTint;
            for (int i = 0; i < models.Count; i++)
            {
                PoolBall copy = Object.Instantiate(models[i], models[i].transform.parent);
                copy.name = $"{models[i].name} (second triangle)";
                copy.MarkSecondRack(i < spots.Count ? spots[i] : PoolMatchRules.FindFreeSpot(rackCenter, copy), tint);
            }
        }

        // Copies of each group's balls up to Extra Balls Per Player. Kept by
        // "Rejouer" (back at their own spawn), only the missing ones are made.
        private void SetupExtraBalls()
        {
            int sides = PlayerBallGroups.Sides;
            var originals = new List<PoolBall>[sides];
            var extras = new List<PoolBall>[sides];
            for (int p = 0; p < sides; p++)
            {
                originals[p] = new List<PoolBall>();
                extras[p] = new List<PoolBall>();
            }
            foreach (PoolBall ball in PoolBall.Active)
            {
                int owner = OwnerOf(ball);
                if (owner >= 0) (ball.IsExtra ? extras : originals)[owner].Add(ball);
            }

            int wanted = settings.extraBallsPerPlayer;
            int missing = 0, most = wanted;
            for (int p = 0; p < sides; p++)
            {
                originals[p].Sort((a, b) => a.Number.CompareTo(b.Number));
                missing += Mathf.Max(0, wanted - extras[p].Count);
                most = Mathf.Max(most, extras[p].Count);
            }
            // Worked out before any copy exists: a new copy sits on its model
            // until placed, and would count as part of the rack.
            Vector3 rackCenter = RackCenter();
            var spots = new List<Vector3>();
            IEnumerator<Vector3> rows = RackExtensionSpots();
            while (spots.Count < missing && rows.MoveNext()) spots.Add(rows.Current);
            int nextSpot = 0;
            for (int i = 0; i < most; i++)
            {
                // Alternated, so the added rows mix the groups like the rack.
                for (int p = 0; p < sides; p++)
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
                    // Already tinted like its model: J3's or J4's too.
                    if (model.InSecondRack) extra.MarkSecondRack(extra.SpawnPosition, Color.white);
                }
            }
        }

        // At three or four, the rack as made puts each player's balls in a
        // block (the second rack behind the first, 12-15 together): the owned
        // balls swap spots so each player has some from the front of the
        // triangle to its back, in a new order each match. The 8 stays put.
        private void MixRack()
        {
            int sides = PlayerBallGroups.Sides;
            if (sides <= 2) return;
            var bySide = new List<PoolBall>[sides];
            for (int s = 0; s < sides; s++) bySide[s] = new List<PoolBall>();
            var spots = new List<Vector3>();
            foreach (PoolBall ball in PoolBall.Active)
            {
                int side = OwnerOf(ball);
                if (side < 0) continue;
                bySide[side].Add(ball);
                spots.Add(ball.SpawnPosition);
            }
            if (spots.Count < 2) return;

            // Front (the apex, towards the table's middle) to back, then across.
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 middle = surface != null ? surface.transform.position : Vector3.zero;
            Vector3 along = RackCenter() - middle;
            along.y = 0f;
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.right;
            Vector3 across = Vector3.Cross(Vector3.up, along);
            spots.Sort((a, b) =>
            {
                float da = Vector3.Dot(a - middle, along), db = Vector3.Dot(b - middle, along);
                return Mathf.Abs(da - db) > 0.01f ? da.CompareTo(db) : Vector3.Dot(a, across).CompareTo(Vector3.Dot(b, across));
            });

            // Dealt one each in turn, players and balls in a random order.
            var order = new int[sides];
            for (int s = 0; s < sides; s++)
            {
                order[s] = s;
                Shuffle(bySide[s]);
            }
            for (int s = sides - 1; s > 0; s--)
            {
                int k = Random.Range(0, s + 1);
                (order[s], order[k]) = (order[k], order[s]);
            }
            int next = 0;
            for (int round = 0; next < spots.Count; round++)
                foreach (int s in order)
                    if (round < bySide[s].Count) bySide[s][round].MoveForMatch(spots[next++]);
        }

        private static void Shuffle(List<PoolBall> balls)
        {
            for (int i = balls.Count - 1; i > 0; i--)
            {
                int k = Random.Range(0, i + 1);
                (balls[i], balls[k]) = (balls[k], balls[i]);
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

        // Side whose group the ball is in (a player, or a team's first
        // player), -1 for none (the 8 outside of three players, a cue ball).
        public static int OwnerOf(PoolBall ball) => PlayerBallGroups.OwnerOf(ball);

        // Solid or Stripe; Cue at three (numbers in a row) and for the
        // players without balls (Sumo Pool).
        public override BallGroup GroupOf(int player) =>
            player >= 0 && player < PlayerBallGroups.Players ? PlayerBallGroups.GroupOfSide(PlayerBallGroups.SideOf(player)) : BallGroup.Cue;

        protected static bool IsSideCleared(int side, PoolBall leaving)
        {
            foreach (PoolBall ball in PoolBall.Active)
                if (ball != leaving && OwnerOf(ball) == side) return false;
            return true;
        }

        // leaving: the ball being potted, not off the table yet.
        protected static bool IsGroupCleared(BallGroup group, PoolBall leaving)
        {
            foreach (PoolBall ball in PoolBall.Active)
                if (ball != leaving && ball.Group == group) return false;
            return true;
        }

        private static readonly string[] CueNames = { "Blanche bleue", "Blanche rouge", "Blanche verte", "Blanche violette" };

        public override string DescribePlayer(int player)
        {
            int side = PlayerBallGroups.SideOf(player);
            int[] numbers = PlayerBallGroups.Numbers(side);
            string balls = PlayerBallGroups.Split == GroupSplit.Thirds ? "Billes " + string.Join(", ", numbers)
                : (side % 2 == 0 ? "Pleines" : "Rayées") + (side >= 2 ? " foncées" : "") + $" ({numbers[0]}-{numbers[numbers.Length - 1]})";
            string mate = PlayerBallGroups.Teams ? $" · avec J{PlayerBallGroups.TeammateOf(player) + 1}" : "";
            return $"{CueNames[Mathf.Clamp(player, 0, CueNames.Length - 1)]} · {balls}{mate}";
        }
    }
}
