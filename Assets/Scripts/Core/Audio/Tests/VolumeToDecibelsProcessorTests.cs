#nullable enable
using NUnit.Framework;

namespace TinCan.Core.Audio.Tests
{
    /// <summary>0 is silent, 1 is the authored level, and the steps between follow the log curve.</summary>
    public class VolumeToDecibelsProcessorTests
    {
        private readonly VolumeToDecibelsProcessor _processor = new();

        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.02f)]
        [TestCase(0.1f, -20f)]
        [TestCase(0.01f, -40f)]
        public void Volume_FollowsTheLogCurve(float volume, float decibels)
        {
            Assert.That(_processor.Decibels(volume), Is.EqualTo(decibels).Within(0.01f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(0.00001f)]
        public void ZeroOrBelow_IsSilent(float volume)
        {
            Assert.That(_processor.Decibels(volume), Is.EqualTo(VolumeToDecibelsProcessor.Silent));
        }

        [Test]
        public void AboveOne_IsClampedToTheAuthoredLevel()
        {
            Assert.That(_processor.Decibels(2f), Is.EqualTo(0f));
        }
    }
}
