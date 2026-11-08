#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>Server: a grade a player's peer sent back for a check.</summary>
    public readonly struct SkillCheckResult
    {
        public readonly ulong ClientId;
        public readonly int CheckId;
        public readonly SkillCheckGrade Grade;

        public SkillCheckResult(ulong clientId, int checkId, SkillCheckGrade grade)
        {
            ClientId = clientId;
            CheckId = checkId;
            Grade = grade;
        }
    }
}
