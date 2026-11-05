using System;
using System.Collections.Generic;
using System.Text;

namespace UntitledPoolGame.Pool
{
    // What a throw did to the frame (BowlingPoolRuleSet acts on it).
    public struct BowlingRoll
    {
        public bool Strike;
        public bool Spare;
        public bool FrameOver;
        // All ten pins go back up before the next throw.
        public bool Rerack;
    }

    // One player's bowling score, with the real bonuses: a strike adds the next
    // two throws, a spare the next one. The last frame has no bonus: its strike
    // or spare earns extra throws instead. No Unity in here: the rules can be
    // run outside the engine.
    public class BowlingScoreSheet
    {
        public const int PinCount = 10;

        private readonly int frames;
        private readonly List<int> rolls = new List<int>();
        // Index in rolls of each frame's first throw.
        private readonly List<int> frameStarts = new List<int>();
        private int rollsInFrame;
        private int standing = PinCount;
        // No throw yet at this rack: all ten down is then a strike, not a
        // spare. Not the same as ten standing: a first throw can miss them all.
        private bool freshRack = true;

        public BowlingScoreSheet(int frames)
        {
            this.frames = Math.Max(1, frames);
        }

        public int Frames => frames;
        // 0-based, Frames once finished.
        public int Frame { get; private set; }
        public int RollCount => rolls.Count;
        public bool Finished => Frame >= frames;

        public BowlingRoll AddRoll(int pins)
        {
            if (Finished) return new BowlingRoll { FrameOver = true, Rerack = true };

            pins = Math.Clamp(pins, 0, standing);
            if (rollsInFrame == 0) frameStarts.Add(rolls.Count);
            rolls.Add(pins);
            rollsInFrame++;
            standing -= pins;

            var roll = new BowlingRoll
            {
                Strike = freshRack && standing == 0,
                Spare = !freshRack && standing == 0,
            };
            freshRack = false;
            roll.FrameOver = IsFrameOver(roll.Spare);
            roll.Rerack = roll.FrameOver || standing == 0;
            if (roll.Rerack)
            {
                standing = PinCount;
                freshRack = true;
            }
            if (roll.FrameOver)
            {
                Frame++;
                rollsInFrame = 0;
            }
            return roll;
        }

        private bool IsFrameOver(bool spare)
        {
            if (Frame < frames - 1) return standing == 0 || rollsInFrame == 2;
            // Last frame: a third throw only after a strike or a spare.
            if (rollsInFrame == 1) return false;
            if (rollsInFrame == 2) return rolls[frameStarts[Frame]] != PinCount && !spare;
            return true;
        }

        // Bonuses count as far as they've been thrown.
        public int Total
        {
            get
            {
                int total = 0;
                for (int frame = 0; frame < frameStarts.Count; frame++)
                {
                    int i = frameStarts[frame];
                    if (frame == frames - 1)
                    {
                        for (int r = i; r < rolls.Count; r++) total += rolls[r];
                    }
                    else if (rolls[i] == PinCount) total += PinCount + RollAt(i + 1) + RollAt(i + 2);
                    else if (RollAt(i) + RollAt(i + 1) == PinCount) total += PinCount + RollAt(i + 2);
                    else total += RollAt(i) + RollAt(i + 1);
                }
                return total;
            }
        }

        private int RollAt(int index) => index < rolls.Count ? rolls[index] : 0;

        // "X  7/  9-  3", as on a bowling alley's screen.
        public string Describe()
        {
            var text = new StringBuilder();
            for (int frame = 0; frame < frameStarts.Count; frame++)
            {
                if (frame > 0) text.Append("  ");
                int end = frame + 1 < frameStarts.Count ? frameStarts[frame + 1] : rolls.Count;
                int up = PinCount;
                bool fresh = true;
                for (int r = frameStarts[frame]; r < end; r++)
                {
                    int pins = rolls[r];
                    if (pins == up) text.Append(fresh ? 'X' : '/');
                    else if (pins == 0) text.Append('-');
                    else text.Append(pins);
                    up -= pins;
                    fresh = up == 0;
                    if (fresh) up = PinCount;
                }
            }
            return text.ToString();
        }
    }
}
