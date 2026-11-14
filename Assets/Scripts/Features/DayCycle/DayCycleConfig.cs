#nullable enable
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// The day: which driver runs it (picked in the Inspector; Fixed Hour to stop and scrub) and the sun's path. Read
    /// by the cloud lab in edit mode (DevTools/Editor/CloudLabDay); built by TinCan > Dev > Look > Build Atmosphere.
    /// Plan: .docs/plans/day-cycle.md.
    /// </summary>
    [CreateAssetMenu(fileName = "DayCycleConfig", menuName = "TinCan/Environment/Day Cycle Config")]
    public class DayCycleConfig : ScriptableObject
    {
        [SerializeReference, DayClockDriverPicker]
        public IDayClockDriver? Driver = new FixedCycleDayDriver();

        public SunPath Path = SunPath.Default;

        /// <summary>The driver in use: the picked one, or a fixed noon when none is picked.</summary>
        public IDayClockDriver ActiveDriver => Driver ?? new FixedHourDayDriver();
    }
}
