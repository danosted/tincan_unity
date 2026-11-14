#nullable enable
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>
    /// Pure: the solar position formula. The hour angle (15 degrees an hour from solar noon) with the latitude and the
    /// declination give the sun's direction in east-up-north, turned into the world by <see cref="SunPath.NorthYaw"/>.
    /// </summary>
    public sealed class SunPathProcessor
    {
        public SunPosition Evaluate(in SunPath path, float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            float latitude = path.Latitude * Mathf.Deg2Rad;
            float declination = path.Declination * Mathf.Deg2Rad;
            float hourAngle = (hour - 12f) * 15f * Mathf.Deg2Rad;

            float east = -Mathf.Cos(declination) * Mathf.Sin(hourAngle);
            float up = Mathf.Sin(latitude) * Mathf.Sin(declination) + Mathf.Cos(latitude) * Mathf.Cos(declination) * Mathf.Cos(hourAngle);
            float north = Mathf.Cos(latitude) * Mathf.Sin(declination) - Mathf.Sin(latitude) * Mathf.Cos(declination) * Mathf.Cos(hourAngle);

            var toSun = Quaternion.Euler(0f, path.NorthYaw, 0f) * new Vector3(east, up, north).normalized;
            float elevation = Mathf.Asin(Mathf.Clamp(up, -1f, 1f)) * Mathf.Rad2Deg;
            float azimuth = Mathf.Repeat(Mathf.Atan2(east, north) * Mathf.Rad2Deg, 360f);
            return new SunPosition(hour, elevation, azimuth, toSun);
        }

        /// <summary>The hour the sun passes <paramref name="elevation"/> degrees, in the morning or the afternoon; null
        /// when it never does that day (above its noon height, or below its midnight depth).</summary>
        public float? HourAtElevation(in SunPath path, float elevation, bool morning)
        {
            float latitude = path.Latitude * Mathf.Deg2Rad;
            float declination = path.Declination * Mathf.Deg2Rad;
            float cosHourAngle = (Mathf.Sin(elevation * Mathf.Deg2Rad) - Mathf.Sin(latitude) * Mathf.Sin(declination))
                / (Mathf.Cos(latitude) * Mathf.Cos(declination));
            if (cosHourAngle < -1f || cosHourAngle > 1f) return null;

            float hours = Mathf.Acos(cosHourAngle) * Mathf.Rad2Deg / 15f;
            return morning ? 12f - hours : 12f + hours;
        }

        /// <summary>The sun's height at solar noon, in degrees.</summary>
        public float NoonElevation(in SunPath path) => 90f - Mathf.Abs(path.Latitude - path.Declination);
    }
}
