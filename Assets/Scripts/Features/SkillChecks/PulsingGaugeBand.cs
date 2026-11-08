#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>Where the pulsing gauge's band sits (0 to 1 along the gauge) and how wide its Good and Perfect parts are.</summary>
    public readonly struct PulsingGaugeBand
    {
        public readonly float Center;
        public readonly float GoodWidth;
        public readonly float PerfectWidth;

        public PulsingGaugeBand(float center, float goodWidth, float perfectWidth)
        {
            Center = center;
            GoodWidth = goodWidth;
            PerfectWidth = perfectWidth;
        }
    }
}
