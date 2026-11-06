#nullable enable
using NUnit.Framework;

namespace TinCan.Core.Audio.Tests
{
    /// <summary>A new sound takes a finished voice first, and cuts off the oldest only when every voice is busy.</summary>
    public class AudioVoiceProcessorTests
    {
        [Test]
        public void Take_UsesFreeVoicesInOrder()
        {
            var voices = new AudioVoiceProcessor(3);

            Assert.That(voices.Take(0f, 1f), Is.EqualTo(0));
            Assert.That(voices.Take(0.1f, 1f), Is.EqualTo(1));
            Assert.That(voices.Take(0.2f, 1f), Is.EqualTo(2));
        }

        [Test]
        public void Take_ReusesAFinishedVoice()
        {
            var voices = new AudioVoiceProcessor(2);
            voices.Take(0f, 0.5f);
            voices.Take(0f, 5f);

            Assert.That(voices.IsBusy(0, 1f), Is.False);
            Assert.That(voices.Take(1f, 1f), Is.EqualTo(0));
        }

        [Test]
        public void Take_WhenAllAreBusy_StealsTheOldest()
        {
            var voices = new AudioVoiceProcessor(2);
            voices.Take(0f, 10f);
            voices.Take(1f, 10f);

            Assert.That(voices.Take(2f, 10f), Is.EqualTo(0));
            Assert.That(voices.Take(3f, 10f), Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RejectsAnEmptyPool()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AudioVoiceProcessor(0));
        }
    }
}
