#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Features.CrewBody.Tests
{
    public class CrewBodyProcessorTests
    {
        [Test]
        public void SignedSpeed_IsGroundSpeed_NegativeOnlyMovingMostlyBack()
        {
            Assert.That(CrewBodyProcessor.SignedSpeed(new Vector3(0f, -3f, 7f)), Is.EqualTo(7f).Within(1e-4f), "falling speed is not ground speed");
            Assert.That(CrewBodyProcessor.SignedSpeed(new Vector3(0f, 0f, -7f)), Is.EqualTo(-7f).Within(1e-4f));
            Assert.That(CrewBodyProcessor.SignedSpeed(new Vector3(7f, 0f, 0f)), Is.EqualTo(7f).Within(1e-4f), "sideways runs forward");
            Assert.That(CrewBodyProcessor.SignedSpeed(new Vector3(5f, 0f, -1f)), Is.GreaterThan(0f), "a little back of sideways");
        }

        [TestCase(true, 0.2f, CrewBodyGait.Idle)]
        [TestCase(true, 7f, CrewBodyGait.Run)]
        [TestCase(true, -7f, CrewBodyGait.Run)]
        [TestCase(true, 12.6f, CrewBodyGait.Sprint)]
        [TestCase(false, 7f, CrewBodyGait.Airborne)]
        public void Gait_FromGroundAndSpeed(bool grounded, float speed, CrewBodyGait expected)
        {
            Assert.That(CrewBodyProcessor.Gait(grounded, speed, 7f, 12.6f), Is.EqualTo(expected));
        }
    }
}
