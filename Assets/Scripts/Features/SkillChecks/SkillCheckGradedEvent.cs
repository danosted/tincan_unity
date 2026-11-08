#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>Server: a skill check ended (a grade came back, or it ran out as a Miss). Bonus: the boost window added its effect.</summary>
    public readonly struct SkillCheckGradedEvent
    {
        public readonly string Check;
        public readonly SkillCheckGrade Grade;
        public readonly bool Bonus;

        public SkillCheckGradedEvent(string check, SkillCheckGrade grade, bool bonus)
        {
            Check = check;
            Grade = grade;
            Bonus = bonus;
        }
    }
}
