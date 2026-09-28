#nullable enable
using NUnit.Framework;
using TinCan.Features.DesignedEvents;

namespace TinCan.Tests.EditMode
{
    public class EventRunProcessorTests
    {
        private readonly EventRunProcessor _processor = new();

        [Test]
        public void TimedPhase_StaysUntilTimeIsUp()
        {
            Assert.That(_processor.Advance(false, false, 89, 90, false), Is.EqualTo(EventStep.Stay));
            Assert.That(_processor.Advance(false, false, 90, 90, false), Is.EqualTo(EventStep.NextPhase));
        }

        [Test]
        public void TimedLastPhase_Succeeds()
        {
            Assert.That(_processor.Advance(false, false, 90, 90, true), Is.EqualTo(EventStep.Succeed));
        }

        [Test]
        public void ConditionPhase_EndsWhenConditionHolds()
        {
            Assert.That(_processor.Advance(true, false, 10, 90, false), Is.EqualTo(EventStep.Stay));
            Assert.That(_processor.Advance(true, true, 10, 90, false), Is.EqualTo(EventStep.NextPhase));
            Assert.That(_processor.Advance(true, true, 10, 90, true), Is.EqualTo(EventStep.Succeed));
        }

        [Test]
        public void ConditionPhase_FailsOnTimeout()
        {
            Assert.That(_processor.Advance(true, false, 90, 90, false), Is.EqualTo(EventStep.Fail));
        }

        [Test]
        public void Condition_WinsOverTimeoutOnTheSameTick()
        {
            Assert.That(_processor.Advance(true, true, 90, 90, true), Is.EqualTo(EventStep.Succeed));
        }
    }
}
