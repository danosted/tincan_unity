#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Features.Respawn;

namespace TinCan.Tests.EditMode
{
    public class RespawnProcessorTests
    {
        [Test]
        public void RuleFor_FindsTheRuleForTheCause()
        {
            var overboard = new RespawnRule { Cause = RespawnCause.Overboard, CarriedItem = CarriedItemOutcome.Lose, DelaySeconds = 4f };

            var rule = RespawnProcessor.RuleFor(new List<RespawnRule> { overboard }, RespawnCause.Overboard);

            Assert.That(rule, Is.SameAs(overboard));
        }

        [Test]
        public void RuleFor_WithoutARule_RespawnsAtOnceAndKeepsWhatIsCarried()
        {
            var rule = RespawnProcessor.RuleFor(new List<RespawnRule>(), RespawnCause.Overboard);

            Assert.That(rule.CarriedItem, Is.EqualTo(CarriedItemOutcome.Keep));
            Assert.That(rule.DelaySeconds, Is.Zero);
        }

        [Test]
        public void Remaining_CountsDownTheDelay_AndStopsAtZero()
        {
            Assert.That(RespawnProcessor.Remaining(fellAt: 10f, now: 12f, delaySeconds: 5f), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(RespawnProcessor.Remaining(fellAt: 10f, now: 16f, delaySeconds: 5f), Is.Zero);
            Assert.That(RespawnProcessor.Remaining(fellAt: 10f, now: 10f, delaySeconds: 0f), Is.Zero);
        }
    }
}
