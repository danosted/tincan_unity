#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>A whole day every <see cref="CycleSeconds"/>, from <see cref="StartHour"/> at second 0.</summary>
    [Serializable]
    public sealed class FixedCycleDayDriver : IDayClockDriver
    {
        [Tooltip("Real seconds for one whole day, night included.")]
        [Min(1f)] public float CycleSeconds = 300f;

        [Tooltip("The hour at second 0. 7: the blue hour before sunrise at the default sun path.")]
        [Range(0f, 24f)] public float StartHour = 7f;

        public float HourAt(in DayClockInput input) =>
            (float)(StartHour + 24.0 * input.Seconds / Math.Max(CycleSeconds, 1f));
    }
}
