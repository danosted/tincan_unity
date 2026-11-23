#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.Atmosphere.Tests
{
    /// <summary>A weather blended into the look (<see cref="WeatherLookProcessor"/>).</summary>
    public class WeatherLookProcessorTests
    {
        private static readonly WeatherLook Storm = new()
        {
            SunScale = 0.2f,
            SunColour = new Color(0.5f, 0.55f, 0.7f),
            AmbientSky = new Color(0.2f, 0.2f, 0.3f),
            AmbientHorizon = new Color(0.25f, 0.2f, 0.3f),
            AmbientGround = new Color(0.15f, 0.15f, 0.2f),
            SunwardFog = new Color(0.3f, 0.3f, 0.4f),
            AwayFog = new Color(0.25f, 0.25f, 0.35f),
            FogDensity = 0.02f,
            HazeDensity = 0.004f,
            MaxFog = 0.95f,
            FlashColour = new Color(0.6f, 0.65f, 0.8f)
        };

        [Test]
        public void NoWeight_LeavesTheLookAlone()
        {
            var look = LookSettings.Default;

            var result = WeatherLookProcessor.Apply(look, Storm, 0f, 0f);

            Assert.That(result.SunIntensity, Is.EqualTo(look.SunIntensity));
            Assert.That(result.AmbientSky, Is.EqualTo(look.AmbientSky));
            Assert.That(result.FogDensity, Is.EqualTo(look.FogDensity));
        }

        [Test]
        public void FullWeight_IsTheWeathersLook_ButTheSunKeepsItsDirection()
        {
            var look = LookSettings.Default;

            var result = WeatherLookProcessor.Apply(look, Storm, 1f, 0f);

            Assert.That(result.SunIntensity, Is.EqualTo(look.SunIntensity * Storm.SunScale).Within(1e-5f));
            Assert.That(result.SunColour, Is.EqualTo(Storm.SunColour));
            Assert.That(result.AmbientSky, Is.EqualTo(Storm.AmbientSky));
            Assert.That(result.FogDensity, Is.EqualTo(Storm.FogDensity).Within(1e-6f));
            Assert.That(result.SunAngles, Is.EqualTo(look.SunAngles));
            Assert.That(result.FogBaseHeight, Is.EqualTo(look.FogBaseHeight));
        }

        [Test]
        public void HalfWeight_IsHalfway()
        {
            var look = LookSettings.Default;

            var result = WeatherLookProcessor.Apply(look, Storm, 0.5f, 0f);

            Assert.That(result.MaxFog, Is.EqualTo(Mathf.Lerp(look.MaxFog, Storm.MaxFog, 0.5f)).Within(1e-5f));
        }

        [Test]
        public void AFlash_BrightensTheAmbient()
        {
            var look = LookSettings.Default;

            var dark = WeatherLookProcessor.Apply(look, Storm, 1f, 0f);
            var lit = WeatherLookProcessor.Apply(look, Storm, 1f, 1f);

            Assert.That(lit.AmbientSky.r, Is.GreaterThan(dark.AmbientSky.r));
            Assert.That(lit.SunIntensity, Is.EqualTo(dark.SunIntensity), "a flash lights the air, not the sun");
        }
    }
}
