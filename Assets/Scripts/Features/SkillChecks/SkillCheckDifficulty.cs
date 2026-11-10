#nullable enable
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Domain Layer, pure: a check made harder by a level (a challenge's escalation round; level 0 is the check as
    /// authored). Each level narrows the gauge's band, speeds its needle and adds a step to the combo.
    /// </summary>
    public static class SkillCheckDifficulty
    {
        public const float NarrowerPerLevel = 0.75f;
        public const float FasterPerLevel = 0.85f;
        public const int MaxComboLength = 8;

        public static float Width(float width, int level) => Mathf.Max(0.01f, width * Mathf.Pow(NarrowerPerLevel, level));

        public static float Period(float period, int level) => Mathf.Max(0.3f, period * Mathf.Pow(FasterPerLevel, level));

        public static int ComboLength(int length, int level) => Mathf.Min(MaxComboLength, length + level);
    }
}
