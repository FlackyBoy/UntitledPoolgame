using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // The Shooter's powers (ShooterPower): crates, and what they do to the
    // match. All put back at the end (ClearPowerEffects).
    public partial class ShooterMiniRuleSet
    {
        private static readonly Color Ice = new Color(0.55f, 0.85f, 1f, 0.45f);
        private const float IceAroundBody = 1.3f;
        private const float IceAroundBall = 1.12f;
        private const float BlastEdgeStrength = 0.5f;
        private const float TraceThrowTime = 1.5f;

        private readonly List<(PoolBall ball, float until, GameObject ice)> frozenBalls = new List<(PoolBall, float, GameObject)>();
        private readonly List<(PoolPocket pocket, float until)> closedPockets = new List<(PoolPocket, float)>();
        private readonly List<GameObject> powerObjects = new List<GameObject>();

        public float FeltY => feltY;
        public LocalShooterWeapon WeaponOf(int player) => fighters[player].weapon;

        private void Keep(GameObject thing)
        {
            powerObjects.RemoveAll(o => o == null);
            powerObjects.Add(thing);
        }

        // Activated with the power button: one at a time, the next one would
        // take its place and lose it (PoolMatchRules.CheckPowerUse).
        public bool CanArm(int player) => CanFire(player) && fighters[player].armed == null;

        public ShooterPower ArmedPower(int player) => fighters[player].armed;

        public void ArmPower(int player, ShooterPower power, bool level2)
        {
            Fighter f = fighters[player];
            f.armed = power;
            f.armedLevel2 = level2;
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] J{player + 1} arme « {power.PowerName} ».", power);
        }

        // No power waiting counts as ready.
        public bool ArmedPowerReady(int player)
        {
            Fighter f = fighters[player];
            return f.armed == null || f.armed.ReadyToFire(this, player, f.armedLevel2);
        }

        // Takes the place of this shot (LocalShooterWeapon.Fire). False if no
        // power is waiting.
        public bool FireArmedPower(int player)
        {
            Fighter f = fighters[player];
            if (f.armed == null) return false;
            ShooterPower power = f.armed;
            f.armed = null;
            power.Fire(this, player, f.armedLevel2);
            return true;
        }

        // True when a player walking there took it (PoolPowerCrate).
        public bool TryTakeCrate(PoolMatchRules match, Vector3 position, PoolPower power)
        {
            if (CurrentPhase != Phase.Fight) return false;
            float reach = Settings.crateReach;
            for (int p = 0; p < 2; p++)
            {
                if (!CanFire(p) || Flat(fighters[p].body.transform.position - position).sqrMagnitude > reach * reach) continue;
                match.GrantPower(p, power);
                if (Settings.debugLogs) Debug.Log($"[ShooterMini] J{p + 1} ramasse « {power.PowerName} ».", fighters[p].body);
                return true;
            }
            return false;
        }

        // ---------- Rolling balls ----------

        // In front of the player, on the felt, where they're looking.
        public void LaunchRoller(int player, ShooterRollingBall.Spec spec, bool chaseOther)
        {
            Fighter f = fighters[player];
            if (f.body == null || f.weapon == null) return;
            Vector3 direction = Flat(f.weapon.AimDirection).normalized;
            if (direction == Vector3.zero) direction = f.body.transform.forward;
            f.body.GetCapsule(out _, out _, out float bodyRadius);
            Vector3 at = f.body.transform.position + direction * (bodyRadius + spec.radius + Settings.ballHitMargin);
            at.y = feltY + spec.radius;
            Fighter other = fighters[1 - player];
            ShooterRollingBall ball = ShooterRollingBall.Launch(this, player, spec, at, direction, chaseOther && other.body != null ? other.body.transform : null);
            Keep(ball.gameObject);
        }

        // Knocks down whoever it reaches, as a table ball does. True if it hit.
        public bool TryHitWithRoller(ShooterRollingBall roller)
        {
            if (CurrentPhase != Phase.Fight) return false;
            ShooterMiniSettings s = Settings;
            Vector3 rolling = Flat(roller.Velocity);
            if (rolling.sqrMagnitude < s.minBallHitSpeed * s.minBallHitSpeed) return false;
            Vector3 c = roller.transform.position;
            bool hit = false;
            for (int p = 0; p < 2; p++)
            {
                Fighter f = fighters[p];
                if (f.body == null || f.eliminated || f.body.IsDown || Time.time < f.ballSafeUntil) continue;
                if (p == roller.Owner && roller.OwnerSafe) continue;
                f.body.GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
                Vector3 nearest = ClosestOnSegment(bottom, top, c);
                float reach = radius + roller.Radius + s.ballHitMargin;
                if ((c - nearest).sqrMagnitude > reach * reach) continue;
                f.ballSafeUntil = Time.time + s.ballSafeTime;
                float speed = Mathf.Min(rolling.magnitude * s.knockTransfer, s.knockMaxSpeed);
                Hurt(p, roller.Damage, nearest, Push(rolling, speed), roller.Label, Vector3.zero);
                hit = true;
                if (CurrentPhase != Phase.Fight) return true;
            }
            return hit;
        }

        // ---------- Grapple ----------

        // The line brought player onto the other one: down they go.
        public void GrappleLanded(int player, Vector3 direction, float damage, float knockSpeed, string by)
        {
            int victim = 1 - player;
            Fighter f = fighters[victim];
            if (CurrentPhase != Phase.Fight || f.body == null || f.eliminated || f.body.IsDown) return;
            Hurt(victim, damage, f.body.BodyPosition, Push(direction, knockSpeed), by, Vector3.zero);
        }

        public bool IsOpponent(int player, LocalPlayerShrink body) => body != null && fighters[1 - player].body == body;

        // ---------- Mine ----------

        public void LayMine(int player, ShooterMine.Spec spec)
        {
            Fighter f = fighters[player];
            if (f.body == null) return;
            Vector3 at = f.body.transform.position;
            at.y = feltY;
            Keep(ShooterMine.Lay(this, player, spec, at).gameObject);
        }

        // A player on it, or a ball rolling over it. A ball at rest doesn't
        // count: one lying nearby would set it off as soon as it's armed.
        public bool IsMineTouched(Vector3 at, float reach)
        {
            if (CurrentPhase != Phase.Fight) return false;
            foreach (Fighter f in fighters)
            {
                if (f.body == null || f.eliminated || f.body.IsDown) continue;
                f.body.GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
                float within = radius + reach;
                if ((at - ClosestOnSegment(bottom, top, at)).sqrMagnitude <= within * within) return true;
            }
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.Rigidbody.isKinematic || !ball.IsRolling) continue;
                float within = ball.Radius + reach;
                if (Flat(ball.transform.position - at).sqrMagnitude <= within * within) return true;
            }
            foreach (ShooterRollingBall roller in ShooterRollingBall.Active)
            {
                float within = roller.Radius + reach;
                if (Flat(roller.transform.position - at).sqrMagnitude <= within * within) return true;
            }
            return false;
        }

        // Thrown up as well as out, and on its own terms: Push's small lift
        // (0.16 m/s at this speed, 6 mm of air under the Mini's gravity) left a
        // flat push the feet's friction ate, and the body fell where it stood.
        public void Blast(Vector3 at, int owner, float radius, float damage, float knockSpeed, float liftSpeed, float ballSpeed, string by)
        {
            for (int p = 0; p < 2 && CurrentPhase == Phase.Fight; p++)
            {
                Fighter f = fighters[p];
                if (f.body == null || f.eliminated || f.body.IsDown) continue;
                f.body.GetCapsule(out Vector3 bottom, out Vector3 top, out _);
                Vector3 nearest = ClosestOnSegment(bottom, top, at);
                float distance = (nearest - at).magnitude;
                if (distance > radius) continue;
                float strength = Mathf.Lerp(1f, BlastEdgeStrength, distance / radius);
                Vector3 knock = (Flat(nearest - at).normalized * knockSpeed + Vector3.up * liftSpeed) * strength;
                if (Hurt(p, damage, nearest, knock, by, Vector3.zero) && Settings.debugLogs)
                    f.body.StartCoroutine(TraceThrow(f.body, by));
            }
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (ball.Rigidbody.isKinematic) continue;
                Vector3 away = Flat(ball.transform.position - at);
                float distance = away.magnitude;
                if (distance > radius || distance < 1e-4f) continue;
                ball.Rigidbody.AddForce(away / distance * (ballSpeed * (1f - distance / radius)), ForceMode.VelocityChange);
                ball.MarkHitBy(owner);
            }
        }

        // Diagnostic: how far the body really went.
        private static IEnumerator TraceThrow(LocalPlayerShrink body, string by)
        {
            Vector3 start = body.BodyPosition;
            float highest = 0f, farthest = 0f;
            for (float end = Time.time + TraceThrowTime; Time.time < end; )
            {
                yield return null;
                Vector3 moved = body.BodyPosition - start;
                highest = Mathf.Max(highest, moved.y);
                farthest = Mathf.Max(farthest, Flat(moved).magnitude);
            }
            Debug.Log($"[ShooterMini] Projeté par {by} : monté de {highest * 100f:0} cm, parti à {farthest * 100f:0} cm.", body);
        }

        // ---------- Freeze ----------

        // Nothing works any more: the input is off, the body stands still in
        // its pose. A blow that knocks them down ends it (UpdateFreeze).
        private void Freeze(Fighter f, float time)
        {
            f.frozenUntil = Mathf.Max(f.frozenUntil, Time.time + time);
            if (f.ice != null) return;
            f.body.SetFrozen(true);
            if (f.weapon != null) f.weapon.StopMoves();
            f.body.GetCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            f.ice = ShooterPowerFx.Shape(PrimitiveType.Sphere, ShooterPowerFx.Glass(Ice, "Shooter Ice"), "Shooter Ice");
            f.ice.transform.position = (bottom + top) * 0.5f;
            f.ice.transform.localScale = Vector3.one * (((top - bottom).magnitude + radius * 2f) * IceAroundBody);
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] {f.body.name} gelé {time:0.#} s.", f.body);
        }

        private static void UpdateFreeze(Fighter f)
        {
            if (f.ice == null) return;
            if (Time.time >= f.frozenUntil || f.body.IsDown) Thaw(f);
            // Closing the pause menu gives everyone their input back.
            else f.body.SetFrozen(true);
        }

        private static void Thaw(Fighter f)
        {
            if (f.ice == null) return;
            if (f.body != null) f.body.SetFrozen(false);
            Object.Destroy(f.ice);
            f.ice = null;
            f.frozenUntil = 0f;
        }

        // A wall for the time being: nothing moves it.
        public void FreezeBall(PoolBall ball, float time)
        {
            for (int i = 0; i < frozenBalls.Count; i++)
            {
                if (frozenBalls[i].ball != ball) continue;
                frozenBalls[i] = (ball, Time.time + time, frozenBalls[i].ice);
                return;
            }
            if (ball.Rigidbody.isKinematic) return;
            ball.Rigidbody.linearVelocity = Vector3.zero;
            ball.Rigidbody.angularVelocity = Vector3.zero;
            ball.Rigidbody.isKinematic = true;
            GameObject ice = ShooterPowerFx.Shape(PrimitiveType.Sphere, ShooterPowerFx.Glass(Ice, "Shooter Ice"), "Shooter Ice");
            ice.transform.position = ball.transform.position;
            ice.transform.localScale = Vector3.one * (ball.Radius * 2f * IceAroundBall);
            frozenBalls.Add((ball, Time.time + time, ice));
        }

        // ---------- Magnet, pocket ----------

        public void PlantMagnet(int player, float duration, float radius, float acceleration, float range, GameObject vfxPrefab, float vfxScale)
        {
            LocalShooterWeapon weapon = fighters[player].weapon;
            if (weapon == null) return;
            Vector3 at = PoolTableSurface.Instance.ClampToPlayArea(weapon.AimedPoint(range), 0.03f);
            at.y = feltY;
            Keep(ShooterMagnet.Plant(at, duration, radius, acceleration, vfxPrefab, vfxScale).gameObject);
        }

        public void ClosePocketInSight(int player, float duration)
        {
            LocalShooterWeapon weapon = fighters[player].weapon;
            if (weapon == null) return;
            PoolPocket best = null;
            float bestAngle = float.MaxValue;
            foreach (PoolPocket pocket in PoolPocket.Active)
            {
                if (pocket.IsClosed) continue;
                float angle = Vector3.Angle(weapon.AimDirection, pocket.transform.position - weapon.AimOrigin);
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                best = pocket;
            }
            if (best == null) return;
            best.SetClosed(true);
            closedPockets.Add((best, Time.time + duration));
            if (Settings.debugLogs) Debug.Log($"[ShooterMini] Poche {best.DescribeLocation()} fermée {duration:0.#} s par J{player + 1}.", best);
        }

        // ---------- Timers, end ----------

        private void UpdatePowerEffects()
        {
            for (int i = frozenBalls.Count - 1; i >= 0; i--)
            {
                if (Time.time < frozenBalls[i].until) continue;
                ReleaseBall(frozenBalls[i]);
                frozenBalls.RemoveAt(i);
            }
            for (int i = closedPockets.Count - 1; i >= 0; i--)
            {
                if (Time.time < closedPockets[i].until) continue;
                if (closedPockets[i].pocket != null) closedPockets[i].pocket.SetClosed(false);
                closedPockets.RemoveAt(i);
            }
        }

        private static void ReleaseBall((PoolBall ball, float until, GameObject ice) frozen)
        {
            if (frozen.ball != null) frozen.ball.Rigidbody.isKinematic = false;
            if (frozen.ice != null) Object.Destroy(frozen.ice);
        }

        private void ClearPowerEffects()
        {
            foreach (Fighter f in fighters) Thaw(f);
            foreach (var frozen in frozenBalls) ReleaseBall(frozen);
            frozenBalls.Clear();
            foreach (var closed in closedPockets)
                if (closed.pocket != null) closed.pocket.SetClosed(false);
            closedPockets.Clear();
            foreach (GameObject thing in powerObjects)
                if (thing != null) Object.Destroy(thing);
            powerObjects.Clear();
        }
    }
}
