#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// What a weather does to the look at its full weight (a storm at its heart): the sun dimmed and tinted, the ambient
    /// and the fog toward their own colours, the air thicker. The look's day light comes first; the weather blends it
    /// toward these by its weight (<see cref="WeatherLookProcessor"/>). A flash (lightning) adds its colour on top.
    /// Plan: .docs/plans/storm-crossing.md (ST1).
    /// </summary>
    [Serializable]
    public struct WeatherLook
    {
        [Header("Sun")]
        [Tooltip("The sun's intensity at full weight, as a share of the look's.")]
        [Range(0f, 1f)] public float SunScale;
        [Tooltip("The sun's colour at full weight.")]
        public Color SunColour;

        [Header("Ambient")]
        public Color AmbientSky;
        public Color AmbientHorizon;
        public Color AmbientGround;

        [Header("Fog")]
        public Color SunwardFog;
        public Color AwayFog;
        [Min(0f)] public float FogDensity;
        [Min(0f)] public float HazeDensity;
        [Range(0f, 1f)] public float MaxFog;

        [Header("Clouds")]
        [Tooltip("What the clouds (the drawn sea and the painted sky) are multiplied toward at full weight: their shadow sides darken too.")]
        public Color CloudShade;

        [Header("Flash")]
        [Tooltip("What a flash (lightning) adds to the ambient at its peak.")]
        public Color FlashColour;
    }

    /// <summary>
    /// A weather the atmosphere blends in (the storm): its look, how much of it shows here (0 none, 1 all), and a flash
    /// (0 to 1) added on top. Read every frame on every peer with a view; presentation only.
    /// </summary>
    public interface IAtmosphereWeather
    {
        float Weight { get; }
        WeatherLook Look { get; }
        float Flash { get; }
    }
}
