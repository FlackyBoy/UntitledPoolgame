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
        // Every crate made, spawned or waiting to respawn.
        private readonly List<PoolPowerCrate> crates = new List<PoolPowerCrate>();

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

        private void OnEnable() => PoolMatchRules.MatchBegan += OnMatchBegan;
        private void OnDisable() => PoolMatchRules.MatchBegan -= OnMatchBegan;

        private void Start()
        {
            // Already under way (started before this woke up).
            if (PoolMatchRules.Instance != null && PoolMatchRules.Instance.MatchStarted) OnMatchBegan();
        }

        // Powers on only, known once the match has started. Each match may
        // differ (Boogie Night chains modes, with or without powers): the
        // crates are put away, then dealt again for this mode if it has powers.
        private void OnMatchBegan()
        {
            if (settings == null || settings.availablePowers == null || settings.availablePowers.Length == 0) return;
            StopAllCoroutines();
            foreach (PoolPowerCrate crate in crates) if (crate != null) crate.gameObject.SetActive(false);
            occupiedBy.Clear();
            if (PoolMatchRules.Instance.PowersEnabled) SpawnInitialCrates();
        }

        private void SpawnInitialCrates()
        {
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("PoolPowerCrateManager: no PoolPowerSpawnPoint found in the scene — no crates to spawn.");
                return;
            }

            int count = Mathf.Min(settings.activeCrateCount, spawnPoints.Count);
            List<PoolPowerSpawnPoint> pool = new List<PoolPowerSpawnPoint>(spawnPoints);
            for (int i = 0; i < count; i++)
            {
                PoolPowerSpawnPoint point = TakeRandom(pool);
                // The crates put away by a match without powers come back first.
                PoolPowerCrate crate = i < crates.Count && crates[i] != null ? crates[i] : CreateCrate();
                SpawnAt(crate, point);
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
            PoolPowerCrate crate = go.AddComponent<PoolPowerCrate>();
            crates.Add(crate);
            return crate;
        }

        private void SpawnAt(PoolPowerCrate crate, PoolPowerSpawnPoint point)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            PoolPower power = rules != null ? settings.PickPower(rules.Mode) : null;
            // No power allowed in this mode: the spot stays empty.
            if (power == null)
            {
                crate.gameObject.SetActive(false);
                return;
            }
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
