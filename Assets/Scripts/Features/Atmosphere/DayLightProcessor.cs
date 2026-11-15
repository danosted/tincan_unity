#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// Pure: the day's light at a height of the sun. <see cref="Evaluate"/> blends the two keys around the elevation
    /// (keys in any order; below the lowest or above the highest, that key); <see cref="Apply"/> lays the light over a
    /// look, so <see cref="AtmosphereProcessor"/> takes it as any look. Plan: .docs/plans/day-cycle.md.
    /// </summary>
    public sealed class DayLightProcessor
    {
        public DayLightKey Evaluate(IReadOnlyList<DayLightKey> keys, float elevation)
        {
            if (keys.Count == 0) return default;

            int below = -1, above = -1;
            for (int i = 0; i < keys.Count; i++)
            {
                float at = keys[i].Elevation;
                if (at <= elevation && (below < 0 || at > keys[below].Elevation)) below = i;
                if (at >= elevation && (above < 0 || at < keys[above].Elevation)) above = i;
            }

            if (below < 0) return keys[above];
            if (above < 0 || above == below) return keys[below];

            var from = keys[below];
            var to = keys[above];
            float t = Mathf.InverseLerp(from.Elevation, to.Elevation, elevation);
            return new DayLightKey
            {
                Elevation = elevation,
                SunColour = Color.Lerp(from.SunColour, to.SunColour, t),
                SunIntensity = Mathf.Lerp(from.SunIntensity, to.SunIntensity, t),
                AmbientSky = Color.Lerp(from.AmbientSky, to.AmbientSky, t),
                AmbientHorizon = Color.Lerp(from.AmbientHorizon, to.AmbientHorizon, t),
                AmbientGround = Color.Lerp(from.AmbientGround, to.AmbientGround, t),
                SunwardFog = Color.Lerp(from.SunwardFog, to.SunwardFog, t),
                AwayFog = Color.Lerp(from.AwayFog, to.AwayFog, t),
                SkyZenith = Color.Lerp(from.SkyZenith, to.SkyZenith, t),
                SkyBelt = Color.Lerp(from.SkyBelt, to.SkyBelt, t),
                NightShade = Color.Lerp(from.NightShade, to.NightShade, t),
                Stars = Mathf.Lerp(from.Stars, to.Stars, t),
            };
        }

        /// <summary>The look with the day's light in place of its own sun, ambient and fog colours.</summary>
        public LookSettings Apply(in LookSettings look, in DayLightKey light)
        {
            var lit = look;
            lit.SunColour = light.SunColour;
            lit.SunIntensity = light.SunIntensity;
            lit.AmbientSky = light.AmbientSky;
            lit.AmbientHorizon = light.AmbientHorizon;
            lit.AmbientGround = light.AmbientGround;
            lit.SunwardFog = light.SunwardFog;
            lit.AwayFog = light.AwayFog;
            return lit;
        }
    }
}
