#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.SkillChecks
{
    /// <summary>This peer: the check this player is doing, for its view and scenarios.</summary>
    public interface ISkillCheckRunner
    {
        bool IsRunning { get; }

        /// <summary>The check running, or the one that just ended while its grade shows.</summary>
        SkillCheckDefinition? Definition { get; }

        /// <summary>The pulsing gauge: the needle, 0 to 1.</summary>
        float Needle { get; }

        /// <summary>The pulsing gauge: where to press.</summary>
        PulsingGaugeBand Band { get; }

        /// <summary>The button combo: the directions to press, in order.</summary>
        IReadOnlyList<ComboDirection> Combo { get; }

        /// <summary>The button combo: how many of them are pressed.</summary>
        int ComboDone { get; }

        /// <summary>Seconds left to finish.</summary>
        float SecondsLeft { get; }

        /// <summary>The grade just given, while it shows after the check; null otherwise.</summary>
        SkillCheckGrade? Shown { get; }
    }
}
