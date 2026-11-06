#nullable enable
using NUnit.Framework;
using TinCan.DevTools.AudioLab;
using UnityEngine;

namespace TinCan.DevTools.Tests
{
    /// <summary>The orbiting emitter circles the centre at its radius, one lap per period, starting straight ahead.</summary>
    public class AudioLabViewTests
    {
        [Test]
        public void Orbit_StartsAhead_AndQuarterLapIsToTheRight()
        {
            var centre = new Vector3(0f, 1.7f, 0f);

            var start = AudioLabView.OrbitPosition(centre, 12f, 10f, 0f);
            var quarter = AudioLabView.OrbitPosition(centre, 12f, 10f, 2.5f);

            Assert.That(Vector3.Distance(start, centre + new Vector3(0f, 0f, 12f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(quarter, centre + new Vector3(12f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void Orbit_StaysOnItsCircle_AndAZeroPeriodStandsStill()
        {
            var centre = Vector3.one;

            for (float t = 0f; t < 20f; t += 0.7f)
                Assert.That(Vector3.Distance(AudioLabView.OrbitPosition(centre, 5f, 3f, t), centre), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(AudioLabView.OrbitPosition(centre, 5f, 0f, 7f), Is.EqualTo(AudioLabView.OrbitPosition(centre, 5f, 0f, 0f)));
        }
    }
}
