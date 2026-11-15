#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.Atmosphere.Tests
{
    public class DayLightProcessorTests
    {
        private readonly DayLightProcessor _processor = new();

        private static DayLightKey Key(float elevation, float intensity, Color colour) => new()
        {
            Elevation = elevation,
            SunIntensity = intensity,
            SunColour = colour,
            AmbientSky = colour,
            SkyZenith = colour,
            NightShade = new Color(0f, 0f, 0f, 1f - intensity),
            Stars = 1f - intensity,
        };

        // Out of order on purpose: the processor finds the keys around an elevation in any order.
        private static readonly DayLightKey[] Keys =
        {
            Key(10f, 1f, Color.white),
            Key(-10f, 0f, Color.black),
            Key(0f, 0.5f, Color.red),
        };

        [Test]
        public void BetweenTwoKeys_Blends()
        {
            var light = _processor.Evaluate(Keys, 5f);

            Assert.That(light.SunIntensity, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(light.SunColour.g, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(light.NightShade.a, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(light.Stars, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void OnAKey_IsThatKey()
        {
            Assert.That(_processor.Evaluate(Keys, 0f).SunColour, Is.EqualTo(Color.red));
        }

        [Test]
        public void BeyondTheKeys_HoldsTheNearest()
        {
            Assert.That(_processor.Evaluate(Keys, 40f).SunIntensity, Is.EqualTo(1f));
            Assert.That(_processor.Evaluate(Keys, -40f).SunIntensity, Is.EqualTo(0f));
        }

        [Test]
        public void NoKeys_IsNoLight()
        {
            Assert.That(_processor.Evaluate(System.Array.Empty<DayLightKey>(), 5f).SunIntensity, Is.EqualTo(0f));
        }

        [Test]
        public void TheSunLight_ShinesAwayFromTheSun_EvenStraightOverhead()
        {
            var low = new Vector3(0.3f, 0.1f, -0.9f).normalized;

            Assert.That(Vector3.Dot(AtmospherePresenter.SunRotation(low) * Vector3.forward, -low), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Vector3.Angle(AtmospherePresenter.SunRotation(Vector3.up) * Vector3.forward, Vector3.down), Is.LessThan(0.01f));
        }

        [Test]
        public void Apply_ReplacesTheLight_AndKeepsTheFogShape()
        {
            var look = LookSettings.Default;
            var light = _processor.Evaluate(Keys, -10f);

            var lit = _processor.Apply(look, light);

            Assert.That(lit.SunIntensity, Is.EqualTo(0f));
            Assert.That(lit.AmbientSky, Is.EqualTo(Color.black));
            Assert.That(lit.FogDensity, Is.EqualTo(look.FogDensity));
            Assert.That(lit.WhiteoutColour, Is.EqualTo(look.WhiteoutColour));
        }
    }
}
