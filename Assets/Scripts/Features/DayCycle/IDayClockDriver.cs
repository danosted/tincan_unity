#nullable enable
namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// What drives the day: turns the clock's seconds into an hour of the day. Drivers plug into <see cref="DayClock"/>
    /// (a fixed cycle, a fixed hour; later the voyage or the weather) without the clock knowing which one runs. A driver
    /// that is plain data is <c>[Serializable]</c> and picked on <see cref="DayCycleConfig"/>. Plan: .docs/plans/day-cycle.md.
    /// </summary>
    public interface IDayClockDriver
    {
        /// <summary>The hour of the day (any value; the clock wraps it into 0..24) at <paramref name="seconds"/> on the
        /// clock's time source.</summary>
        float HourAt(double seconds);
    }
}
