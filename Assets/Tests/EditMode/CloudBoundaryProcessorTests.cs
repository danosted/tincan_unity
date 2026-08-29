#nullable enable
using NUnit.Framework;
using TinCan.Features.CloudBoundary;

namespace TinCan.Tests.EditMode
{
    public class CloudBoundaryProcessorTests
    {
        private CloudBoundaryProcessor _processor = null!;

        [SetUp]
        public void SetUp()
        {
            _processor = new CloudBoundaryProcessor();
        }

        [Test]
        public void BelowEmergencyDepth_StartsFullCountdown()
        {
            CloudEmergencyState result = _processor.EvaluateAirship(
                default,
                altitude: 94f,
                surfaceHeight: 100f,
                emergencyDepth: 5f,
                recoveryMargin: 2f,
                emergencyDuration: 15f,
                deltaTime: 1f);

            Assert.That(result.IsActive, Is.True);
            Assert.That(result.HasExpired, Is.False);
            Assert.That(result.RemainingTime, Is.EqualTo(15f));
        }

        [Test]
        public void ActiveEmergency_CountsDownAndExpires()
        {
            var state = new CloudEmergencyState(true, false, 0.5f);

            CloudEmergencyState result = _processor.EvaluateAirship(
                state,
                altitude: 90f,
                surfaceHeight: 100f,
                emergencyDepth: 5f,
                recoveryMargin: 2f,
                emergencyDuration: 15f,
                deltaTime: 1f);

            Assert.That(result.IsActive, Is.True);
            Assert.That(result.HasExpired, Is.True);
            Assert.That(result.RemainingTime, Is.Zero);
        }

        [Test]
        public void AboveRecoveryMargin_ResetsEmergency()
        {
            var state = new CloudEmergencyState(true, false, 4f);

            CloudEmergencyState result = _processor.EvaluateAirship(
                state,
                altitude: 102f,
                surfaceHeight: 100f,
                emergencyDepth: 5f,
                recoveryMargin: 2f,
                emergencyDuration: 15f,
                deltaTime: 1f);

            Assert.That(result.IsActive, Is.False);
            Assert.That(result.HasExpired, Is.False);
            Assert.That(result.RemainingTime, Is.EqualTo(15f));
        }

        [Test]
        public void InsideHysteresisBand_PreservesActiveEmergency()
        {
            var state = new CloudEmergencyState(true, false, 10f);

            CloudEmergencyState result = _processor.EvaluateAirship(
                state,
                altitude: 100f,
                surfaceHeight: 100f,
                emergencyDepth: 5f,
                recoveryMargin: 2f,
                emergencyDuration: 15f,
                deltaTime: 1f);

            Assert.That(result.IsActive, Is.True);
            Assert.That(result.RemainingTime, Is.EqualTo(9f));
        }
    }
}
