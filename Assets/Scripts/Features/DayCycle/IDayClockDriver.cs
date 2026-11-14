#nullable enable
namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// What drives the day: turns the clock's input (seconds, the session's progress) into an hour of the day. Drivers
    /// plug into <see cref="DayClock"/> (a fixed cycle, a fixed hour, the voyage) without the clock knowing which one
    /// runs. A driver is plain data, <c>[Serializable]</c>, picked on <see cref="DayCycleConfig"/>.
    /// Plan: .docs/plans/day-cycle.md.
    /// </summary>
    public interface IDayClockDriver
    {
        /// <summary>The hour of the day (any value; the clock wraps it into 0..24) for <paramref name="input"/>.</summary>
        float HourAt(in DayClockInput input);
    }
}
