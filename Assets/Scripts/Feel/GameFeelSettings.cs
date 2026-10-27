using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // The moments of a match that can trigger a Feel sequence.
    public enum GameFeelEvent
    {
        BallPotted,       // one of the shooter's balls goes in (see PoolMatchRules.MeaningOfPot)
        CueBallPotted,    // the cue ball goes in
        Foul,
        Shot,             // any shot
        FullPowerShot,    // a shot charged past the threshold
        PowerGranted,     // a power picked up
        BallBlasted,      // "BOUM !" (ball blast power)
        TurnStart,        // the turn passes to a player
        MatchWon,
        PlayerHit,        // a punch or a kick lands on a player (intensity × the blow's strength)
        PlayerKnockedOut, // the hit gauge is full: K.O. (R&D combat)
        GuardBroken,      // a player's guard breaks (R&D combat)
        TableHoleImpact,  // a meteor of the Trou power hits the table (rumble, shake…)
        OpponentBallPotted, // a ball of the opponent's group goes in (8-ball): nothing to cheer
        PowerActivated,   // a power launched (the launcher's moment)
        PowerReceived,    // an Attack power sent at a player (the target's moment, at launch)
        PowerRefused,     // the power button pressed but nothing launched (wrong moment)
        HungryHoleSwallow, // Hungry Hole: a hole swallows a ball or a prop (intensity × the bite's size)
        HungryHoleAteHole, // Hungry Hole: a hole eats the other one (the eater's moment)
        HungryHoleGrow,    // Hungry Hole: a hole grows (intensity × the growth; not when it shrinks)
        SnakeGrow,         // Snake Pool: a ball joins a player's chain (that player's moment)
        SnakeCut,          // Snake Pool: a chain is cut (the attacker's moment; intensity × the links lost)
        PowerWashSplash,   // Power Wash Pool: a pot washes a disc around the pocket (the washer's moment; the 8's splash is stronger)
        // New moments go at the end: assets store the number.
    }

    [Serializable]
    public class GameFeelEntry
    {
        [Tooltip("Le moment de la partie.")]
        public GameFeelEvent gameEvent;
        [Tooltip("La séquence Feel jouée (un prefab avec un MMF Player). Créée dans Text FX Studio ou à la main.")]
        public MMF_Player feedback;
        [Tooltip("Intensité passée à la séquence (1 = normale).")]
        [Range(0f, 3f)] public float intensity = 1f;
    }

    // Which Feel sequence (MMF_Player prefab) plays at which moment. The game
    // only says "foul, player 2, here" (GameFeel); the sequence is built in
    // Feel's inspector, with our HUD text and camera feedbacks.
    [CreateAssetMenu(fileName = "GameFeelSettings", menuName = "Pool/Game Feel Settings")]
    public class GameFeelSettings : ScriptableObject
    {
        [Tooltip("Coupe toutes les séquences Feel de la partie d'un coup.")]
        public bool enabled = true;
        [Tooltip("Charge au-dessus de laquelle un tir compte comme « tir plein » (0..1).")]
        [Range(0f, 1f)] public float fullPowerThreshold = 0.95f;
        [Tooltip("Une ligne par moment ; plusieurs lignes pour le même moment jouent toutes.")]
        public List<GameFeelEntry> entries = new List<GameFeelEntry>();

        private static GameFeelSettings instance;
        public static GameFeelSettings Instance =>
            instance != null ? instance : instance = Pool.PoolSettingsLoader.LoadOrDefault<GameFeelSettings>("GameFeelSettings");
    }
}
