using NUnit.Framework;
using UnityEngine;
using TinCan.Features.HumanoidMovement;

namespace TinCan.Tests.EditMode
{
    public class HumanoidMovementProcessorTests
    {
        private HumanoidMovementProcessor _processor;

        [SetUp]
        public void SetUp()
        {
            _processor = new HumanoidMovementProcessor();
        }

        [Test]
        public void CalculateHorizontalVelocity_AcceleratesTowardTargetDirection()
        {
            Vector3 result = _processor.CalculateHorizontalVelocity(
                currentVelocity: Vector3.zero,
                targetDirection: Vector3.forward,
                targetSpeed: 5f,
                acceleration: 10f,
                deceleration: 20f,
                deltaTime: 0.1f);

            // Acceleration rate (10) applies since there is input, capped by MoveTowards at rate * deltaTime = 1
            Assert.That(result.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.x, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void CalculateHorizontalVelocity_DeceleratesWhenNoInput()
        {
            Vector3 result = _processor.CalculateHorizontalVelocity(
                currentVelocity: new Vector3(0, 0, 5f),
                targetDirection: Vector3.zero,
                targetSpeed: 5f,
                acceleration: 10f,
                deceleration: 20f,
                deltaTime: 0.1f);

            // Deceleration rate (20) applies since there is no input; MoveTowards steps by rate * deltaTime = 2 toward 0
            Assert.That(result.z, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void CalculateHorizontalVelocity_ReachesTargetWithoutOvershoot()
        {
            Vector3 result = _processor.CalculateHorizontalVelocity(
                currentVelocity: Vector3.zero,
                targetDirection: Vector3.forward,
                targetSpeed: 5f,
                acceleration: 10f,
                deceleration: 20f,
                deltaTime: 10f); // Large deltaTime should clamp at target, not overshoot

            Assert.That(result, Is.EqualTo(new Vector3(0, 0, 5f)));
        }

        [Test]
        public void CalculateVerticalVelocity_JumpAppliesJumpForceWhenGrounded()
        {
            float result = _processor.CalculateVerticalVelocity(
                currentVertical: 0f,
                gravity: 10f,
                isGrounded: true,
                isPlatformSupported: false,
                isJumping: true,
                jumpForce: 8f,
                deltaTime: 0.1f);

            Assert.That(result, Is.EqualTo(8f));
        }

        [Test]
        public void CalculateVerticalVelocity_AppliesGroundStickinessWhenGroundedAndFalling()
        {
            float result = _processor.CalculateVerticalVelocity(
                currentVertical: -5f,
                gravity: 10f,
                isGrounded: true,
                isPlatformSupported: false,
                isJumping: false,
                jumpForce: 8f,
                deltaTime: 0.1f);

            Assert.That(result, Is.EqualTo(-2f));
        }

        [Test]
        public void CalculateVerticalVelocity_AppliesGravityWhenAirborne()
        {
            float result = _processor.CalculateVerticalVelocity(
                currentVertical: 0f,
                gravity: 10f,
                isGrounded: false,
                isPlatformSupported: false,
                isJumping: false,
                jumpForce: 8f,
                deltaTime: 0.1f);

            Assert.That(result, Is.EqualTo(-1f).Within(0.0001f));
        }

        [Test]
        public void CalculateVerticalVelocity_PlatformSupportedActsLikeGrounded()
        {
            float result = _processor.CalculateVerticalVelocity(
                currentVertical: -3f,
                gravity: 10f,
                isGrounded: false,
                isPlatformSupported: true,
                isJumping: false,
                jumpForce: 8f,
                deltaTime: 0.1f);

            Assert.That(result, Is.EqualTo(-2f));
        }
    }
}
