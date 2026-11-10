#nullable enable
namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>Server: starts challenges and keeps their score. One runs at a time.</summary>
    public interface ISkillChallenges
    {
        /// <summary>The challenge running, or null.</summary>
        SkillChallengeDefinition? Running { get; }

        /// <summary>Starts it on the ship with stations; false (and a log line) while one runs or when no station it names is aboard.</summary>
        bool TryStart(SkillChallengeDefinition challenge);

        /// <summary>Drops the running challenge without a grade (a new session).</summary>
        void Cancel();

        /// <summary>Challenges that ended with this grade since the host started.</summary>
        int Count(SkillCheckGrade grade);
    }
}
