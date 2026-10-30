using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere: golf in the décor. Each player has their own cue ball
    // (SpeedPoolRuleSet), set on their tee of the scene's PlayAnywhereHole;
    // the object balls sit out. Every stroke counts, the ball must end in
    // the cup (slowly enough), the lowest total wins (a tie goes to whoever
    // holed out first). Out of bounds (a PlayAnywhereOutOfBounds zone, a
    // pocket of the table, a fall under the course) costs a penalty and the
    // ball goes back to where it last stood still. Reaching Max Strokes ends
    // the hole for that player. The hole is played in rounds of placing and
    // shooting (PlayAnywhereRuleSet.Rounds.cs), the match in several holes
    // with a score card between them (PlayAnywhereRuleSet.Course.cs). In turns, the player
    // farthest from the cup plays next, as in golf; in Speed Pool, both
    // play whenever they like. Surfaces: this file, after the cup.
    public partial class PlayAnywhereRuleSet : SpeedPoolRuleSet
    {
        // Set while a Play Anywhere match is on (PoolMatchRules asks).
        public static PlayAnywhereRuleSet Current { get; private set; }

        // The level has a course to play (Boogie Night's wheel leaves the
        // mode out otherwise).
        public static bool HasCourse => Object.FindAnyObjectByType<PlayAnywhereHole>() != null;

        private PlayAnywhereSettings settings;
        private PlayAnywhereHole hole;
        private readonly PoolBall[] balls = new PoolBall[2];
        private readonly int[] strokes = new int[2];
        private readonly bool[] finished = new bool[2];
        // Where each ball last stood still: back there when out of bounds
        // (struck, or knocked by the other ball).
        private readonly Vector3[] lastRest = new Vector3[2];
        // Speed Pool: the stroke under way, until the ball stops (Tick).
        private readonly bool[] rolling = new bool[2];
        private readonly float[] struckAt = new float[2];
        // Who finished first, the tie-break.
        private int firstFinisher = -1;

        // The ball reads as still for a frame or two after the strike.
        private const float MinStrokeTime = 0.3f;
        // Falling off a ledge isn't standing still.
        private const float StillVerticalSpeed = 0.05f;
        // How far the ball's bottom may be from the cup's disc, up or down.
        private const float CupHeightTolerance = 0.05f;
        // Diagnostics: passes within this distance of the cup's edge are logged.
        private const float CupTraceRange = 0.3f;
        // Per player, closest approach of the current pass: distance, height, speed.
        private readonly Vector3[] closestPass = { Vector3.positiveInfinity, Vector3.positiveInfinity };
        // Per player, the lip was already hit on this pass over the cup.
        private readonly bool[] lipped = new bool[2];
        // Per player, the ground last rolled on (null = the felt).
        private readonly PlayAnywhereSurfaceType[] ground = new PlayAnywhereSurfaceType[2];
        // Each collider's ground, looked up once (null = the felt).
        private readonly Dictionary<Collider, PlayAnywhereSurfaceType> groundOf = new Dictionary<Collider, PlayAnywhereSurfaceType>();
        // Every surface type (Resources/PlayAnywhereSurfaces), and which
        // one each listed material is.
        private PlayAnywhereSurfaceType[] surfaceTypes;
        private readonly Dictionary<Material, PlayAnywhereSurfaceType> typeOfMaterial = new Dictionary<Material, PlayAnywhereSurfaceType>();
        private readonly List<Material> materialBuffer = new List<Material>();
        // The pool table (rails, felt...): keeps the felt's friction.
        private Transform tableRoot;
        // Décor colliders given a surface's bounce, and what they had
        // (Cleanup puts it back).
        private static readonly List<Collider> bouncedColliders = new List<Collider>();
        private static readonly List<PhysicsMaterial> originalMaterials = new List<PhysicsMaterial>();
        // Below the ball's bottom, the ground still counts as under it.
        private const float GroundProbe = 0.03f;

        public float ShotPowerScale => settings != null ? settings.shotPowerMultiplier : 1f;
        public int StrokesOf(int player) => strokes[player];
        public int Par => hole != null ? hole.Par : 0;
        public PlayAnywhereSettings Settings => settings;

        // The ball a placed gadget (PlayAnywhereGadget) may act on: on the
        // course, not holed out, not parked. null otherwise.
        public PoolBall PlayableBall(int player)
        {
            if (hole == null || phase == Phase.Intermission || player < 0 || player >= 2 || finished[player]) return null;
            PoolBall ball = balls[player];
            return ball != null && !ball.Rigidbody.isKinematic ? ball : null;
        }

        // A gadget destroyed the ball (the portal): as if it left the course.
        public void SendOutOfBounds(int player, string where)
        {
            if (match == null || match.GameOver || PlayableBall(player) == null) return;
            OutOfBounds(match, player, where);
        }

        public override void Setup(PoolMatchRules match)
        {
            settings = PoolSettingsLoader.LoadOrDefault<PlayAnywhereSettings>("PlayAnywhereSettings");
            AssignSurfaces();
            Current = this;
            PlayAnywhereSpawnMenu.Ensure();
            PlayAnywhereScoreboard.Hide();
            BuildCourse();
            hole = course.Count > 0 ? course[0] : null;
            ShowOnly(hole);

            // Copy: deactivating a ball removes it from Active.
            foreach (PoolBall ball in new List<PoolBall>(PoolBall.Active))
                if (!ball.IsCueBall) ball.gameObject.SetActive(false);
            if (!FindPlayerCueBalls(out balls[0], out balls[1])) return;

            SpeedPoolSettings colours = PoolSettingsLoader.LoadOrDefault<SpeedPoolSettings>("SpeedPoolSettings");
            for (int p = 0; p < 2; p++)
            {
                PoolBall ball = balls[p];
                Vector3 tee = hole != null ? hole.TeePosition(p, ball.Radius) : ball.SpawnPosition;
                ball.MakePlayerCueBall(p, tee, p == 0 ? colours.player1CueColor : colours.player2CueColor);
                // Off the felt is the whole point: the mode judges out of bounds.
                ball.FreeRoaming = true;
            }
            ResetHoleState();

            if (hole == null)
            {
                Debug.LogWarning("[PlayAnywhere] Pas de trou (PlayAnywhereHole) dans la scène : les billes restent sur la table. Tools > Pool > Play Anywhere > Créer un trou.");
                return;
            }
            if (settings.debugLogs)
                Debug.Log($"[PlayAnywhere] Départ : trou '{hole.name}' par {hole.Par}, J1 {balls[0].transform.position}, J2 {balls[1].transform.position}.", hole);
            CheckTees();
            StartRounds(match);
        }

        // A new hole: nothing played yet, the balls on their tees.
        private void ResetHoleState()
        {
            for (int p = 0; p < 2; p++)
            {
                strokes[p] = 0;
                finished[p] = false;
                rolling[p] = false;
                lipped[p] = false;
                closestPass[p] = Vector3.positiveInfinity;
                if (balls[p] == null) continue;
                lastRest[p] = balls[p].transform.position;
                // The felt until the first frame finds the ground.
                ground[p] = null;
                ApplyGround(balls[p], null);
            }
            firstFinisher = -1;
        }

        // Called by PoolMatchRules.NotifyShotFired, before the ball moves.
        public void OnStroke(PoolBall ball)
        {
            int p = PlayerOf(ball);
            if (p < 0 || finished[p]) return;
            strokes[p]++;
            roundStrokes[p]++;
            lastRest[p] = ball.transform.position;
            rolling[p] = true;
            struckAt[p] = Time.time;
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] J{p + 1} : coup {strokes[p]} depuis {lastRest[p]}.", ball);
        }

        // Every frame from PoolMatchRules.Update: the cup, out of bounds, in
        // Speed Pool the end of each stroke, and the round's phase.
        public void Tick(PoolMatchRules match)
        {
            if (hole == null || match.GameOver || phase == Phase.Intermission) return;
            for (int p = 0; p < 2; p++)
            {
                PoolBall ball = balls[p];
                if (ball == null || finished[p] || ball.Rigidbody.isKinematic) continue;
                Vector3 pos = ball.transform.position;
                bool grounded = UpdateGround(p, ball, pos);

                if (IsInCup(ball, pos, p))
                {
                    HoleOut(match, p);
                    continue;
                }
                if (pos.y < hole.LowestY - settings.outOfBoundsDepth || !hole.InPlayArea(pos) || PlayAnywhereOutOfBounds.AnyContains(pos))
                {
                    OutOfBounds(match, p, "hors limites");
                    continue;
                }
                bool still = !ball.IsRolling && Mathf.Abs(ball.Rigidbody.linearVelocity.y) < StillVerticalSpeed;
                if (!still) continue;
                // On the ground only: the first frame of a fall reads as still too.
                if (grounded) lastRest[p] = pos;
                if (match.IsSimultaneous && rolling[p] && Time.time - struckAt[p] >= MinStrokeTime)
                {
                    rolling[p] = false;
                    CheckStrokeLimit(match, p);
                }
            }
            if (match.GameOver) return;
            TickAwaitingPlayers();
            if (phase == Phase.Build) TickBuild();
            else if (match.IsSimultaneous) TickShootSimultaneous();
        }

        // The friction of what the ball rolls on. In the air it keeps the
        // last ground's. Returns whether there is ground under the ball.
        private bool UpdateGround(int p, PoolBall ball, Vector3 pos)
        {
            if (!Physics.Raycast(pos, Vector3.down, out RaycastHit hit, ball.Radius + GroundProbe, ~0, QueryTriggerInteraction.Ignore))
                return false;
            // Static décor was sorted at the start; anything else (spawned
            // since, moving objects) is looked up once.
            if (!groundOf.TryGetValue(hit.collider, out PlayAnywhereSurfaceType type))
            {
                type = FindSurface(hit.collider);
                groundOf[hit.collider] = type;
            }
            if (type == ground[p]) return true;
            ground[p] = type;
            ApplyGround(ball, type);
            if (settings.debugLogs)
                Debug.Log($"[PlayAnywhere] J{p + 1} roule sur {(type != null ? type.name : "le tapis du billard")} ({hit.collider.name}).", hit.collider);
            return true;
        }

        // Once, at the start: every static décor collider gets its surface,
        // for the friction under the ball (groundOf) and the bounce off it
        // (the type's physics material). Objects with a rigidbody (players,
        // balls, props) keep theirs, and so do colliders that already have a
        // physics material (the table's rails and felt), unless a Surface
        // component asks.
        private void AssignSurfaces()
        {
            RestoreBounce();
            PoolTableSurface felt = PoolTableSurface.Instance;
            tableRoot = felt != null ? (felt.transform.parent != null ? felt.transform.parent : felt.transform) : null;
            surfaceTypes = Resources.LoadAll<PlayAnywhereSurfaceType>("PlayAnywhereSurfaces");
            typeOfMaterial.Clear();
            groundOf.Clear();
            foreach (PlayAnywhereSurfaceType t in surfaceTypes)
                foreach (Material m in t.materials)
                    if (m != null && !typeOfMaterial.ContainsKey(m)) typeOfMaterial[m] = t;

            int count = 0;
            foreach (Collider c in Object.FindObjectsByType<Collider>())
            {
                if (c.isTrigger || c.attachedRigidbody != null) continue;
                PlayAnywhereSurfaceType type = FindSurface(c);
                groundOf[c] = type;
                if (type == null) continue;
                if (c.sharedMaterial != null && c.GetComponentInParent<PlayAnywhereSurface>() == null) continue;
                bouncedColliders.Add(c);
                originalMaterials.Add(c.sharedMaterial);
                c.sharedMaterial = type.RuntimeMaterial;
                count++;
            }
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] Surfaces : {count} colliders du décor, {surfaceTypes.Length} types, {typeOfMaterial.Count} matériaux listés.");
        }

        // Its Surface component, the pool table (null = felt), a listed
        // material, a keyword, or the default.
        private PlayAnywhereSurfaceType FindSurface(Collider collider)
        {
            PlayAnywhereSurface surface = collider.GetComponentInParent<PlayAnywhereSurface>();
            if (surface != null && surface.Type != null) return surface.Type;
            if (tableRoot != null && collider.transform.IsChildOf(tableRoot)) return null;

            // The collider is often on its own object, beside or above the mesh.
            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null) renderer = collider.GetComponentInParent<Renderer>();
            if (renderer == null) renderer = collider.GetComponentInChildren<Renderer>();
            materialBuffer.Clear();
            if (renderer != null) renderer.GetSharedMaterials(materialBuffer);
            foreach (Material m in materialBuffer)
                if (m != null && typeOfMaterial.TryGetValue(m, out PlayAnywhereSurfaceType byMaterial)) return byMaterial;

            foreach (PlayAnywhereSurfaceType t in surfaceTypes)
                foreach (string keyword in t.keywords)
                {
                    if (string.IsNullOrEmpty(keyword)) continue;
                    if (NameHas(collider.name, keyword) || (renderer != null && NameHas(renderer.name, keyword))) return t;
                    foreach (Material m in materialBuffer)
                        if (m != null && NameHas(m.name, keyword)) return t;
                }
            return settings.defaultSurface;
        }

        private static bool NameHas(string name, string keyword) =>
            name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static void RestoreBounce()
        {
            for (int i = 0; i < bouncedColliders.Count; i++)
                if (bouncedColliders[i] != null) bouncedColliders[i].sharedMaterial = originalMaterials[i];
            bouncedColliders.Clear();
            originalMaterials.Clear();
        }

        private static void ApplyGround(PoolBall ball, PlayAnywhereSurfaceType type)
        {
            ball.RollingFrictionOverride = type != null ? type.rollingFriction : -1f;
            ball.SlideFrictionOverride = type != null ? type.slideFriction : -1f;
        }

        // Over the cup (centre within its radius), at its level (the ball's
        // bottom within CupHeightTolerance of the cup's disc, which may sit a
        // little above or under the floor) and slow enough.
        private bool IsInCup(PoolBall ball, Vector3 pos, int p)
        {
            Vector3 offset = pos - hole.CupPosition;
            float height = offset.y - ball.Radius;
            offset.y = 0f;
            float distance = offset.magnitude;
            float speed = ball.HorizontalSpeed;
            bool over = distance < hole.CupRadius && Mathf.Abs(height) < CupHeightTolerance;
            bool caught = over && speed < settings.cupCatchSpeed;
            if (settings.debugLogs) TraceCupPass(p, caught, distance, height, speed);
            if (!over) lipped[p] = false;
            else if (!caught && !lipped[p])
            {
                // Once per pass, or the ball would brake to a stop on the lip.
                lipped[p] = true;
                HitLip(ball, offset, distance);
            }
            return caught;
        }

        // Too fast over the cup: the ball dips in and catches the far edge.
        // It loses speed (slow enough, it drops on the next frames), hops,
        // and is turned away from the centre, more the closer it passes to
        // the edge (a lip-out).
        private void HitLip(PoolBall ball, Vector3 offset, float distance)
        {
            Rigidbody rb = ball.Rigidbody;
            Vector3 v = rb.linearVelocity;
            Vector3 horizontal = new Vector3(v.x, 0f, v.z);
            // Which side of the centre the ball's path runs: turned that way.
            float side = Vector3.Dot(Vector3.Cross(horizontal, offset), Vector3.up) >= 0f ? 1f : -1f;
            float edge = Mathf.Clamp01(distance / hole.CupRadius);
            float angle = settings.cupLipDeflection * edge * side;
            horizontal = Quaternion.AngleAxis(angle, Vector3.up) * horizontal * (1f - settings.cupLipSlowdown);
            rb.linearVelocity = new Vector3(horizontal.x, Mathf.Max(v.y, 0f) + settings.cupLipHop, horizontal.z);
            if (settings.debugLogs)
                Debug.Log($"[PlayAnywhere] Lèvre du trou : {v.magnitude:0.00} → {horizontal.magnitude:0.00} m/s, déviée de {angle:0}°.", ball);
        }

        // Diagnostics: one line per pass near the cup that misses, with the
        // closest approach and what stopped the ball from dropping.
        private void TraceCupPass(int p, bool caught, float distance, float height, float speed)
        {
            bool near = distance < hole.CupRadius + CupTraceRange;
            if (near && !caught && distance < closestPass[p].x)
                closestPass[p] = new Vector3(distance, height, speed);
            if ((near && !caught) || float.IsPositiveInfinity(closestPass[p].x)) return;
            Vector3 c = closestPass[p];
            closestPass[p] = Vector3.positiveInfinity;
            if (caught) return;
            string why = c.x >= hole.CupRadius ? "trop loin du centre"
                : Mathf.Abs(c.y) >= CupHeightTolerance ? $"pas à la hauteur du trou (règle la hauteur du disc « Trou » : il doit être au niveau du sol)"
                : "trop vite";
            Debug.Log($"[PlayAnywhere] J{p + 1} passe à {c.x * 100f:0.0} cm du centre du trou (rayon {hole.CupRadius * 100f:0.0} cm), " +
                      $"dessous de la bille à {c.y * 100f:+0.0;-0.0} cm du disc (tolérance {CupHeightTolerance * 100f:0} cm), {c.z:0.00} m/s (max {settings.cupCatchSpeed:0.00}) → raté : {why}.", balls[p]);
        }

        private void HoleOut(PoolMatchRules match, int p)
        {
            PoolBall ball = balls[p];
            ball.FallIntoTableHole(hole.CupPosition + Vector3.up * ball.Radius, settings.cupSinkTime);
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] J{p + 1} rentre sa bille en {strokes[p]} coups (par {hole.Par}).", ball);
            Finish(match, p);
        }

        // Turn based: the shot is over, every ball at rest. Golf order: the
        // player farthest from the cup with strokes left this round plays;
        // nobody left, the round is over.
        public override void ResolveShot(PoolMatchRules match, IReadOnlyList<PoolBall> pocketedThisShot, bool cueBallPocketed, PoolBall firstContact)
        {
            int player = match.CurrentPlayer;
            rolling[player] = false;
            CheckStrokeLimit(match, player);
            if (match.GameOver || hole == null || phase == Phase.Intermission) return;

            int next = NextShooter();
            if (settings.debugLogs) Debug.Log(next >= 0
                ? $"[PlayAnywhere] Fin du coup de J{player + 1} → J{next + 1} joue (le plus loin du trou)."
                : $"[PlayAnywhere] Fin du coup de J{player + 1} : plus de coups dans cette manche.");
            if (next < 0) EndRound();
            else if (next != player) match.SwitchTurn();
        }

        private void CheckStrokeLimit(PoolMatchRules match, int p)
        {
            if (finished[p] || settings.maxStrokes <= 0 || strokes[p] < settings.maxStrokes) return;
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] J{p + 1} arrive à {settings.maxStrokes} coups sans avoir rentré sa bille : trou fini.", balls[p]);
            balls[p].BeginBallInHand();
            Finish(match, p);
        }

        // A table pocket: out of bounds too. OnPocketed parks the ball on its
        // tee right after this event, hence the next frame.
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            int p = PlayerOf(ball);
            if (p < 0 || finished[p]) return;
            match.StartCoroutine(OutOfBoundsNextFrame(match, p, match.MatchGeneration));
        }

        private IEnumerator OutOfBoundsNextFrame(PoolMatchRules match, int p, int generation)
        {
            yield return null;
            if (match == null || generation != match.MatchGeneration || match.GameOver) yield break;
            OutOfBounds(match, p, "dans une poche de la table");
        }

        // Only the player's own stroke costs the penalty: a ball knocked out
        // by the other one, or falling off its tee, just comes back.
        private void OutOfBounds(PoolMatchRules match, int p, string where)
        {
            PoolBall ball = balls[p];
            bool own = rolling[p];
            if (own) strokes[p] += settings.outOfBoundsPenalty;
            if (settings.debugLogs || !own)
                Debug.Log(own
                    ? $"[PlayAnywhere] J{p + 1} {where} : +{settings.outOfBoundsPenalty} (total {strokes[p]}), la bille revient à {lastRest[p]}."
                    : $"[PlayAnywhere] J{p + 1} {where} sans avoir été tirée (poussée par l'autre bille, ou tombée de sa place : vérifie le sol sous le départ) : pas de pénalité, elle revient à {lastRest[p]}.", ball);
            // Parked first: a kinematic body takes the move without a sweep.
            ball.BeginBallInHand();
            ball.PlaceAt(lastRest[p]);
            ball.EndBallInHand();
            rolling[p] = false;
            CheckStrokeLimit(match, p);
        }

        private void Finish(PoolMatchRules match, int p)
        {
            finished[p] = true;
            rolling[p] = false;
            if (firstFinisher < 0) firstFinisher = p;
            if (!finished[1 - p]) return;
            EndCourse();
        }

        private int PlayerOf(PoolBall ball) => ball == balls[0] ? 0 : ball == balls[1] ? 1 : -1;

        public override string DescribePlayer(int player)
        {
            string colour = player == 0 ? "Blanche bleue" : "Blanche rouge";
            return hole == null ? colour : $"{colour} · {strokes[player]} coup{(strokes[player] > 1 ? "s" : "")} (par {hole.Par}){(finished[player] ? " · fini" : "")} · {DescribeRound(player)}";
        }

        // Another mode starts: the balls are table balls again.
        public static void Cleanup()
        {
            Current = null;
            RestoreBounce();
            ClearProps();
            PlayAnywhereSpawnMenu.Clear();
            PlayAnywhereScoreboard.Hide();
            HideAllHoles();
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                ball.FreeRoaming = false;
                ApplyGround(ball, null);
            }
        }
    }
}
