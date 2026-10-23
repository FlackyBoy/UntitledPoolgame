using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Moves the power to another random ball still on the table at random
    // intervals, with a new random power. PowerBall only holds it and glows.
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
            // First power as soon as the match starts (waiting the interval
            // looked like power balls didn't work).
            yield return new WaitUntil(IsMatchLive);

            // Party mode only. The mode is known once the match has started.
            if (PoolMatchRules.Instance.Mode != PoolGameMode.Party) yield break;
            // Speed Pool with powers: the crates only (decided 21/10).
            if (PoolMatchRules.Instance.IsSimultaneous) yield break;

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
            powerBall.SetGlow(power, settings.GetColor(power.Type), settings.ballGlowIntensity, settings.ballGlowRange, settings.GetBallMaterial(power.Type));
            currentBall = powerBall;
        }
    }
}
