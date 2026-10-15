using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UntitledPoolGame.Pool
{
    // One meteor of TableHolePower: falls on a slant onto its spot, then
    // on impact — explosion, balls blasted off the spot, the hole opened
    // (PoolMatchRules.AddTableHole), a shake and flash on both screens, the
    // gamepads rumbling, Feel's "impact de météore" — and goes away once the
    // rumble is over. A meteor still falling when the match restarts or ends
    // lands without opening anything.
    public class TableHoleMeteor : MonoBehaviour
    {
        private TableHolePower power;
        private Vector3 target;
        private int owner;
        private float delay;
        private int generation;
        private readonly List<Gamepad> rumbling = new List<Gamepad>();

        public static void Launch(TableHolePower power, Vector3 target, int owner, float delay, int generation)
        {
            var meteor = new GameObject("Meteor").AddComponent<TableHoleMeteor>();
            meteor.power = power;
            meteor.target = target;
            meteor.owner = owner;
            meteor.delay = delay;
            meteor.generation = generation;
        }

        private IEnumerator Start()
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // From above, off to a random side, so it comes in on a slant.
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * power.FallSideOffset;
            Vector3 start = target + Vector3.up * power.FallHeight + side;
            GameObject fireball = CreateFireball(start);

            float time = Mathf.Max(0.01f, power.FallTime);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                // Speeding up all the way down.
                fireball.transform.position = Vector3.LerpUnclamped(start, target, k * k);
                yield return null;
            }
            fireball.transform.position = target;
            LetGo(fireball, 1.5f);

            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || rules.MatchGeneration != generation || !rules.MatchStarted || rules.GameOver)
            {
                Destroy(gameObject);
                yield break;
            }

            if (power.ImpactPrefab != null)
            {
                GameObject impact = Instantiate(power.ImpactPrefab, target, power.ImpactPrefab.transform.rotation);
                impact.transform.localScale = power.ImpactPrefab.transform.localScale * power.ImpactScale;
                if (power.ImpactLifetime > 0f) Destroy(impact, power.ImpactLifetime);
            }
            TableHole hole = TableHole.Create(power, target);
            power.BlastBalls(hole);
            rules.AddTableHole(hole, owner, power);

            LocalPoolPowerEffectReceiver.PlayImpact(-1, power.ShakeMagnitude, power.ShakeDuration,
                power.FlashColor, power.FlashAlpha, power.FlashDuration);
            TableHolePower.RaiseImpact(target, owner);

            if (power.RumbleDuration > 0f && (power.RumbleLow > 0f || power.RumbleHigh > 0f))
            {
                StartRumble();
                yield return new WaitForSecondsRealtime(power.RumbleDuration);
                StopRumble();
            }
            Destroy(gameObject);
        }

        private GameObject CreateFireball(Vector3 position)
        {
            if (power.MeteorPrefab != null)
            {
                GameObject fireball = Instantiate(power.MeteorPrefab, position, power.MeteorPrefab.transform.rotation);
                fireball.transform.localScale = power.MeteorPrefab.transform.localScale * power.MeteorScale;
                return fireball;
            }
            // Placeholder: an orange sphere.
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * 0.12f;
            sphere.GetComponent<Renderer>().material.color = new Color(1f, 0.45f, 0.1f);
            return sphere;
        }

        // Stops emitting and lets the flames already out fade, rather than
        // popping the fireball out of existence.
        private static void LetGo(GameObject effect, float fade)
        {
            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>();
            if (systems.Length == 0) { Destroy(effect); return; }
            foreach (ParticleSystem system in systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(effect, fade);
        }

        // Every player's gamepad (keyboard players have none).
        private void StartRumble()
        {
            foreach (PlayerInput player in PlayerInput.all)
                foreach (InputDevice device in player.devices)
                    if (device is Gamepad pad && !rumbling.Contains(pad))
                    {
                        pad.SetMotorSpeeds(power.RumbleLow, power.RumbleHigh);
                        rumbling.Add(pad);
                    }
        }

        private void StopRumble()
        {
            foreach (Gamepad pad in rumbling)
                if (pad != null) pad.ResetHaptics();
            rumbling.Clear();
        }

        // Never leave a gamepad rumbling (scene unloaded mid-rumble).
        private void OnDestroy() => StopRumble();
    }
}
