#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>The day stands still at <see cref="Hour"/>: for a scene with one time of day, or for scrubbing in the lab.</summary>
    [Serializable]
    public sealed class FixedHourDayDriver : IDayClockDriver
    {
        [Range(0f, 24f)] public float Hour = 12f;

        public float HourAt(in DayClockInput input) => Hour;
    }
}
