using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Catch the Hole: opens the living holes (LivingHole) on the felt, adds
    // more as the match goes on (pots, timer) up to Max Holes, and works out
    // how far the speed-up is (Intensity, 0 → 1). One per scene, created on
    // demand; a restart (PoolMatchRules.MatchGeneration) takes the holes away.
    public class CatchTheHole : MonoBehaviour
    {
        public static CatchTheHole Instance { get; private set; }

        public CatchTheHoleSettings Settings { get; private set; }
        public bool Running { get; private set; }
        // How far the speed-up has gone, 0 → 1 (Ramp).
        public float Intensity { get; private set; }
        public float CruiseSpeed => Mathf.Lerp(Settings.baseSpeed, Mathf.Max(Settings.baseSpeed, Settings.maxSpeed), Intensity);
        public int HoleCount => holes.Count;

        private readonly List<LivingHole> holes = new List<LivingHole>();
        private TableHolePower look;
        private int generation;
        private float elapsed;
        private float sinceLastAdd;
        private int pots;
        private int potsSinceLastAdd;
        private int objectBallsAtStart;
        private int holesOpened;

        public static CatchTheHole Ensure()
        {
            if (Instance == null) Instance = new GameObject("Catch The Hole").AddComponent<CatchTheHole>();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
            Settings = PoolSettingsLoader.LoadOrDefault<CatchTheHoleSettings>("CatchTheHoleSettings");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => PoolBall.Pocketed += HandlePocketed;
        private void OnDisable() => PoolBall.Pocketed -= HandlePocketed;

        // Opens Start Holes and starts the clock. Anything left from before
        // is closed first.
        public void Begin()
        {
            Clear();
            PoolMatchRules rules = PoolMatchRules.Instance;
            look = FindLook();
            if (rules == null || look == null || PoolTableSurface.Instance == null)
            {
                Debug.LogWarning("[CatchTheHole] Pas de partie, de table ou d'apparence de trou (CatchTheHoleSettings > Look, ou un pouvoir Trou dans PoolPowerSpawnSettings) : rien n'est ouvert.");
                return;
            }
            generation = rules.MatchGeneration;
            objectBallsAtStart = 0;
            foreach (PoolBall ball in PoolBall.Active)
                if (!ball.IsCueBall) objectBallsAtStart++;
            Running = true;
            for (int i = 0; i < Settings.startHoles; i++) AddHole();
        }

        // Closes every living hole (at once) and stops.
        public void Clear()
        {
            foreach (LivingHole hole in holes)
                if (hole != null && hole.Hole != null) hole.Hole.Close(instant: true);
            holes.Clear();
            Running = false;
            Intensity = 0f;
            elapsed = sinceLastAdd = 0f;
            pots = potsSinceLastAdd = 0;
        }

        // One more hole, if under Max Holes: on the spot of the felt furthest
        // from the balls and the other holes, opening from nothing.
        public bool AddHole()
        {
            if (!Running || holes.Count >= Settings.maxHoles) return false;
            float margin = LivingHole.MaxReach(Settings);
            Vector3 best = LivingHole.RandomFeltPoint(margin);
            float bestScore = float.MinValue;
            for (int i = 0; i < 20; i++)
            {
                Vector3 candidate = LivingHole.RandomFeltPoint(margin);
                float score = Mathf.Min(DistanceToBalls(candidate), LivingHole.DistanceToOtherHoles(candidate, null));
                if (score > bestScore) { bestScore = score; best = candidate; }
            }

            TableHole hole = TableHole.Create(look, best, Settings.smoke, Settings.plainHole);
            hole.name = $"LivingHole {++holesOpened}";
            LivingHole living = hole.gameObject.AddComponent<LivingHole>();
            living.Init(this, Settings, look.HoleRadius);
            holes.Add(living);
            sinceLastAdd = 0f;
            potsSinceLastAdd = 0;
            if (Settings.debugLogs)
                Debug.Log($"[CatchTheHole] {hole.name} ouvert ({holes.Count}/{Settings.maxHoles}), à {bestScore * 100f:0} cm de la bille la plus proche.");
            return true;
        }

        private void Update()
        {
            if (!Running) return;
            PoolMatchRules rules = PoolMatchRules.Instance;
            // Rejouer (or the match gone): the holes go with the old match.
            if (rules == null || rules.MatchGeneration != generation)
            {
                Clear();
                return;
            }
            if (!rules.MatchStarted || rules.GameOver) return;

            elapsed += Time.deltaTime;
            sinceLastAdd += Time.deltaTime;
            if (Settings.addHoleEverySeconds > 0f && sinceLastAdd >= Settings.addHoleEverySeconds) AddHole();
            Intensity = ComputeIntensity();
        }

        private float ComputeIntensity()
        {
            float progress = objectBallsAtStart > 0 ? Mathf.Clamp01((float)pots / objectBallsAtStart) : 0f;
            float timer = Mathf.Clamp01(elapsed / Mathf.Max(1f, Settings.rampSeconds));
            return Settings.ramp switch
            {
                CatchTheHoleRamp.Progress => progress,
                CatchTheHoleRamp.Timer => timer,
                CatchTheHoleRamp.Both => Mathf.Max(progress, timer),
                _ => 0f,
            };
        }

        private void HandlePocketed(PoolBall ball, PoolPocket pocket)
        {
            if (!Running || ball.IsCueBall) return;
            pots++;
            potsSinceLastAdd++;
            if (Settings.addHoleEveryPots > 0 && potsSinceLastAdd >= Settings.addHoleEveryPots) AddHole();
        }

        private static float DistanceToBalls(Vector3 point)
        {
            float nearest = 1f;
            foreach (PoolBall ball in PoolBall.Active)
            {
                Vector3 offset = point - ball.transform.position;
                offset.y = 0f;
                nearest = Mathf.Min(nearest, offset.magnitude);
            }
            return nearest;
        }

        // The Trou power's look: the settings' one, else the first Trou
        // handed out by PoolPowerSpawnSettings.
        private TableHolePower FindLook()
        {
            if (Settings.look != null) return Settings.look;
            PoolPowerSpawnSettings spawn = PoolSettingsLoader.LoadOrDefault<PoolPowerSpawnSettings>("PoolPowerSpawnSettings");
            if (spawn != null && spawn.availablePowers != null)
                foreach (PoolPower power in spawn.availablePowers)
                    if (power is TableHolePower hole) return hole;
            return null;
        }
    }
}
