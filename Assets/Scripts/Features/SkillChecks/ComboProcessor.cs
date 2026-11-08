#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Domain, pure: the button combo. A row of directions to press in order. A right press moves on; a wrong one
    /// restarts the row instead of failing it (and counts as the row's first step if it is one). Finished without a
    /// wrong press is Perfect, finished at all is Good, not finished in time is a Miss.
    /// </summary>
    public class ComboProcessor
    {
        private const int Directions = 4;

        /// <summary>A row of <paramref name="length"/> directions, never the same one twice in a row (so a slip shows).</summary>
        public ComboDirection[] Sequence(Random random, int length)
        {
            var sequence = new ComboDirection[Math.Max(1, length)];
            for (int i = 0; i < sequence.Length; i++)
            {
                int pick = random.Next(i == 0 ? Directions : Directions - 1);
                if (i > 0 && pick >= (int)sequence[i - 1]) pick++;
                sequence[i] = (ComboDirection)pick;
            }
            return sequence;
        }

        /// <summary>How many steps are done after this press, from <paramref name="done"/> before it.</summary>
        public int Advance(IReadOnlyList<ComboDirection> sequence, int done, ComboDirection pressed, out bool wrong)
        {
            wrong = done >= sequence.Count || sequence[done] != pressed;
            if (!wrong) return done + 1;
            return sequence.Count > 0 && sequence[0] == pressed ? 1 : 0;
        }

        public SkillCheckGrade Grade(bool finished, int wrongPresses) =>
            !finished ? SkillCheckGrade.Miss : wrongPresses == 0 ? SkillCheckGrade.Perfect : SkillCheckGrade.Good;
    }
}
