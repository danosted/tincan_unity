#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>Server: opens skill checks and keeps their score.</summary>
    public interface ISkillChecks
    {
        /// <summary>Opens the host's check for this player; false (and a log line) while one is open or the host rests.</summary>
        bool TryOpen(ISkillCheckHost host, ulong clientId);

        /// <summary>Checks that ended with this grade since the host started.</summary>
        int Count(SkillCheckGrade grade);
    }
}
