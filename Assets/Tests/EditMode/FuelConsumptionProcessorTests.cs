#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel;

namespace TinCan.Tests.EditMode
{
    public class FuelConsumptionProcessorTests
    {
        private FuelConsumptionProcessor _processor = null!;

        [SetUp]
        public void SetUp() => _processor = new FuelConsumptionProcessor();

        [Test]
        public void ComputeDrain_NoThrottle_NoDrain()
        {
            Assert.That(_processor.ComputeDrain(0f, false, 2f, 2f, 0.1f), Is.EqualTo(0f));
        }

        [Test]
        public void ComputeDrain_FullThrottle_IsRateTimesDeltaTime()
        {
            Assert.That(_processor.ComputeDrain(1f, false, 2f, 2f, 0.5f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ComputeDrain_ReverseThrottle_DrainsTheSameAsForward()
        {
            Assert.That(_processor.ComputeDrain(-1f, false, 2f, 2f, 0.5f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ComputeDrain_Boosting_MultipliesDrain()
        {
            Assert.That(_processor.ComputeDrain(1f, true, 2f, 3f, 0.5f), Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void ComputeDrain_BoostMultiplierBelowOne_NeverReducesDrain()
        {
            Assert.That(_processor.ComputeDrain(1f, true, 2f, 0.5f, 0.5f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ClampLevel_StaysWithinTank()
        {
            Assert.That(_processor.ClampLevel(-5f, 100f), Is.EqualTo(0f));
            Assert.That(_processor.ClampLevel(150f, 100f), Is.EqualTo(100f));
            Assert.That(_processor.ClampLevel(42f, 100f), Is.EqualTo(42f));
        }

        [Test]
        public void IsDriven_RequiresPossessorAndThrottle()
        {
            Assert.That(_processor.IsDriven(true, 0.5f), Is.True);
            Assert.That(_processor.IsDriven(false, 0.5f), Is.False);
            Assert.That(_processor.IsDriven(true, 0f), Is.False);
        }
    }
}
