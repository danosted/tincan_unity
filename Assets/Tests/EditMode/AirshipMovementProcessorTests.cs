using NUnit.Framework;
using UnityEngine;
using TinCan.Features.Airship;

namespace TinCan.Tests.EditMode
{
    public class AirshipMovementProcessorTests
    {
        private AirshipMovementProcessor _processor;

        [SetUp]
        public void SetUp()
        {
            _processor = new AirshipMovementProcessor();
        }

        [Test]
        public void CalculateLinearSpeed_AcceleratesTowardThrottleTarget()
        {
            var input = new AirshipInputState { Throttle = 1f };

            float result = _processor.CalculateLinearSpeed(
                currentSpeed: 0f,
                input: input,
                maxForward: 10f,
                maxBackward: 5f,
                accel: 20f,
                decel: 10f,
                deltaTime: 0.1f);

            Assert.That(result, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void CalculateLinearSpeed_DecelerateWhenThrottleReleased()
        {
            var input = new AirshipInputState { Throttle = 0f };

            float result = _processor.CalculateLinearSpeed(
                currentSpeed: 5f,
                input: input,
                maxForward: 10f,
                maxBackward: 5f,
                accel: 20f,
                decel: 10f,
                deltaTime: 0.1f);

            Assert.That(result, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void CalculateLinearSpeed_OvershootCorrection_DeceleratesWhenFasterThanNewTarget()
        {
            // Currently at full forward speed, throttle reversed to backward - should decelerate
            // toward the new (lower magnitude) target rather than accelerate past it.
            var input = new AirshipInputState { Throttle = -0.1f };

            float result = _processor.CalculateLinearSpeed(
                currentSpeed: 10f,
                input: input,
                maxForward: 10f,
                maxBackward: 5f,
                accel: 20f,
                decel: 2f,
                deltaTime: 0.1f);

            // targetSpeed = -0.1 * 5 = -0.5; |10| > |-0.5| so decel rate (2) applies: 10 - 0.2 = 9.8
            Assert.That(result, Is.EqualTo(9.8f).Within(0.0001f));
        }

        [Test]
        public void CalculateVelocityWithDrift_BlendsTowardTargetVelocity()
        {
            Vector3 result = _processor.CalculateVelocityWithDrift(
                currentVelocity: Vector3.zero,
                targetForwardDirection: Vector3.forward,
                currentSpeed: 10f,
                blendRate: 1f,
                deltaTime: 0.5f);

            Assert.That(result, Is.EqualTo(new Vector3(0, 0, 5f)));
        }

        [Test]
        public void CalculateAngularVelocity_YawMomentumAcceleratesTowardInput()
        {
            var input = new AirshipInputState { Yaw = 1f };

            Vector3 result = _processor.CalculateAngularVelocity(
                currentAngularVelocity: Vector3.zero,
                input: input,
                currentSpeed: 5f,
                maxForwardSpeed: 10f,
                currentRoll: 0f,
                turnSpeed: 30f,
                pitchSpeed: 20f,
                angularAccel: 10f,
                angularDecel: 5f,
                maxBankAngle: 20f,
                bankSpeed: 1f,
                deltaTime: 0.1f);

            // targetYaw = 30, accel rate 10 applies since input.Yaw != 0: MoveTowards(0, 30, 1) = 1
            Assert.That(result.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CalculateAngularVelocity_BanksAwayFromTurnDirection()
        {
            var input = new AirshipInputState { Yaw = 1f };

            Vector3 result = _processor.CalculateAngularVelocity(
                currentAngularVelocity: Vector3.zero,
                input: input,
                currentSpeed: 10f,
                maxForwardSpeed: 10f,
                currentRoll: 0f,
                turnSpeed: 30f,
                pitchSpeed: 20f,
                angularAccel: 10f,
                angularDecel: 5f,
                maxBankAngle: 20f,
                bankSpeed: 1f,
                deltaTime: 0.1f);

            // targetBankAngle = -1 * 20 * 1(speedFactor) = -20; rollDifference = -20 - 0 = -20; rollVel = -20 * 1
            Assert.That(result.z, Is.EqualTo(-20f).Within(0.0001f));
        }

        [Test]
        public void CalculateAngularVelocity_ZeroMaxForwardSpeed_StaysFinite()
        {
            // A stalled engine overrides the flight speed attribute to 0; the bank factor must not become 0/0.
            var input = new AirshipInputState { Yaw = 1f, Pitch = 0f };

            Vector3 result = _processor.CalculateAngularVelocity(
                currentAngularVelocity: Vector3.zero,
                input: input,
                currentSpeed: 0f,
                maxForwardSpeed: 0f,
                currentRoll: 0f,
                turnSpeed: 45f,
                pitchSpeed: 30f,
                angularAccel: 15f,
                angularDecel: 20f,
                maxBankAngle: 15f,
                bankSpeed: 2f,
                deltaTime: 0.1f);

            Assert.That(float.IsNaN(result.x) || float.IsNaN(result.y) || float.IsNaN(result.z), Is.False);
            Assert.That(result.z, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
