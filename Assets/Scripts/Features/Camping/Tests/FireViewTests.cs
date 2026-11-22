#nullable enable
using NUnit.Framework;

namespace TinCan.Features.Camping.Tests
{
    public class FireViewTests
    {
        [Test]
        public void Flicker_StaysBetweenThreeQuartersAndOne()
        {
            for (float t = 0f; t < 20f; t += 0.037f)
            {
                float flicker = FireView.Flicker(t, seed: 2);
                Assert.That(flicker, Is.InRange(0.75f, 1f), $"at {t}");
            }
        }

        [Test]
        public void FlameHeight_IsNothingWhenOut_AndGrowsWithTheFire()
        {
            Assert.That(FireView.FlameHeight(0f, 1f), Is.EqualTo(0f));
            Assert.That(FireView.FlameHeight(0.1f, 1f), Is.LessThan(FireView.FlameHeight(1f, 1f)));
            Assert.That(FireView.FlameHeight(1f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void LightLevel_ADyingFireStillGlows()
        {
            Assert.That(FireView.LightLevel(0f, 1f), Is.EqualTo(0f));
            Assert.That(FireView.LightLevel(0.01f, 1f), Is.GreaterThan(0.3f));
            Assert.That(FireView.LightLevel(1f, 1f), Is.EqualTo(1f));
        }
    }
}
