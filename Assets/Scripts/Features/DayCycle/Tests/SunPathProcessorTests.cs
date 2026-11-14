#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.DayCycle.Tests
{
    public class SunPathProcessorTests
    {
        private readonly SunPathProcessor _processor = new();
        private static readonly SunPath North = SunPath.Default; // 63 N, declination -15

        [Test]
        public void Noon_IsLowInTheSouth()
        {
            var sun = _processor.Evaluate(North, 12f);

            Assert.That(sun.Elevation, Is.EqualTo(12f).Within(0.01f));
            Assert.That(sun.Azimuth, Is.EqualTo(180f).Within(0.01f));
            Assert.That(sun.ToSun.z, Is.LessThan(0f), "south is -Z with north +Z");
            Assert.That(_processor.NoonElevation(North), Is.EqualTo(12f).Within(0.01f));
        }

        [Test]
        public void Midnight_IsDeepBelowTheHorizon()
        {
            Assert.That(_processor.Evaluate(North, 0f).Elevation, Is.EqualTo(-42f).Within(0.01f));
        }

        [Test]
        public void Morning_RisesInTheSouthEast_AndTheAfternoonMirrorsIt()
        {
            var morning = _processor.Evaluate(North, 9f);
            var afternoon = _processor.Evaluate(North, 15f);

            Assert.That(morning.Azimuth, Is.InRange(90f, 180f));
            Assert.That(morning.ToSun.x, Is.GreaterThan(0f), "east is +X");
            Assert.That(afternoon.Azimuth, Is.EqualTo(360f - morning.Azimuth).Within(0.01f));
            Assert.That(afternoon.Elevation, Is.EqualTo(morning.Elevation).Within(0.01f));
        }

        [Test]
        public void Sunrise_IsAboutEightAndSunsetAboutFour()
        {
            float? rise = _processor.HourAtElevation(North, 0f, morning: true);
            float? set = _processor.HourAtElevation(North, 0f, morning: false);

            Assert.That(rise, Is.EqualTo(8.11f).Within(0.02f));
            Assert.That(set, Is.EqualTo(15.89f).Within(0.02f));
            Assert.That(_processor.Evaluate(North, rise!.Value).Elevation, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void AnElevationTheSunNeverReaches_HasNoHour()
        {
            Assert.That(_processor.HourAtElevation(North, 20f, morning: true), Is.Null);
            Assert.That(_processor.HourAtElevation(North, -50f, morning: true), Is.Null);
        }

        [Test]
        public void NorthYaw_TurnsThePathInTheWorld()
        {
            var path = North;
            path.NorthYaw = 90f; // north is +X, so south is -X

            var noon = _processor.Evaluate(path, 12f);

            Assert.That(noon.ToSun.x, Is.LessThan(-0.9f));
            Assert.That(noon.Azimuth, Is.EqualTo(180f).Within(0.01f), "the bearing is from the path's north");
        }

        [Test]
        public void LightRotation_ShinesAwayFromTheSun()
        {
            var sun = _processor.Evaluate(North, 10f);

            Assert.That(Vector3.Dot(sun.LightRotation * Vector3.forward, -sun.ToSun), Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
