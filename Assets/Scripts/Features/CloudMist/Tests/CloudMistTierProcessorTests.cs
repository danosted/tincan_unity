#nullable enable
using NUnit.Framework;

namespace TinCan.Features.CloudMist.Tests
{
    public sealed class CloudMistTierProcessorTests
    {
        private static readonly CloudMistTier[] Tiers =
        {
            new("Low", 0.25f, 16f, 12, 0, 0, false),
            new("Medium", 0.25f, 10f, 24, 0, 1, true),
            new("High", 0.5f, 6f, 40, 0, 1, true),
            new("Ultra", 0.5f, 4f, 64, 3, 2, true),
        };

        // Very Low has none; Very High and Ultra both draw Ultra.
        private static readonly int[] ByLevel = { -1, 0, 1, 2, 3, 3 };

        private readonly CloudMistTierProcessor _processor = new();

        [TestCase("Low", 5, 0)]
        [TestCase("ultra", 0, 3)]
        [TestCase("High", 1, 2)]
        public void ANamedTier_WinsOverTheQualityLevel(string choice, int level, int expected) =>
            Assert.That(_processor.Pick(choice, Tiers, level, ByLevel), Is.EqualTo(expected));

        [Test]
        public void Off_DrawsNoMist_AtAnyLevel() =>
            Assert.That(_processor.Pick(CloudMistTierProcessor.Off, Tiers, 5, ByLevel), Is.EqualTo(-1));

        [TestCase(0, -1)]
        [TestCase(1, 0)]
        [TestCase(3, 2)]
        [TestCase(5, 3)]
        public void Auto_FollowsTheQualityLevel(int level, int expected) =>
            Assert.That(_processor.Pick(CloudMistTierProcessor.Auto, Tiers, level, ByLevel), Is.EqualTo(expected));

        [Test]
        public void NoSetting_IsAuto() =>
            Assert.That(_processor.Pick(null, Tiers, 3, ByLevel), Is.EqualTo(2));

        [Test]
        public void AnUnknownChoice_IsAuto() =>
            Assert.That(_processor.Pick("Cinematic", Tiers, 1, ByLevel), Is.EqualTo(0));

        [TestCase(-3, -1)]
        [TestCase(9, 3)]
        public void ALevelOutsideTheMapping_TakesItsNearestEnd(int level, int expected) =>
            Assert.That(_processor.Pick(null, Tiers, level, ByLevel), Is.EqualTo(expected));

        [Test]
        public void AMappingToAMissingTier_DrawsNoMist() =>
            Assert.That(_processor.Pick(null, Tiers, 0, new[] { 7 }), Is.EqualTo(-1));

        [Test]
        public void NoTiers_DrawsNoMist() =>
            Assert.That(_processor.Pick("Low", new CloudMistTier[0], 3, ByLevel), Is.EqualTo(-1));

        [TestCase(1000f, 600f)] // Normal view distance
        [TestCase(2500f, 1500f)] // Very Far: the mist reaches further, whatever the tier
        public void TheMistReaches_AShareOfTheViewDistance(float farClipPlane, float reach)
        {
            var quality = UnityEngine.ScriptableObject.CreateInstance<CloudMistQuality>();
            try
            {
                Assert.That(quality.Reach(farClipPlane), Is.EqualTo(reach).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(quality);
            }
        }
    }
}
