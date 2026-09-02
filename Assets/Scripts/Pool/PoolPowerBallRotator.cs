using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Periodically moves "which ball currently carries a power" to a
    // different random ball still on the table — PowerBall itself only
    // holds the assigned power and renders the glow; this decides WHEN it
    // rotates (random interval), WHICH ball gets it next, and WHAT power
    // (random draw from PoolPowerSpawnSettings.availablePowers), same split
    // of responsibilities as PoolPowerCrateManager/PoolPowerCrate.
    public class PoolPowerBallRotator : MonoBehaviour
    {
        private PoolPowerSpawnSettings settings;
        private PowerBall currentBall;

        private void Awake()
        {
            settings = Resources.Load<PoolPowerSpawnSettings>("PoolPowerSpawnSettings");
            if (settings == null)
                Debug.LogWarning("PoolPowerSpawnSettings asset not found in Assets/Resources — the power ball won't rotate. Run Tools > Pool > Ensure Config Assets Exist to create it.");
        }

        private void Start()
        {
            if (settings == null || settings.availablePowers == null || settings.availablePowers.Length == 0) return;
            StartCoroutine(RotationLoop());
        }

        private IEnumerator RotationLoop()
        {
            // Assigns a power right away once the match starts, rather than
            // leaving every ball powerless for the first
            // ballRotationMinInterval..MaxInterval seconds (up to 40s by
            // default) — that gap was indistinguishable from "picking up a
            // power ball doesn't work" if tested early in a match.
            yield return new WaitUntil(IsMatchLive);
            Rotate();

            while (true)
            {
                float delay = Random.Range(settings.ballRotationMinInterval, settings.ballRotationMaxInterval);
                yield return new WaitForSeconds(delay);

                if (!IsMatchLive()) continue;

                Rotate();
            }
        }

        private static bool IsMatchLive()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules != null && rules.MatchStarted && !rules.GameOver;
        }

        private void Rotate()
        {
            if (currentBall != null) currentBall.ClearGlow();

            List<PoolBall> candidates = new List<PoolBall>();
            foreach (PoolBall ball in PoolBall.Active)
            {
                if (!ball.IsCueBall) candidates.Add(ball);
            }
            if (candidates.Count == 0) return;

            PoolBall chosen = candidates[Random.Range(0, candidates.Count)];
            if (!chosen.TryGetComponent(out PowerBall powerBall)) return;

            PoolPower power = settings.availablePowers[Random.Range(0, settings.availablePowers.Length)];
            powerBall.SetGlow(power, settings.GetColor(power.Type), settings.ballGlowIntensity, settings.ballGlowRange);
            currentBall = powerBall;
        }
    }
}
