#nullable enable
using NUnit.Framework;

namespace TinCan.Core.Settings.Tests
{
    public sealed class ViewDistanceTests
    {
        [TestCase("Near", 700f)]
        [TestCase("Normal", 1000f)]
        [TestCase("Far", 1500f)]
        [TestCase("very far", 2500f)]
        public void AChoice_GivesItsMetres(string choice, float metres) =>
            Assert.That(ViewDistance.Metres(choice), Is.EqualTo(metres));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Infinite")]
        public void NoOrAnUnknownChoice_IsNormal(string? choice) =>
            Assert.That(ViewDistance.Metres(choice), Is.EqualTo(1000f));

        [Test]
        public void TheDefault_IsOneOfTheChoices_AndTheyRunNearToFar()
        {
            Assert.That(ViewDistance.Names, Does.Contain(ViewDistance.Default));
            for (int i = 1; i < ViewDistance.Options.Count; i++)
                Assert.That(ViewDistance.Options[i].Metres, Is.GreaterThan(ViewDistance.Options[i - 1].Metres));
        }
    }
}
