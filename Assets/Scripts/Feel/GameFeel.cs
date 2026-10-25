using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Listens to the match (balls potted, fouls, shots, powers, BOUM, turns,
    // win) and plays the Feel sequence GameFeelSettings gives for that moment.
    // Each sequence prefab is instantiated once per scene and replayed.
    // EventPlayer tells our own feedbacks (MMF_PoolHudText,
    // MMF_PoolScreenJuice) whose half of the screen the moment belongs to.
    // Created automatically in any scene with a PoolMatchRules.
    public class GameFeel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        private static void TryCreate()
        {
            // Any scene with a match, or with players (the R&D arena).
            if (FindAnyObjectByType<GameFeel>() != null) return;
            if (FindAnyObjectByType<PoolMatchRules>() == null && FindAnyObjectByType<UnityEngine.InputSystem.PlayerInputManager>() == null) return;
            new GameObject("Game feel (Feel)").AddComponent<GameFeel>();
        }

        // The player the moment belongs to (-1 = nobody in particular).
        public static int EventPlayer { get; private set; } = -1;

        private readonly Dictionary<MMF_Player, MMF_Player> instances = new Dictionary<MMF_Player, MMF_Player>();
        private int lastShooter = -1;
        private bool wonPlayed;

        private void Start()
        {
            // Instantiated up front so a sequence is initialized before its
            // first play.
            foreach (GameFeelEntry e in GameFeelSettings.Instance.entries)
                if (e != null && e.feedback != null) InstanceOf(e.feedback);
        }

        private void OnEnable()
        {
            PoolBall.Pocketed += OnPocketed;
            PoolMatchRules.Fouled += OnFoul;
            PoolMatchRules.PowerGranted += OnPower;
            PoolMatchRules.PowerActivated += OnPowerActivated;
            PoolMatchRules.PowerRefused += OnPowerRefused;
            PoolMatchRules.BallBlasted += OnBlast;
            PoolMatchRules.TurnChanged += OnTurn;
            LocalPoolAimController.ShotTaken += OnShot;
            UntitledPoolGame.Interaction.LocalUnarmedMelee.PlayerHit += OnPlayerHit;
            UntitledPoolGame.Interaction.LocalHitGauge.KnockedOut += OnKnockedOut;
            UntitledPoolGame.Interaction.LocalUnarmedMelee.GuardBroken += OnGuardBroken;
            TableHolePower.Impact += OnTableHoleImpact;
            HungryHoleCue.Swallowed += OnHungryHoleSwallow;
            HungryHoleCue.AteHole += OnHungryHoleAteHole;
            HungryHoleCue.Grew += OnHungryHoleGrew;
        }

        private void OnDisable()
        {
            PoolBall.Pocketed -= OnPocketed;
            PoolMatchRules.Fouled -= OnFoul;
            PoolMatchRules.PowerGranted -= OnPower;
            PoolMatchRules.PowerActivated -= OnPowerActivated;
            PoolMatchRules.PowerRefused -= OnPowerRefused;
            PoolMatchRules.BallBlasted -= OnBlast;
            PoolMatchRules.TurnChanged -= OnTurn;
            LocalPoolAimController.ShotTaken -= OnShot;
            UntitledPoolGame.Interaction.LocalUnarmedMelee.PlayerHit -= OnPlayerHit;
            UntitledPoolGame.Interaction.LocalHitGauge.KnockedOut -= OnKnockedOut;
            UntitledPoolGame.Interaction.LocalUnarmedMelee.GuardBroken -= OnGuardBroken;
            TableHolePower.Impact -= OnTableHoleImpact;
            HungryHoleCue.Swallowed -= OnHungryHoleSwallow;
            HungryHoleCue.AteHole -= OnHungryHoleAteHole;
            HungryHoleCue.Grew -= OnHungryHoleGrew;
        }

        // Everyone feels it: the table shook for both.
        private void OnTableHoleImpact(Vector3 point, int owner) => Play(GameFeelEvent.TableHoleImpact, -1, point);

        // Intensity from the bite's size, never quite nothing for a small one.
        private void OnHungryHoleSwallow(int owner, Vector3 point, float size) =>
            Play(GameFeelEvent.HungryHoleSwallow, owner, point, Mathf.Lerp(0.4f, 1f, size));
        private void OnHungryHoleAteHole(int eater, int eaten, Vector3 point) => Play(GameFeelEvent.HungryHoleAteHole, eater, point);
        // A growth of half a ball radius plays at full intensity.
        private void OnHungryHoleGrew(int owner, Vector3 point, float growth)
        {
            if (growth > 0f) Play(GameFeelEvent.HungryHoleGrow, owner, point, Mathf.Clamp(growth * 2f, 0.3f, 1.5f));
        }

        // The victim's moment (its screen for the HUD text), scaled by how
        // hard the blow was.
        private void OnPlayerHit(int attacker, int victim, Vector3 point, float strength) =>
            Play(GameFeelEvent.PlayerHit, victim, point, strength);

        private void OnKnockedOut(int victim, Vector3 point) => Play(GameFeelEvent.PlayerKnockedOut, victim, point);
        private void OnGuardBroken(int player, Vector3 point) => Play(GameFeelEvent.GuardBroken, player, point);

        private void Update()
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            bool over = rules != null && rules.MatchStarted && rules.GameOver;
            if (over && !wonPlayed) Play(GameFeelEvent.MatchWon, rules.Winner, Vector3.zero);
            wonPlayed = over;
        }

        private int Shooter => lastShooter >= 0 ? lastShooter : PoolMatchRules.Instance != null ? PoolMatchRules.Instance.CurrentPlayer : -1;

        private void OnPocketed(PoolBall ball, PoolPocket pocket)
        {
            Vector3 at = pocket != null ? pocket.transform.position : ball.transform.position;
            PoolMatchRules rules = PoolMatchRules.Instance;
            // Speed Pool: both shoot at once, the pot is about its owner.
            int shooter = rules != null && rules.IsSimultaneous ? rules.PotOwner(ball) : Shooter;
            if (ball.IsCueBall) { Play(GameFeelEvent.CueBallPotted, shooter, at); return; }
            // The celebration only for one of the shooter's own balls.
            PoolMatchRules.PotMeaning meaning = rules != null ? rules.MeaningOfPot(ball) : PoolMatchRules.PotMeaning.Good;
            if (meaning == PoolMatchRules.PotMeaning.Good) Play(GameFeelEvent.BallPotted, shooter, at);
            else if (meaning == PoolMatchRules.PotMeaning.Opponent) Play(GameFeelEvent.OpponentBallPotted, shooter, at);
        }

        private void OnFoul() => Play(GameFeelEvent.Foul, Shooter, Vector3.zero);
        private void OnPower(int player) => Play(GameFeelEvent.PowerGranted, player, Vector3.zero);

        // The launcher's moment, then the target's for an Attack (sent at
        // launch even when its effect waits for the target's turn: that's
        // when the target learns it's coming).
        private void OnPowerActivated(int player, PoolPower power, bool waitsForOpponentTurn)
        {
            Play(GameFeelEvent.PowerActivated, player, Vector3.zero);
            if (power != null && power.TargetsOpponent) Play(GameFeelEvent.PowerReceived, 1 - player, Vector3.zero);
        }

        private void OnPowerRefused(int player, PowerRefusal reason) => Play(GameFeelEvent.PowerRefused, player, Vector3.zero);
        private void OnBlast(PoolBall ball, int player) => Play(GameFeelEvent.BallBlasted, player, ball != null ? ball.transform.position : Vector3.zero);
        private void OnTurn(int player) => Play(GameFeelEvent.TurnStart, player, Vector3.zero);

        private void OnShot(int player, float charge)
        {
            lastShooter = player;
            Play(GameFeelEvent.Shot, player, Vector3.zero);
            if (charge >= GameFeelSettings.Instance.fullPowerThreshold) Play(GameFeelEvent.FullPowerShot, player, Vector3.zero);
        }

        // Also callable from code for a moment that has no event yet.
        public void Play(GameFeelEvent gameEvent, int player, Vector3 position, float intensityScale = 1f)
        {
            GameFeelSettings cfg = GameFeelSettings.Instance;
            if (!cfg.enabled) return;
            foreach (GameFeelEntry e in cfg.entries)
            {
                if (e == null || e.gameEvent != gameEvent || e.feedback == null) continue;
                MMF_Player p = InstanceOf(e.feedback);
                EventPlayer = player;
                p.transform.position = position;
                p.PlayFeedbacks(position, e.intensity * intensityScale);
            }
        }

        private MMF_Player InstanceOf(MMF_Player prefab)
        {
            if (instances.TryGetValue(prefab, out MMF_Player p) && p != null) return p;
            p = Instantiate(prefab, transform);
            p.name = prefab.name;
            instances[prefab] = p;
            return p;
        }

        // Turns a target choice of our feedbacks into a player index (-1 =
        // both halves of the screen).
        public static int Resolve(FeelTarget target) => target switch
        {
            FeelTarget.EventPlayer => EventPlayer,
            FeelTarget.TurnPlayer => PoolMatchRules.Instance != null ? PoolMatchRules.Instance.CurrentPlayer : -1,
            FeelTarget.Player1 => 0,
            FeelTarget.Player2 => 1,
            _ => -1,
        };
    }

    public enum FeelTarget { EventPlayer, TurnPlayer, Player1, Player2, Everyone }
}
