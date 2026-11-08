#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>Which minigame a skill check plays (hazards-and-boons.md, B3, the kinds).</summary>
    public enum SkillCheckKind
    {
        /// <summary>Press while the needle is in the band.</summary>
        PulsingGauge = 0,

        /// <summary>Press a row of directions in order; a wrong one restarts the row.</summary>
        Combo = 1
    }
}
