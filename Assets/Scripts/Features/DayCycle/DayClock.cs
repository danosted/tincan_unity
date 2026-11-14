#nullable enable
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// The headless day: seconds from whatever time source the caller has (the editor's clock in the lab; later synced
    /// network time, so every peer agrees) go through the driver to an hour, and through the sun's path to where the sun
    /// is. Knows nothing about light or rendering; the Atmosphere feature and the lab turn the sun into light. The
    /// driver can be swapped at any time. Plan: .docs/plans/day-cycle.md.
    /// </summary>
    public sealed class DayClock
    {
        private readonly SunPathProcessor _sunPath;

        public DayClock(IDayClockDriver driver, SunPath path, SunPathProcessor sunPath)
        {
            Driver = driver;
            Path = path;
            _sunPath = sunPath;
        }

        public IDayClockDriver Driver { get; set; }

        public SunPath Path { get; set; }

        /// <summary>The hour of the day for <paramref name="input"/>, 0..24.</summary>
        public float HourAt(in DayClockInput input) => Mathf.Repeat(Driver.HourAt(input), 24f);

        /// <summary>The hour of the day at <paramref name="seconds"/>, without a session.</summary>
        public float HourAt(double seconds) => HourAt(new DayClockInput(seconds));

        /// <summary>Where the sun is for <paramref name="input"/>.</summary>
        public SunPosition SunAt(in DayClockInput input) => _sunPath.Evaluate(Path, HourAt(input));

        /// <summary>Where the sun is at <paramref name="seconds"/>, without a session.</summary>
        public SunPosition SunAt(double seconds) => SunAt(new DayClockInput(seconds));
    }
}
