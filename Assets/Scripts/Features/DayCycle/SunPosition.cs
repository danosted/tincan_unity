#nullable enable
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>The sun at one hour: its height above the horizon, its bearing, and the world direction toward it.</summary>
    public readonly struct SunPosition
    {
        public SunPosition(float hour, float elevation, float azimuth, Vector3 toSun)
        {
            Hour = hour;
            Elevation = elevation;
            Azimuth = azimuth;
            ToSun = toSun;
        }

        /// <summary>The hour of the day, 0..24.</summary>
        public float Hour { get; }

        /// <summary>Degrees above the horizon; negative below it (-6 ends the blue hour, -18 is full night).</summary>
        public float Elevation { get; }

        /// <summary>World bearing in degrees, clockwise from north (90 east, 180 south).</summary>
        public float Azimuth { get; }

        /// <summary>Unit world direction from the viewer toward the sun.</summary>
        public Vector3 ToSun { get; }

        /// <summary>The rotation of a directional light shining from the sun (its forward is the way the light travels).</summary>
        public Quaternion LightRotation => Quaternion.LookRotation(-ToSun, Mathf.Abs(ToSun.y) > 0.999f ? Vector3.forward : Vector3.up);
    }
}
