#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// The light and sky at one height of the sun (<see cref="Elevation"/>), one key of a look's day
    /// (<see cref="LookProfile.Day"/>). <see cref="DayLightProcessor"/> blends the two keys around the sun's height and
    /// lays the result over the look; what a key does not hold (the fog's shape, the whiteout) stays the look's.
    /// Plan: .docs/plans/day-cycle.md.
    /// </summary>
    [Serializable]
    public struct DayLightKey
    {
        [Tooltip("The sun's height in degrees this key is for; negative below the horizon (-6 ends the blue hour).")]
        public float Elevation;

        [Header("Sun")]
        public Color SunColour;
        [Min(0f)] public float SunIntensity;

        [Header("Ambient")]
        public Color AmbientSky;
        public Color AmbientHorizon;
        public Color AmbientGround;

        [Header("Air")]
        public Color SunwardFog;
        public Color AwayFog;

        [Header("Sky")]
        [Tooltip("The sky straight up.")]
        public Color SkyZenith;
        [Tooltip("The pink band low in the sky opposite a low sun (the belt of Venus); alpha is how strong.")]
        public Color SkyBelt;

        [Header("Night")]
        [Tooltip("What surfaces with fixed colours (the stylized clouds) darken toward; alpha is how far (0 by day).")]
        public Color NightShade;

        [Tooltip("How bright the stars in the painted sky are, 0 (none, by day) .. 1 (the full night sky).")]
        [Range(0f, 1f)] public float Stars;
    }
}
