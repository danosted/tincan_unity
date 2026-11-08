#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>This peer: the check this player is doing, for the gauge view and scenarios.</summary>
    public interface ISkillCheckRunner
    {
        bool IsRunning { get; }

        /// <summary>The check running, or the one that just ended while its grade shows.</summary>
        SkillCheckDefinition? Definition { get; }

        /// <summary>The needle, 0 to 1.</summary>
        float Needle { get; }

        PulsingGaugeBand Band { get; }

        /// <summary>Seconds left to press.</summary>
        float SecondsLeft { get; }

        /// <summary>The grade just given, while it shows after the check; null otherwise.</summary>
        SkillCheckGrade? Shown { get; }
    }
}
