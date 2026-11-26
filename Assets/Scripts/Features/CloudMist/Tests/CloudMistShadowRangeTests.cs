#nullable enable
using NUnit.Framework;

namespace TinCan.Features.CloudMist.Tests
{
    public sealed class CloudMistShadowRangeTests
    {
        private readonly CloudMistShadowRange _range = new();

        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(50f)]
        public void ARangeNotPastTheGamesOwn_LeavesTheShadowsAlone(float range) =>
            Assert.That(_range.Splits(50f, range), Is.Null);

        [Test]
        public void TheGamesOwnDistance_StaysTheFirstCascade()
        {
            var splits = _range.Splits(50f, 400f)!.Value;
            Assert.That(splits.x * 400f, Is.EqualTo(50f).Within(1e-3f));
        }

        [Test]
        public void TheCascades_GrowByAnEvenRatio_OutToTheRange()
        {
            // 50 m to 400 m is a ratio of 8: cascades end at 50, 100, 200 and 400 m.
            var splits = _range.Splits(50f, 400f)!.Value;
            Assert.That(splits.y * 400f, Is.EqualTo(100f).Within(1e-2f));
            Assert.That(splits.z * 400f, Is.EqualTo(200f).Within(1e-2f));
        }
    }
}
