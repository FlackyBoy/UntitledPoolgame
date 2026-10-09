using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace UntitledPoolGame.Core
{
    // The moments of a match that can trigger a Feel sequence.
    public enum GameFeelEvent
    {
        BallPotted,       // a numbered ball goes in
        CueBallPotted,    // the cue ball goes in
        Foul,
        Shot,             // any shot
        FullPowerShot,    // a shot charged past the threshold
        PowerGranted,     // a power picked up
        BallBlasted,      // "BOUM !" (ball blast power)
        TurnStart,        // the turn passes to a player
        MatchWon,
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

    // Which Feel sequence (MMF_Player prefab) plays at which moment of a
    // match — the game only says "foul, player 2, here" (GameFeel), the
    // designer builds what it feels like in Feel's inspector: sound, pad
    // rumble, freeze frame, particles, plus our own feedbacks for the HUD text
    // (MMF_PoolHudText) and the camera (MMF_PoolScreenJuice).
    // Assets/Resources/GameFeelSettings, created by Tools > Pool > Ensure
    // Config Assets Exist. Tooltips in French: read by the designer.
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
