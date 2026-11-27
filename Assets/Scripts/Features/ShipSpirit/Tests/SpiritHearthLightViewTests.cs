#nullable enable
using NUnit.Framework;

namespace TinCan.Features.ShipSpirit.Tests
{
    public class SpiritHearthLightViewTests
    {
        [Test]
        public void Flicker_StaysSoft_BetweenFourFifthsAndALittleOverOne()
        {
            for (float t = 0f; t < 30f; t += 0.031f)
            {
                Assert.That(SpiritHearthLightView.Flicker(t, seed: 3.7f), Is.InRange(0.8f, 1.1f), $"at {t}");
            }
        }

        [Test]
        public void Flicker_Moves()
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float t = 0f; t < 10f; t += 0.05f)
            {
                float f = SpiritHearthLightView.Flicker(t, seed: 0f);
                min = System.Math.Min(min, f);
                max = System.Math.Max(max, f);
            }

            Assert.That(max - min, Is.GreaterThan(0.1f), "a hearth light that never changes is not alive");
        }

        [Test]
        public void Flicker_DiffersBySeed()
        {
            Assert.That(SpiritHearthLightView.Flicker(1f, seed: 0f), Is.Not.EqualTo(SpiritHearthLightView.Flicker(1f, seed: 2.5f)).Within(1e-4f));
        }
    }
}
