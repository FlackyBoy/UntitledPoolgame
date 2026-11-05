using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RootMotion.Dynamics;
using UntitledPoolGame.Interaction;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // Shooter miniature: both players shrunk on the felt, shooting at each
    // other with the cue held as a rifle (LocalShooterWeapon). Health runs out
    // through shots, kicks, balls rolling into you and falls; each elimination is a
    // point for the other player, first to Eliminations To Win. A fall only
    // costs health: the player comes back on the felt.
    //
    // The table's balls are cover, made body-high (Cover Ball Scale): shots and
    // kicks move them, and one rolling into a player hurts them. Speed Pool
    // only (no turns). Derived from
    // SpeedPoolRuleSet because PoolMatchRules sends the pots to its
    // HandlePocketed; the cue balls stay put away, nobody shoots them.
    //
    // PoolMatchRules calls Tick every frame. The shrunk bodies are Pocket
    // Size's (LocalPlayerShrink, PocketSizeSettings).
    public partial class ShooterMiniRuleSet : SpeedPoolRuleSet
    {
        public enum Phase { Countdown, Fight, Over }

        protected class Fighter
        {
            public LocalPlayerShrink body;
            public LocalShooterWeapon weapon;
            public PlayerInput waitedFor;
            public int readyFrame;
            public Vector3 spawn;
            public float health;
            public int eliminations;
            public bool eliminated;
            public float respawnAt;
            public float safeUntil;
            public float ballSafeUntil;
            // Shooter Billard.
            public float slowUntil;
            public int hitsInARow;
            public float lastHitTime = -100f;
            public float impactUntil;
            public float frozenUntil;
            public GameObject ice;
            // Activated, waiting for the next shot.
            public ShooterPower armed;
            public bool armedLevel2;
        }

        // The match under way, null in other modes.
        public static ShooterMiniRuleSet Current { get; private set; }

        private static ShooterMiniSettings cfg;
        public static ShooterMiniSettings Settings =>
            cfg != null ? cfg : cfg = PoolSettingsLoader.LoadOrDefault<ShooterMiniSettings>("ShooterMiniSettings");

        // The model goes back to the clean animation over this long.
        private const float ImpactFade = 0.2f;

        protected readonly Fighter[] fighters = { new Fighter(), new Fighter() };
        private readonly List<(PoolBall ball, float time)> respots = new List<(PoolBall, float)>();
        private readonly List<Vector3> placed = new List<Vector3>();
        // Each ball's velocity last frame (CheckBalls).
        private readonly Dictionary<PoolBall, Vector3> lastVelocity = new Dictionary<PoolBall, Vector3>();
        private bool ready;
        private float phaseTime;
        private float feltY;
        private int ballLayers;
        private int winner;

        public Phase CurrentPhase { get; private set; }

        // What a mode built on this one changes (ShooterPoolRuleSet).
        protected virtual int BallsKept => Settings.coverBalls;
        protected virtual bool KeepsBall(PoolBall ball) => true;
        protected virtual void OnBallsPlaced() { }
        protected virtual void UpdateEffects(Fighter f) { }

        protected int CountdownLeft => Mathf.CeilToInt(Mathf.Max(0f, Settings.countdown - phaseTime));

        public bool CanFire(int player)
        {
            if (CurrentPhase != Phase.Fight) return false;
            Fighter f = fighters[player];
            return !f.eliminated && f.body != null && !f.body.IsDown && f.ice == null;
        }

        public override void Setup(PoolMatchRules match)
        {
            cfg = PoolSettingsLoader.LoadOrDefault<ShooterMiniSettings>("ShooterMiniSettings");
            LocalPlayerShrink.RestoreAll();
            Current = this;
            ballLayers = LayerMask.GetMask("Poolball");
            LocalUnarmedMelee.PlayerHit -= OnPlayerKicked;
            LocalUnarmedMelee.PlayerHit += OnPlayerKicked;

            PoolTableSurface surface = PoolTableSurface.Instance;
            // Both bodies come from these: without them, nothing to play
            // (ready stays false, Tick does nothing).
            if (surface == null || PocketSizeRuleSet.Settings.tinyPrefab == null || PlayerInputManager.instance == null || PlayerInputManager.instance.playerPrefab == null)
            {
                Debug.LogError("[ShooterMini] Il manque la table, le prefab du petit (PocketSizeSettings, Tiny Prefab) ou celui du joueur (PlayerInputManager, Player Prefab) : pas de partie.");
                return;
            }

            // Two players or nothing, as in Pocket Size.
            if (PlayerInput.all.Count < 2)
                Debug.Log($"[ShooterMini] Un seul joueur : écran partagé. {Core.SplitScreenCheatSpawner.JoinPlayer()}");
            if (PlayerInput.all.Count < 2)
                Debug.LogWarning("[ShooterMini] Il faut deux joueurs : le deuxième sera posé sur la table dès qu'il arrive (C+P).");

            feltY = surface.FeltTopY;
            Vector3 center = surface.transform.position;
            Vector3 along = surface.transform.rotation * Vector3.right;
            fighters[0].spawn = center - along * (surface.HalfLength * 0.6f);
            fighters[1].spawn = center + along * (surface.HalfLength * 0.6f);
            foreach (Fighter f in fighters)
            {
                f.spawn.y = feltY + 0.01f;
                f.health = Settings.maxHealth;
            }

            CurrentPhase = Phase.Countdown;
            ScatterBalls();
            OnBallsPlaced();
            ready = true;
            for (int p = 0; p < 2; p++) TryPlace(p);
        }

        // Another mode comes next: everyone full size, the cue ball back.
        public static new void Cleanup()
        {
            if (Current == null) return;
            Current.ClearPowerEffects();
            Current = null;
            LocalUnarmedMelee.PlayerHit -= OnPlayerKicked;
            LocalPlayerShrink.RestoreAll();
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                ball.ResetSize();
                if (ball.IsCueBall) ball.ResetToSpawn();
            }
        }

        // Swaps a player's body for the small one as soon as they're there and
        // ready. A body just there is given two frames: its puppet and
        // controllers start up first.
        private void TryPlace(int p)
        {
            Fighter f = fighters[p];
            if (f.body != null) return;
            PlayerInput player = PlayerInput.GetPlayerByIndex(p);
            if (player == null) return;
            if (player != f.waitedFor)
            {
                f.waitedFor = player;
                f.readyFrame = Time.frameCount + 2;
            }
            if (Time.frameCount < f.readyFrame) return;
            PocketSizeSettings tiny = PocketSizeRuleSet.Settings;
            f.body = LocalPlayerShrink.Shrink(player, tiny.tinyPrefab, PlayerInputManager.instance.playerPrefab,
                f.spawn, FacingCenter(f.spawn), tiny.tinyBlocksBalls ? ballLayers : 0, tiny.tinyNearClip, tiny.tinyJumpHeight, tiny.tinyFallStiffness, tiny.tinyFallGravity, tiny.tinyFallDamper);
            f.weapon = LocalShooterWeapon.Equip(f.body, p, this, Settings);
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] J{p + 1} posé sur la table à {f.spawn}.", f.body);
        }

        private static Quaternion FacingCenter(Vector3 from)
        {
            Vector3 look = Flat(PoolTableSurface.Instance.transform.position - from);
            return look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look) : Quaternion.identity;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // Cover Balls balls at random spots, the rest put away for the match,
        // the cue balls too.
        private void ScatterBalls()
        {
            var balls = new List<PoolBall>();
            foreach (PoolBall ball in Object.FindObjectsByType<PoolBall>(FindObjectsInactive.Include))
            {
                if (ball.IsCueBall || ball.IsExtra || !KeepsBall(ball)) ball.gameObject.SetActive(false);
                else balls.Add(ball);
            }
            for (int i = balls.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (balls[i], balls[j]) = (balls[j], balls[i]);
            }

            placed.Clear();
            int keep = Mathf.Min(BallsKept, balls.Count);
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (i >= keep)
                {
                    ball.gameObject.SetActive(false);
                    continue;
                }
                ball.ResetToSpawn();
                ball.SetSize(Settings.coverBallScale, Settings.coverBallMassExponent);
                Vector3 spot = FeltSpot(ball.Radius * 2f, feltY + ball.Radius);
                placed.Add(spot);
                ball.PlaceAt(spot);
            }
            placed.Clear();
        }

        // A random point of the felt at height y, clear of the pockets, the
        // players' starts and the balls already placed. Falls back on the
        // table's centre.
        private Vector3 FeltSpot(float margin, float y)
        {
            float clearance = Settings.coverClearance;
            float apart = margin * 1.5f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p = RandomFeltPoint(margin);
                p.y = y;
                bool clear = true;
                foreach (Fighter f in fighters)
                    clear &= Flat(p - f.spawn).sqrMagnitude >= clearance * clearance;
                foreach (Vector3 other in placed)
                    clear &= Flat(p - other).sqrMagnitude >= apart * apart;
                if (clear) return p;
            }
            Vector3 fallback = PoolTableSurface.Instance.ClampToPlayArea(PoolTableSurface.Instance.transform.position, margin);
            fallback.y = y;
            return fallback;
        }

        private static Vector3 RandomFeltPoint(float margin)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            Vector3 along = surface.transform.rotation * Vector3.right;
            Vector3 across = surface.transform.rotation * Vector3.forward;
            Vector3 p = surface.transform.position + along * (Random.Range(-1f, 1f) * surface.HalfLength) + across * (Random.Range(-1f, 1f) * surface.HalfWidth);
            return PoolPocket.AvoidAllPockets(surface.ClampToPlayArea(p, margin), margin);
        }

        // Where a player comes back: a free spot of the felt, away from the
        // other player when there's one to be found. Falls back on their start.
        private Vector3 RespawnSpot(int p)
        {
            Fighter other = fighters[1 - p];
            float clearance = Settings.respawnClearance;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 spot = RandomFeltPoint(0.05f);
                spot.y = feltY + 0.01f;
                if (other.body != null && Flat(spot - other.body.BodyPosition).sqrMagnitude < clearance * clearance) continue;
                if (IsClearOfBalls(spot)) return spot;
            }
            return fighters[p].spawn;
        }

        private static bool IsClearOfBalls(Vector3 spot)
        {
            foreach (PoolBall ball in PoolBall.Active)
            {
                float apart = ball.Radius + 0.03f;
                if (Flat(spot - ball.transform.position).sqrMagnitude < apart * apart) return false;
            }
            return true;
        }

        // ---------- Every frame (PoolMatchRules.Update) ----------

        public void Tick(PoolMatchRules match)
        {
            if (match.GameOver || !ready) return;
            ShooterMiniSettings s = Settings;
            phaseTime += Time.deltaTime;
            UpdateRespots();
            for (int p = 0; p < 2; p++) TryPlace(p);

            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    // Free to find their footing: a body off the table goes back on it.
                    foreach (Fighter f in fighters)
                        if (f.body != null && !OnTable(f.body)) f.body.Teleport(f.spawn, FacingCenter(f.spawn));
                    if (phaseTime < s.countdown) break;
                    CurrentPhase = Phase.Fight;
                    phaseTime = 0f;
                    if (s.debugLogs) Debug.Log("[ShooterMini] Combat !");
                    break;

                case Phase.Fight:
                    UpdatePowerEffects();
                    CheckBalls();
                    for (int p = 0; p < 2 && CurrentPhase == Phase.Fight; p++) UpdateFighter(p);
                    break;

                case Phase.Over:
                    if (phaseTime < s.endDelay) break;
                    LocalPlayerShrink.RestoreAll();
                    if (s.debugLogs) Debug.Log($"[ShooterMini] Victoire de J{winner + 1} ({fighters[0].eliminations}-{fighters[1].eliminations}).");
                    match.Win(winner);
                    break;
            }
        }

        private bool OnTable(LocalPlayerShrink body)
        {
            Vector3 p = body.transform.position;
            if (Mathf.Abs(p.y - feltY) > 0.15f) return false;
            return Flat(PoolTableSurface.Instance.ClampToPlayArea(p, 0f) - p).sqrMagnitude < 0.05f * 0.05f;
        }

        private void UpdateRespots()
        {
            for (int i = respots.Count - 1; i >= 0; i--)
            {
                (PoolBall ball, float time) = respots[i];
                if (Time.time < time) continue;
                respots.RemoveAt(i);
                if (ball == null) continue;
                ball.ResetToSpawn();
                ball.PlaceAt(PoolMatchRules.FindFreeSpot(FeltSpot(ball.Radius * 2f, feltY + ball.Radius), ball));
            }
        }

        private void UpdateFighter(int p)
        {
            Fighter f = fighters[p];
            if (f.body == null) return;
            ShowImpact(f);
            UpdateFreeze(f);
            UpdateEffects(f);
            CheckFall(p);
            if (f.eliminated)
            {
                // Back up first: moved while down, the ragdoll would come along.
                if (Time.time >= f.respawnAt && !f.body.IsDown) Respawn(p);
                return;
            }
            LocalShooterWeapon shooter = fighters[1 - p].weapon;
            if (shooter != null && shooter.FindHit(f.body, out Vector3 point, out Vector3 direction, out float freeze)
                && Hurt(p, Settings.shotDamage, point, Vector3.zero, "un tir", Push(direction, Settings.shotKnockSpeed)) && !f.eliminated)
            {
                ReactToShot(f, point, direction);
                if (freeze > 0f && !f.body.IsDown) Freeze(f, freeze);
            }
        }

        // The puppet takes the shot where it landed: the muscle nearest the
        // impact is thrown, and its chain with it, down to the hips, or a shot
        // in the hand would only move the hand. Nothing else plays (the gauge's
        // blow clip read as a guard, 04/11).
        private static void ReactToShot(Fighter f, Vector3 point, Vector3 direction)
        {
            PuppetMaster puppet = f.body.Puppet;
            if (puppet == null || f.body.IsDown) return;
            Muscle nearest = null;
            float best = float.MaxValue;
            foreach (Muscle muscle in puppet.muscles)
            {
                if (muscle.rigidbody == null) continue;
                float d = (muscle.rigidbody.worldCenterOfMass - point).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                nearest = muscle;
            }
            if (nearest == null) return;
            // A speed, whatever the mass: a force scaled to this body moved it by
            // 4 cm/s. The pin goes down directly rather than through a muscle hit:
            // kept above Pin Weight Threshold, a shot can't knock them out, only
            // the rules do (Hurt).
            float speed = Settings.shotImpactSpeed * LocalPlayerRagdollController.ScaleOf(puppet);
            nearest.state.pinWeightMlp = Mathf.Min(nearest.state.pinWeightMlp, Settings.shotPin);
            nearest.rigidbody.AddForceAtPosition(direction * speed * nearest.rigidbody.mass, point, ForceMode.Impulse);
            foreach (int child in nearest.childIndexes) ShakeMuscle(puppet.muscles[child], direction * (speed * 0.5f));
            foreach (int parent in nearest.parentIndexes) ShakeMuscle(puppet.muscles[parent], direction * (speed * 0.5f));
            f.impactUntil = Time.time + Settings.shotReactionTime;
            if (Settings.debugLogs) f.body.StartCoroutine(TraceImpact(puppet, nearest, speed));
        }

        private static void ShakeMuscle(Muscle muscle, Vector3 velocity)
        {
            if (muscle.rigidbody == null || muscle.state.isDisconnected) return;
            muscle.state.pinWeightMlp = Mathf.Min(muscle.state.pinWeightMlp, Settings.shotPin);
            muscle.rigidbody.AddForce(velocity, ForceMode.VelocityChange);
        }

        // What makes it show. Standing, the model doesn't follow the ragdoll:
        // the prefab's BehaviourPuppet is Unmapped, with Max Mapping Weight at 0
        // (the animation stays clean), and the ragdoll moved unseen until the
        // body fell. For the time of the reaction, the mapping is forced: here
        // in Update, after the puppet's FixedUpdate brought it back down and
        // before its LateUpdate maps the bones.
        private static void ShowImpact(Fighter f)
        {
            float left = f.impactUntil - Time.time;
            PuppetMaster puppet = f.body.Puppet;
            if (left <= 0f || puppet == null) return;
            float weight = Mathf.Clamp01(left / ImpactFade);
            foreach (Muscle muscle in puppet.muscles)
                if (!muscle.state.isDisconnected)
                    muscle.state.mappingWeightMlp = Mathf.Max(muscle.state.mappingWeightMlp, weight);
        }

        // Diagnostic: how far the ragdoll really left the animation, the muscle
        // hit and the hips, for 0.4 s, and whether the model was following it.
        private static IEnumerator TraceImpact(PuppetMaster puppet, Muscle hit, float speed)
        {
            float biggest = 0f, hips = 0f, pin = 1f, mapping = 1f;
            Muscle root = puppet.muscles[0];
            for (float end = Time.time + 0.4f; Time.time < end && puppet != null; )
            {
                yield return new WaitForFixedUpdate();
                biggest = Mathf.Max(biggest, Vector3.Distance(hit.transform.position, hit.target.position));
                hips = Mathf.Max(hips, Vector3.Distance(root.transform.position, root.target.position));
                pin = Mathf.Min(pin, hit.state.pinWeightMlp);
                yield return null;
                mapping = Mathf.Min(mapping, hit.state.mappingWeightMlp);
            }
            Debug.Log($"[ShooterMini] Impact sur {hit.transform.name} à {speed:0.00} m/s : écart max {biggest * 100f:0.0} cm, hanches {hips * 100f:0.0} cm, pin min {pin:0.00}, mapping min {mapping:0.00}.");
        }

        // A kick landed (LocalUnarmedMelee): it already throws the body, this
        // is the health. strength: about 0.5 for a tap, 1 for a Spartan kick.
        private static void OnPlayerKicked(int attacker, int victim, Vector3 point, float strength)
        {
            ShooterMiniRuleSet rules = Current;
            if (rules == null || rules.CurrentPhase != Phase.Fight || victim < 0 || victim > 1 || rules.fighters[victim].body == null) return;
            // Thrown away from the kicker if this is what puts them down.
            Fighter from = attacker >= 0 && attacker <= 1 ? rules.fighters[attacker] : null;
            Vector3 away = from != null && from.body != null ? point - from.body.transform.position : Vector3.zero;
            rules.Hurt(victim, Settings.kickDamage * strength, point, Vector3.zero, "un coup de pied", Push(away, Settings.shotKnockSpeed * strength * 2f));
        }

        // Flat in that direction, a little up: how a body is thrown (Knock Lift).
        private static Vector3 Push(Vector3 direction, float speed) =>
            (Flat(direction).normalized + Vector3.up * Settings.knockLift) * speed;

        private void Respawn(int p)
        {
            Fighter f = fighters[p];
            Vector3 spot = RespawnSpot(p);
            f.body.Teleport(spot, FacingCenter(spot));
            f.eliminated = false;
            f.health = Settings.maxHealth;
            f.safeUntil = Time.time + Settings.respawnSafeTime;
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] J{p + 1} réapparaît à {spot}.", f.body);
        }

        // Under the felt (a pocket, off the table): health lost, and back on
        // the felt whatever state they're in.
        private void CheckFall(int p)
        {
            Fighter f = fighters[p];
            Vector3 where = f.body.BodyPosition;
            if (where.y > feltY - Settings.fallDepth) return;
            Vector3 spot = RespawnSpot(p);
            f.body.Teleport(spot, FacingCenter(spot));
            Hurt(p, Settings.fallDamage, where, Vector3.zero, "une chute", Vector3.zero);
        }

        // Any ball rolling fast enough into a player hurts them and knocks them
        // down, whoever sent it. A distance test rather than collision
        // callbacks: the ball bounces off the body before overlapping it (Ball
        // Hit Margin), which is also why last frame's velocity counts.
        private void CheckBalls()
        {
            ShooterMiniSettings s = Settings;
            foreach (PoolBall ball in PoolBall.Active)
            {
                Vector3 now = ball.Rigidbody.linearVelocity;
                lastVelocity.TryGetValue(ball, out Vector3 before);
                lastVelocity[ball] = now;
                if (ball.Rigidbody.isKinematic) continue;

                for (int p = 0; p < 2; p++)
                {
                    Fighter f = fighters[p];
                    if (f.body == null || f.eliminated) continue;
                    if (f.body.IsDown) f.ballSafeUntil = Time.time + s.ballSafeTime;
                    if (Time.time < f.ballSafeUntil) continue;

                    f.body.GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
                    Vector3 c = ball.transform.position;
                    Vector3 nearest = ClosestOnSegment(bottom, top, c);
                    Vector3 incoming = Coming(now, nearest - c, s.minBallHitSpeed) ? now
                                     : Coming(before, nearest - c, s.minBallHitSpeed) ? before : Vector3.zero;
                    if (incoming == Vector3.zero) continue;
                    float reach = radius + ball.Radius + s.ballHitMargin + incoming.magnitude * Time.deltaTime;
                    if ((c - nearest).sqrMagnitude > reach * reach) continue;

                    f.ballSafeUntil = Time.time + s.ballSafeTime;
                    Vector3 rolling = Flat(incoming);
                    float speed = Mathf.Min(rolling.magnitude * s.knockTransfer, s.knockMaxSpeed);
                    Hurt(p, s.ballDamage, nearest, Push(rolling, speed), $"la bille {ball.Number}", Vector3.zero);
                    if (CurrentPhase != Phase.Fight) return;
                }
            }
        }

        private static bool Coming(Vector3 velocity, Vector3 toPlayer, float minSpeed) =>
            velocity.sqrMagnitude >= minSpeed * minSpeed && Vector3.Dot(Flat(velocity), Flat(toPlayer)) > 0f;

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSqr);
        }

        // knock: thrown down at this velocity (zero = stays up). push: the
        // velocity if this blow is the one that puts them down all the same.
        // False if they couldn't be hurt.
        protected virtual bool Hurt(int p, float damage, Vector3 where, Vector3 knock, string by, Vector3 push)
        {
            Fighter f = fighters[p];
            if (f.eliminated || Time.time < f.safeUntil) return false;
            f.health = Mathf.Max(0f, f.health - damage);
            bool isOut = f.health <= 0f;
            if (knock != Vector3.zero || isOut) f.body.KnockDown(knock != Vector3.zero ? knock : push, false);
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] J{p + 1} touché par {by} à {where} : {f.health:0} de vie.", f.body);
            if (isOut) Eliminate(p);
            return true;
        }

        private void Eliminate(int p)
        {
            Fighter f = fighters[p];
            Fighter other = fighters[1 - p];
            f.eliminated = true;
            f.respawnAt = Time.time + Settings.respawnDelay;
            other.eliminations++;
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] J{p + 1} éliminé : {fighters[0].eliminations}-{fighters[1].eliminations}.", f.body);
            if (other.eliminations >= Settings.eliminationsToWin) EndWith(1 - p);
        }

        protected void EndWith(int player)
        {
            winner = player;
            foreach (Fighter f in fighters) Thaw(f);
            CurrentPhase = Phase.Over;
            phaseTime = 0f;
        }

        protected void QueueRespot(PoolBall ball) => respots.Add((ball, Time.time + Settings.respotDelay));

        // Balls come back after Respot Delay. The cue balls are put away.
        public override void HandlePocketed(PoolMatchRules match, PoolBall ball)
        {
            if (!ball.IsCueBall) QueueRespot(ball);
        }

        // ---------- HUD ----------

        public virtual string HealthLabel(int player)
        {
            Fighter f = fighters[player];
            return f.eliminated ? Settings.eliminatedText : string.Format(Settings.healthFormat, Mathf.CeilToInt(f.health));
        }

        public virtual string DescribeScore(int player)
        {
            ShooterMiniSettings s = Settings;
            if (CurrentPhase == Phase.Countdown)
                return string.Format(s.countdownFormat, CountdownLeft);
            return string.Format(s.scoreFormat, fighters[player].eliminations, fighters[1 - player].eliminations, s.eliminationsToWin);
        }

        // Changes whenever the texts above would.
        public virtual int PlateKey(int player)
        {
            Fighter f = fighters[player];
            int countdown = CurrentPhase == Phase.Countdown ? CountdownLeft : 0;
            return (((f.eliminations * 32 + fighters[1 - player].eliminations) * 512 + Mathf.CeilToInt(f.health)) * 16 + countdown) * 2 + (f.eliminated ? 1 : 0);
        }

        public override string DescribePlayer(int player) => $"{HealthLabel(player)} · {DescribeScore(player)}";
    }
}
