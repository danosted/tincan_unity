#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.Airship.Damage;

namespace TinCan.Tests.EditMode
{
    public class ShipBreakageProcessorTests
    {
        private readonly ShipBreakageProcessor _processor = new();

        [Test]
        public void NextInterval_StaysInRange_EvenWhenBoundsAreSwapped()
        {
            var random = new Random(7);
            for (int i = 0; i < 100; i++)
            {
                float interval = _processor.NextInterval(random, 60f, 30f);
                Assert.That(interval, Is.InRange(30f, 60f));
            }
        }

        [Test]
        public void CanBreak_RespectsTheCapAndNeedsAHealthyPart()
        {
            Assert.That(_processor.CanBreak(new[] { false, false, false }, maxBroken: 2), Is.True);
            Assert.That(_processor.CanBreak(new[] { true, false, false }, maxBroken: 2), Is.True);
            Assert.That(_processor.CanBreak(new[] { true, true, false }, maxBroken: 2), Is.False);
            Assert.That(_processor.CanBreak(new[] { true, true }, maxBroken: 5), Is.False);
        }

        [Test]
        public void PickHealthy_NeverPicksABrokenPart()
        {
            var random = new Random(3);
            var broken = new[] { true, false, true, false };
            var picks = Enumerable.Range(0, 200).Select(_ => _processor.PickHealthy(random, broken)).ToArray();

            Assert.That(picks.All(pick => pick == 1 || pick == 3), Is.True);
            Assert.That(picks.Contains(1) && picks.Contains(3), Is.True, "Both healthy parts should come up over many picks.");
        }

        [Test]
        public void PickHealthy_AllBroken_ReturnsMinusOne()
        {
            Assert.That(_processor.PickHealthy(new Random(1), new[] { true, true }), Is.EqualTo(-1));
        }

        [Test]
        public void SameSeed_GivesTheSameSequence()
        {
            var broken = new[] { false, false, false, false, false };
            var a = new Random(42);
            var b = new Random(42);

            for (int i = 0; i < 10; i++)
            {
                Assert.That(_processor.PickHealthy(a, broken), Is.EqualTo(_processor.PickHealthy(b, broken)));
            }
        }
    }
}
