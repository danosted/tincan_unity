#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Core.Humanoid.Tests
{
    public class HumanoidPresentationProcessorTests
    {
        [Test]
        public void Velocity_IsDistanceOverTime_AndZeroWithoutTime()
        {
            var velocity = HumanoidPresentationProcessor.Velocity(Vector3.zero, new Vector3(0f, 0f, 0.7f), 0.1f);

            Assert.That((velocity - new Vector3(0f, 0f, 7f)).magnitude, Is.LessThan(1e-4f));
            Assert.That(HumanoidPresentationProcessor.Velocity(Vector3.zero, Vector3.one, 0f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void StandingOnAMovingTurningShip_HasNoSpeed()
        {
            var ship = new GameObject("ship").transform;
            var player = new GameObject("player").transform;
            try
            {
                player.SetParent(ship, false);
                player.localPosition = new Vector3(2f, 1f, -3f);
                Vector3 before = ship.InverseTransformPoint(player.position);

                ship.SetPositionAndRotation(new Vector3(5f, 0.5f, 12f), Quaternion.Euler(0f, 20f, 3f));
                Vector3 after = ship.InverseTransformPoint(player.position);

                Assert.That(HumanoidPresentationProcessor.Velocity(before, after, 0.1f).magnitude, Is.LessThan(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(ship.gameObject);
            }
        }

        [Test]
        public void Smooth_MovesPartWay_AndSnapsWithoutSmoothing()
        {
            var eased = HumanoidPresentationProcessor.Smooth(Vector3.zero, Vector3.forward * 10f, 0.1f, 0.1f);

            Assert.That(eased.z, Is.GreaterThan(5f).And.LessThan(10f));
            Assert.That(HumanoidPresentationProcessor.Smooth(Vector3.zero, Vector3.forward, 0.1f, 0f), Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void IsGrounded_OnGround_NotWhileRising_AndOverAShortGap()
        {
            Assert.That(HumanoidPresentationProcessor.IsGrounded(true, 0f, 0f), Is.True);
            Assert.That(HumanoidPresentationProcessor.IsGrounded(true, 6f, 0f), Is.False, "the start of a jump");
            Assert.That(HumanoidPresentationProcessor.IsGrounded(false, -2f, 0.05f), Is.True, "a step down");
            Assert.That(HumanoidPresentationProcessor.IsGrounded(false, -2f, 0.5f), Is.False, "falling");
        }
    }
}
