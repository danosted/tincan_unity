#nullable enable
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// Pure: blends a look toward a weather's look by its weight, then adds the flash. The sun's direction, the fog's
    /// base height and falloff and the whiteout stay the look's own; only light and air change.
    /// </summary>
    public static class WeatherLookProcessor
    {
        public static LookSettings Apply(LookSettings look, in WeatherLook weather, float weight, float flash)
        {
            float w = Mathf.Clamp01(weight);
            float f = Mathf.Clamp01(flash);
            if (w <= 0f && f <= 0f) return look;

            look.SunIntensity *= Mathf.Lerp(1f, weather.SunScale, w);
            look.SunColour = Color.Lerp(look.SunColour, weather.SunColour, w);
            look.AmbientSky = Color.Lerp(look.AmbientSky, weather.AmbientSky, w) + weather.FlashColour * f;
            look.AmbientHorizon = Color.Lerp(look.AmbientHorizon, weather.AmbientHorizon, w) + weather.FlashColour * f;
            look.AmbientGround = Color.Lerp(look.AmbientGround, weather.AmbientGround, w) + weather.FlashColour * (f * 0.5f);
            look.SunwardFog = Color.Lerp(look.SunwardFog, weather.SunwardFog, w);
            look.AwayFog = Color.Lerp(look.AwayFog, weather.AwayFog, w);
            look.FogDensity = Mathf.Lerp(look.FogDensity, weather.FogDensity, w);
            look.HazeDensity = Mathf.Lerp(look.HazeDensity, weather.HazeDensity, w);
            look.MaxFog = Mathf.Lerp(look.MaxFog, weather.MaxFog, w);
            return look;
        }
    }
}
