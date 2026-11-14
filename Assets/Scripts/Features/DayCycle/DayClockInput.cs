#nullable enable
namespace TinCan.Features.DayCycle
{
    /// <summary>What a driver may read to set the hour: the clock's seconds, and how far along the play session is when
    /// one is under way (null otherwise, and always in the lab).</summary>
    public readonly struct DayClockInput
    {
        public DayClockInput(double seconds, float? sessionProgress = null)
        {
            Seconds = seconds;
            SessionProgress = sessionProgress;
        }

        public double Seconds { get; }

        /// <summary>0 at the session's start, 1 at its end; null without a session under way.</summary>
        public float? SessionProgress { get; }
    }
}
