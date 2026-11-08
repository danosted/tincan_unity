#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>How well a skill check went. A Miss gives nothing and costs nothing.</summary>
    public enum SkillCheckGrade
    {
        Miss = 0,
        Good = 1,
        Perfect = 2
    }
}
