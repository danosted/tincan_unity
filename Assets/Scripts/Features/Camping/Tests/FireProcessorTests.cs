#nullable enable
using NUnit.Framework;

namespace TinCan.Features.Camping.Tests
{
    public class FireProcessorTests
    {
        [Test]
        public void Remaining_IsTheTimeUntilItGoesOut_NeverBelowZero()
        {
            Assert.That(FireProcessor.Remaining(now: 10d, outAt: 70d), Is.EqualTo(60f));
            Assert.That(FireProcessor.Remaining(now: 80d, outAt: 70d), Is.EqualTo(0f));
        }

        [Test]
        public void Stoke_AColdFire_BurnsALogFromNow()
        {
            var (outAt, accepted) = FireProcessor.Stoke(now: 100d, outAt: 0d, seconds: 180f, maxSeconds: 600f);

            Assert.That(accepted, Is.EqualTo(180f));
            Assert.That(outAt, Is.EqualTo(280d));
        }

        [Test]
        public void Stoke_ABurningFire_AddsToWhatIsLeft()
        {
            var (outAt, accepted) = FireProcessor.Stoke(now: 100d, outAt: 200d, seconds: 180f, maxSeconds: 600f);

            Assert.That(accepted, Is.EqualTo(180f));
            Assert.That(outAt, Is.EqualTo(380d));
        }

        [Test]
        public void Stoke_NearlyFull_TopsUpToTheMost()
        {
            var (outAt, accepted) = FireProcessor.Stoke(now: 0d, outAt: 480d, seconds: 180f, maxSeconds: 600f);

            Assert.That(accepted, Is.EqualTo(120f));
            Assert.That(outAt, Is.EqualTo(600d));
        }

        [Test]
        public void Stoke_LessThanHalfALogOfRoom_RefusesTheLog()
        {
            var (outAt, accepted) = FireProcessor.Stoke(now: 0d, outAt: 550d, seconds: 180f, maxSeconds: 600f);

            Assert.That(accepted, Is.EqualTo(0f));
            Assert.That(outAt, Is.EqualTo(550d));
        }

        [Test]
        public void Fullness_IsRemainingOverTheMost_Clamped()
        {
            Assert.That(FireProcessor.Fullness(300f, 600f), Is.EqualTo(0.5f));
            Assert.That(FireProcessor.Fullness(900f, 600f), Is.EqualTo(1f));
            Assert.That(FireProcessor.Fullness(10f, 0f), Is.EqualTo(0f));
        }
    }
}
