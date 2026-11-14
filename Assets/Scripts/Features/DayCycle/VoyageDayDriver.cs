#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// A day that lasts one voyage (developer, 2026-11-14): casting off in the blue hour before sunrise, arriving in the
    /// blue hour after sunset, the hour following how far along the route the ship is, so there is no night while
    /// sailing. Waiting for a voyage, the day stands at <see cref="CastOffHour"/>. Reads the session's progress (the
    /// Voyage feature's <c>ISessionProgress</c>); without one (the lab, a scene without Voyage) it stands at the cast-off.
    /// </summary>
    [Serializable]
    public sealed class VoyageDayDriver : IDayClockDriver
    {
        [Tooltip("The hour at cast-off. 7: the blue hour before sunrise at the default sun path.")]
        [Range(0f, 24f)] public float CastOffHour = 7f;

        [Tooltip("The hour on arrival. 17: the blue hour after sunset at the default sun path.")]
        [Range(0f, 24f)] public float ArrivalHour = 17f;

        public float HourAt(in DayClockInput input) =>
            input.SessionProgress is { } progress ? Mathf.Lerp(CastOffHour, ArrivalHour, Mathf.Clamp01(progress)) : CastOffHour;
    }
}
