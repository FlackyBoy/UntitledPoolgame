using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Spawns the crates: where (random spawn point), what (random power) and
    // when a collected one comes back (random delay, new spot). One per table.
    public class PoolPowerCrateManager : MonoBehaviour
    {
        public static PoolPowerCrateManager Instance { get; private set; }

        private PoolPowerSpawnSettings settings;
        private readonly List<PoolPowerSpawnPoint> spawnPoints = new List<PoolPowerSpawnPoint>();
        private readonly Dictionary<PoolPowerCrate, PoolPowerSpawnPoint> occupiedBy = new Dictionary<PoolPowerCrate, PoolPowerSpawnPoint>();

        private void Awake()
        {
            Instance = this;

            settings = Resources.Load<PoolPowerSpawnSettings>("PoolPowerSpawnSettings");
            if (settings == null)
            {
                Debug.LogWarning("PoolPowerSpawnSettings asset not found in Assets/Resources — power crates won't spawn. Run Tools > Pool > Ensure Config Assets Exist to create it.");
                return;
            }

            spawnPoints.AddRange(FindObjectsByType<PoolPowerSpawnPoint>());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (settings == null || settings.availablePowers == null || settings.availablePowers.Length == 0) return;
            StartCoroutine(WaitForPartyModeThenSpawnInitialCrates());
        }

        // Party mode only, known once the match has started.
        private IEnumerator WaitForPartyModeThenSpawnInitialCrates()
        {
            yield return new WaitUntil(() => PoolMatchRules.Instance != null && PoolMatchRules.Instance.MatchStarted);
            if (PoolMatchRules.Instance.Mode != PoolGameMode.Party) yield break;

            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("PoolPowerCrateManager: no PoolPowerSpawnPoint found in the scene — no crates to spawn.");
                yield break;
            }

            int count = Mathf.Min(settings.activeCrateCount, spawnPoints.Count);
            List<PoolPowerSpawnPoint> pool = new List<PoolPowerSpawnPoint>(spawnPoints);
            for (int i = 0; i < count; i++)
            {
                PoolPowerSpawnPoint point = TakeRandom(pool);
                SpawnAt(CreateCrate(), point);
            }
        }

        // Called by PoolPowerCrate.FixedUpdate() right after it's picked up.
        public void NotifyCollected(PoolPowerCrate crate)
        {
            if (!occupiedBy.TryGetValue(crate, out PoolPowerSpawnPoint freedPoint)) return;
            occupiedBy.Remove(crate);
            StartCoroutine(RespawnRoutine(crate, freedPoint));
        }

        private IEnumerator RespawnRoutine(PoolPowerCrate crate, PoolPowerSpawnPoint justFreedPoint)
        {
            float delay = Random.Range(settings.crateRespawnMinDelay, settings.crateRespawnMaxDelay);
            yield return new WaitForSeconds(delay);

            // A free point if possible, else the one it just left.
            List<PoolPowerSpawnPoint> free = new List<PoolPowerSpawnPoint>();
            foreach (PoolPowerSpawnPoint point in spawnPoints)
            {
                bool taken = false;
                foreach (PoolPowerSpawnPoint occupied in occupiedBy.Values)
                {
                    if (occupied == point) { taken = true; break; }
                }
                if (!taken) free.Add(point);
            }

            PoolPowerSpawnPoint target = free.Count > 0 ? free[Random.Range(0, free.Count)] : justFreedPoint;
            SpawnAt(crate, target);
        }

        private PoolPowerCrate CreateCrate()
        {
            GameObject go = new GameObject("PoolPowerCrate");
            go.transform.SetParent(transform, worldPositionStays: false);
            return go.AddComponent<PoolPowerCrate>();
        }

        private void SpawnAt(PoolPowerCrate crate, PoolPowerSpawnPoint point)
        {
            PoolPower power = settings.availablePowers[Random.Range(0, settings.availablePowers.Length)];
            crate.transform.position = point.transform.position;
            crate.gameObject.SetActive(true);
            // The power's own token (its icon) first, else its category's crate.
            GameObject prefab = power.TokenPrefab != null ? power.TokenPrefab : settings.GetCratePrefab(power.Type);
            crate.Initialize(power, prefab, settings.GetColor(power.Type));
            occupiedBy[crate] = point;
        }

        private static PoolPowerSpawnPoint TakeRandom(List<PoolPowerSpawnPoint> pool)
        {
            int index = Random.Range(0, pool.Count);
            PoolPowerSpawnPoint point = pool[index];
            pool.RemoveAt(index);
            return point;
        }
    }
}
