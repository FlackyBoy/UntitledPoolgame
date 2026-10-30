using System.Collections.Generic;
using UnityEngine;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // The score of a Boogie Night, carried over to the level like
    // GameSession: how many matches, who won how many. Each match won is a
    // point; the night is over once the leader can't be caught up, and a tie
    // after the last match calls for one more.
    public static class BoogieNight
    {
        public static bool Active { get; private set; }
        public static int Rounds { get; private set; }
        public static int Played { get; private set; }
        private static readonly int[] wins = new int[2];

        public static void Begin(int rounds)
        {
            Active = true;
            Rounds = Mathf.Max(1, rounds);
            Played = 0;
            wins[0] = wins[1] = 0;
        }

        public static void End() => Active = false;

        public static int Wins(int player) => wins[Mathf.Clamp(player, 0, 1)];

        // winner < 0: a match nobody won, played all the same.
        public static void Record(int winner)
        {
            if (!Active) return;
            Played++;
            if (winner == 0 || winner == 1) wins[winner]++;
        }

        // -1 while tied.
        public static int Leader => wins[0] > wins[1] ? 0 : wins[1] > wins[0] ? 1 : -1;

        public static bool Decided
        {
            get
            {
                int leader = Leader;
                return Active && leader >= 0 && wins[leader] - wins[1 - leader] > Mathf.Max(0, Rounds - Played);
            }
        }

        // The next match is played past the chosen count: the tiebreak.
        public static bool FinalRound => Played >= Rounds;

        // The wheel's slices: the modes of the settings this level can play
        // (golf needs a hole placed in it).
        public static List<PoolGameMode> Slices(BoogieNightSettings s)
        {
            var slices = new List<PoolGameMode>();
            if (s.modes == null) return slices;
            bool course = PlayAnywhereRuleSet.HasCourse;
            foreach (PoolGameMode m in s.modes)
                if (m != PoolGameMode.PlayAnywhere || course) slices.Add(m);
            return slices;
        }

        // What the wheel lands on: a slice, a type the mode can be played
        // in, powers or not.
        public static PoolMatchSetup Draw(BoogieNightSettings s, List<PoolGameMode> slices, out int slice)
        {
            int count = slices.Count;
            slice = count > 0 ? Random.Range(0, count) : -1;
            PoolGameMode mode = slice >= 0 ? slices[slice] : PoolGameMode.EightBall;
            bool turns = PoolMatchSetup.Supports(mode, PoolMatchType.TurnBased);
            bool speedPool = PoolMatchSetup.Supports(mode, PoolMatchType.SpeedPool);
            PoolMatchType type = turns && speedPool
                ? (Random.value < s.speedPoolChance ? PoolMatchType.SpeedPool : PoolMatchType.TurnBased)
                : speedPool ? PoolMatchType.SpeedPool : PoolMatchType.TurnBased;
            return new PoolMatchSetup(mode, type, Random.value < s.powersChance, s.fourteenOneTarget);
        }
    }
}
