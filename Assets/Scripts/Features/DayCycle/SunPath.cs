#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// Where the sun goes through the day: the latitude it is seen from and the season (the sun's declination). At
    /// 63 N in early November (-15) it rises in the south-east about 08:00, peaks at about 12 degrees at noon in the
    /// south and sets in the south-west about 16:00.
    /// </summary>
    [Serializable]
    public struct SunPath
    {
        [Tooltip("Degrees north. Higher keeps the sun lower and the winter days shorter (63: Trondheim).")]
        [Range(-89f, 89f)] public float Latitude;

        [Tooltip("The season, as the sun's declination in degrees: 0 at the equinoxes, -15 early November, -23.4 midwinter, +23.4 midsummer.")]
        [Range(-23.44f, 23.44f)] public float Declination;

        [Tooltip("The world yaw (degrees) that counts as north; 0: north is +Z, east +X.")]
        public float NorthYaw;

        public static SunPath Default => new() { Latitude = 63f, Declination = -15f, NorthYaw = 0f };
    }
}
