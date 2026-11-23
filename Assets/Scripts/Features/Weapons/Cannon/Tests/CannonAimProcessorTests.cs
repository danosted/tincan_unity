#nullable enable
using NUnit.Framework;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon.Tests
{
    /// <summary>The cannon's barrel aim: limits, steering and the muzzle direction (plans <c>cannon-and-hazards.md</c>, <c>ray-cannons.md</c>).</summary>
    public class CannonAimProcessorTests
    {
        private const float Tolerance = 1e-3f;

        private readonly CannonAimProcessor _aim = new();

        [Test]
        public void ClampAim_KeepsTheAimWithinTheLimits()
        {
            var limits = new CannonAimLimits(yawLimit: 60f, minElevation: -10f, maxElevation: 45f);

            Assert.That(_aim.ClampAim(new Vector2(30f, 20f), limits), Is.EqualTo((30f, 20f)));
            Assert.That(_aim.ClampAim(new Vector2(-170f, 80f), limits), Is.EqualTo((-60f, 45f)));
            Assert.That(_aim.ClampAim(new Vector2(0f, -50f), limits).Elevation, Is.EqualTo(-10f).Within(Tolerance));
        }

        [Test]
        public void Steer_SweepsToTheLimit_HoldsThere_AndTurnsBackAtOnce()
        {
            var limits = new CannonAimLimits(yawLimit: 60f, minElevation: -10f, maxElevation: 45f);
            var aim = Vector2.zero;

            for (int frame = 0; frame < 100; frame++) aim = _aim.Steer(aim, new Vector2(20f, 0f), 0.5f, limits);
            Assert.That(aim.x, Is.EqualTo(60f).Within(Tolerance), "a long sweep right stops at the limit: no 180 deg wrap");

            aim = _aim.Steer(aim, new Vector2(-20f, 10f), 0.5f, limits);
            Assert.That(aim.x, Is.EqualTo(50f).Within(Tolerance), "turning back moves off the limit immediately");
            Assert.That(aim.y, Is.EqualTo(5f).Within(Tolerance), "mouse up is elevation up");
        }

        [Test]
        public void ClampAim_FullTurn_WrapsTheYaw_AndStillLimitsElevation()
        {
            var limits = new CannonAimLimits(CannonAimLimits.FullTurn, minElevation: -10f, maxElevation: 45f);

            Assert.That(limits.TurnsFully, Is.True);
            Assert.That(_aim.ClampAim(new Vector2(170f, 20f), limits).Yaw, Is.EqualTo(170f).Within(Tolerance));
            Assert.That(_aim.ClampAim(new Vector2(190f, 20f), limits).Yaw, Is.EqualTo(-170f).Within(Tolerance));
            Assert.That(_aim.ClampAim(new Vector2(-400f, 20f), limits).Yaw, Is.EqualTo(-40f).Within(Tolerance));
            Assert.That(_aim.ClampAim(new Vector2(0f, 80f), limits).Elevation, Is.EqualTo(45f).Within(Tolerance));
        }

        [Test]
        public void Steer_FullTurn_KeepsTurning_PastTheBack()
        {
            var limits = new CannonAimLimits(CannonAimLimits.FullTurn, minElevation: -10f, maxElevation: 45f);
            var aim = new Vector2(170f, 0f);

            aim = _aim.Steer(aim, new Vector2(40f, 0f), 0.5f, limits);

            Assert.That(aim.x, Is.EqualTo(-170f).Within(Tolerance), "190 deg wraps to -170: the barrel keeps turning");
        }

        [Test]
        public void MuzzleDirection_MatchesTheAngles()
        {
            Vector3 up45 = _aim.MuzzleDirection(Quaternion.identity, 0f, 45f);
            Assert.That(Vector3.Distance(up45, new Vector3(0f, 1f, 1f).normalized), Is.LessThan(Tolerance));

            Vector3 right90 = _aim.MuzzleDirection(Quaternion.identity, 90f, 0f);
            Assert.That(Vector3.Distance(right90, Vector3.right), Is.LessThan(Tolerance));
        }
    }
}
