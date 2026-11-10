#nullable enable
namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>Server: a challenge ended with this grade.</summary>
    public readonly struct SkillChallengeEndedEvent
    {
        public readonly string Challenge;
        public readonly SkillCheckGrade Grade;

        public SkillChallengeEndedEvent(string challenge, SkillCheckGrade grade)
        {
            Challenge = challenge;
            Grade = grade;
        }
    }
}
