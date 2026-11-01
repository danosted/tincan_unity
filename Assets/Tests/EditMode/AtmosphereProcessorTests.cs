#nullable enable
using NUnit.Framework;
using TinCan.Features.Atmosphere;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>The look plus the view's cloud submersion gives the frame's light and air (plan .docs/plans/look-slice.md, L2).</summary>
    public class AtmosphereProcessorTests
    {
        private readonly AtmosphereProcessor _processor = new();
        private readonly LookSettings _look = LookSettings.Default;

        [Test]
        public void InClearAir_TheFrameIsTheLook()
        {
            var frame = _processor.Evaluate(_look, whiteout: 0f, lightMultiplier: 1f);

            Assert.That(frame.SunIntensity, Is.EqualTo(_look.SunIntensity));
            Assert.That(frame.SunwardFog, Is.EqualTo(_look.SunwardFog));
            Assert.That(frame.AwayFog, Is.EqualTo(_look.AwayFog));
            Assert.That(frame.FogDensity, Is.EqualTo(_look.FogDensity));
            Assert.That(frame.FogFalloff, Is.EqualTo(_look.FogFalloff));
            Assert.That(frame.MaxFog, Is.EqualTo(_look.MaxFog));
            Assert.That(frame.AmbientSky, Is.EqualTo(_look.AmbientSky));
        }

        [Test]
        public void FullyInACloud_TheAirTurnsWhite_ThickensEvenly_AndTheLightDims()
        {
            var frame = _processor.Evaluate(_look, whiteout: 1f, lightMultiplier: 0.2f);

            Assert.That(frame.SunwardFog, Is.EqualTo(_look.WhiteoutColour));
            Assert.That(frame.AwayFog, Is.EqualTo(_look.WhiteoutColour));
            Assert.That(frame.FogDensity, Is.EqualTo(_look.WhiteoutDensity));
            Assert.That(frame.FogFalloff, Is.LessThan(0.001f), "no bands inside the cloud");
            Assert.That(frame.MaxFog, Is.EqualTo(1f));
            Assert.That(frame.SunIntensity, Is.EqualTo(_look.SunIntensity * 0.2f).Within(1e-5f));
            Assert.That(frame.AmbientSky.r, Is.LessThan(_look.AmbientSky.r));
            Assert.That(frame.AmbientSky.r, Is.GreaterThanOrEqualTo(_look.AmbientSky.r * 0.6f - 1e-5f), "the ambient keeps a floor");
        }

        [Test]
        public void HalfwayIn_SitsBetween()
        {
            var clear = _processor.Evaluate(_look, 0f, 1f);
            var half = _processor.Evaluate(_look, 0.5f, 0.6f);
            var full = _processor.Evaluate(_look, 1f, 0.2f);

            Assert.That(half.FogDensity, Is.GreaterThan(clear.FogDensity).And.LessThan(full.FogDensity));
            Assert.That(half.SunIntensity, Is.LessThan(clear.SunIntensity).And.GreaterThan(full.SunIntensity));
        }

        [Test]
        public void OutOfRangeInputs_AreClamped()
        {
            var frame = _processor.Evaluate(_look, whiteout: 3f, lightMultiplier: -1f);

            Assert.That(frame.FogDensity, Is.EqualTo(_look.WhiteoutDensity));
            Assert.That(frame.SunIntensity, Is.Zero);
        }

        [Test]
        public void ThePlainFogDensity_IsTheBandAtTheViewsHeight_PlusHaze()
        {
            var frame = _processor.Evaluate(_look, 0f, 1f);

            Assert.That(frame.UniformDensityAt(_look.FogBaseHeight, _look.FogBaseHeight), Is.EqualTo(_look.FogDensity + _look.HazeDensity).Within(1e-6f));
            Assert.That(frame.UniformDensityAt(_look.FogBaseHeight - 50f, _look.FogBaseHeight), Is.EqualTo(_look.FogDensity + _look.HazeDensity).Within(1e-6f), "below the base it is the base's");
            float high = frame.UniformDensityAt(_look.FogBaseHeight + 100f, _look.FogBaseHeight);
            Assert.That(high, Is.EqualTo(_look.FogDensity * Mathf.Exp(-_look.FogFalloff * 100f) + _look.HazeDensity).Within(1e-6f));
        }
    }
}
