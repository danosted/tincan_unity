#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// One look's light and air (.docs/ART_DIRECTION.md, "Light and shading"): the sun, the ambient light, height fog in
    /// bands (densest at the cloud sea, thinning upward) tinted by the angle to the sun, distance haze, and the whiteout
    /// inside a cloud. Colours come from the palette (TinCan > Dev > Look > Build Atmosphere). Plain data, so
    /// <see cref="AtmosphereProcessor"/> is testable without assets.
    /// </summary>
    [Serializable]
    public struct LookSettings
    {
        [Header("Sun")]
        public Color SunColour;
        [Min(0f)] public float SunIntensity;
        [Tooltip("The sun's rotation (pitch, yaw). Golden late afternoon: low.")]
        public Vector2 SunAngles;

        [Header("Ambient (three colours: sky above, horizon, below)")]
        public Color AmbientSky;
        public Color AmbientHorizon;
        public Color AmbientGround;

        [Header("Fog")]
        [Tooltip("Fog colour looking toward the sun.")]
        public Color SunwardFog;
        [Tooltip("Fog colour looking away from the sun.")]
        public Color AwayFog;
        [Tooltip("How tightly the sunward colour gathers around the sun (higher is tighter).")]
        [Min(0.1f)] public float SunwardFocus;
        [Tooltip("World height (m) where the fog is densest: the top of the cloud sea.")]
        public float FogBaseHeight;
        [Tooltip("Fog per metre at the base height.")]
        [Min(0f)] public float FogDensity;
        [Tooltip("How fast the fog thins with height, per metre above the base.")]
        [Min(0.0001f)] public float FogFalloff;
        [Tooltip("Haze per metre at any height: far things fade toward the fog colour.")]
        [Min(0f)] public float HazeDensity;
        [Tooltip("The most the fog may cover, so far silhouettes still read.")]
        [Range(0f, 1f)] public float MaxFog;

        [Header("Inside a cloud")]
        public Color WhiteoutColour;
        [Tooltip("Fog per metre when fully inside the cloud deck.")]
        [Min(0f)] public float WhiteoutDensity;

        public static LookSettings Default => new()
        {
            SunColour = new Color(1f, 0.9f, 0.75f),
            SunIntensity = 1.1f,
            SunAngles = new Vector2(28f, 60f),
            AmbientSky = new Color(0.56f, 0.72f, 0.85f),
            AmbientHorizon = new Color(0.91f, 0.89f, 0.82f),
            AmbientGround = new Color(0.66f, 0.65f, 0.78f),
            SunwardFog = new Color(0.91f, 0.82f, 0.62f),
            AwayFog = new Color(0.66f, 0.65f, 0.78f),
            SunwardFocus = 3f,
            FogBaseHeight = 10f,
            FogDensity = 0.0015f,
            FogFalloff = 0.01f,
            HazeDensity = 0.0002f,
            MaxFog = 0.85f,
            WhiteoutColour = new Color(0.98f, 0.96f, 0.93f),
            WhiteoutDensity = 0.08f,
        };
    }
}
