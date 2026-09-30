#nullable enable
using NUnit.Framework;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>The cannon's pure math: the arc, the per-tick sweep segment and expiry, and the barrel aim (plan <c>cannon-and-hazards.md</c>).</summary>
    public class CannonBallisticsTests
    {
        private const int TickRate = 30;
        private const float Tolerance = 1e-3f;
        private static readonly Vector3 Gravity = new(0f, -9.81f, 0f);

        private readonly CannonballProcessor _balls = new();
        private readonly CannonAimProcessor _aim = new();

        [Test]
        public void Arc_FollowsTheBallisticFormula()
        {
            var arc = new BallisticArc(new Vector3(1f, 2f, 3f), new Vector3(0f, 10f, 20f), Gravity, 0);

            Vector3 expected = new Vector3(1f, 2f, 3f) + new Vector3(0f, 10f, 20f) * 2f + 0.5f * 4f * Gravity;
            Assert.That(Vector3.Distance(arc.PositionAt(2f), expected), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(arc.VelocityAt(2f), new Vector3(0f, 10f, 20f) + Gravity * 2f), Is.LessThan(Tolerance));
        }

        [Test]
        public void FromMuzzle_AddsTheShipsVelocity()
        {
            var ship = new Vector3(5f, 0f, 0f);

            var arc = BallisticArc.FromMuzzle(Vector3.zero, Vector3.forward * 3f, 40f, ship, Gravity, 7);

            Assert.That(Vector3.Distance(arc.Velocity, new Vector3(5f, 0f, 40f)), Is.LessThan(Tolerance), "direction is normalized, ship velocity inherited");
            Assert.That(arc.FireTick, Is.EqualTo(7));
        }

        [Test]
        public void Segment_IsEmptyOnTheFireTick_ThenCoversOneTickOfFlight()
        {
            var arc = new BallisticArc(Vector3.zero, new Vector3(0f, 0f, 30f), Vector3.zero, 100);

            var onFire = _balls.SegmentAt(arc, 100, TickRate);
            Assert.That(Vector3.Distance(onFire.From, onFire.To), Is.LessThan(Tolerance));

            var next = _balls.SegmentAt(arc, 101, TickRate);
            Assert.That(Vector3.Distance(next.From, Vector3.zero), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(next.To, new Vector3(0f, 0f, 1f)), Is.LessThan(Tolerance), "30 m/s for one 1/30 s tick");
        }

        [Test]
        public void Segments_ChainWithoutGaps()
        {
            var arc = new BallisticArc(Vector3.zero, new Vector3(3f, 12f, 25f), Gravity, 0);

            for (int tick = 1; tick < 60; tick++)
            {
                var previous = _balls.SegmentAt(arc, tick, TickRate);
                var current = _balls.SegmentAt(arc, tick + 1, TickRate);
                Assert.That(Vector3.Distance(previous.To, current.From), Is.LessThan(Tolerance), $"gap at tick {tick}");
            }
        }

        [Test]
        public void Spent_AfterLifetime()
        {
            var arc = new BallisticArc(Vector3.zero, Vector3.zero, Vector3.zero, 0);
            var limits = new CannonballLimits(maxLifetime: 2f, maxRange: 1000f);

            Assert.That(_balls.IsSpent(arc, 59, TickRate, limits), Is.False);
            Assert.That(_balls.IsSpent(arc, 60, TickRate, limits), Is.True);
        }

        [Test]
        public void Spent_PastRange()
        {
            var arc = new BallisticArc(Vector3.zero, new Vector3(0f, 0f, 30f), Vector3.zero, 0);
            var limits = new CannonballLimits(maxLifetime: 100f, maxRange: 30f);

            Assert.That(_balls.IsSpent(arc, 29, TickRate, limits), Is.False);
            Assert.That(_balls.IsSpent(arc, 30, TickRate, limits), Is.True);
        }

        [Test]
        public void Spent_BelowTheFloor()
        {
            var arc = new BallisticArc(new Vector3(0f, 10f, 0f), Vector3.zero, Gravity, 0);
            var limits = new CannonballLimits(maxLifetime: 100f, maxRange: 1000f, floorHeight: 0f);

            Assert.That(_balls.IsSpent(arc, 30, TickRate, limits), Is.False, "fallen 4.9 m after 1 s");
            Assert.That(_balls.IsSpent(arc, 45, TickRate, limits), Is.True, "fallen 11 m after 1.5 s");
        }

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
        public void BaseVelocity_IsTheShipsTravel()
        {
            var rotation = Quaternion.Euler(0f, 40f, 0f);

            Vector3 velocity = _aim.BaseVelocityAt(new Vector3(1f, 2f, 3f), Vector3.zero, rotation, new Vector3(0f, 0f, 0.5f), rotation, 0.1f);

            Assert.That(Vector3.Distance(velocity, new Vector3(0f, 0f, 5f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void BaseVelocity_IgnoresTheBarrelSwing()
        {
            // The muzzle point jumps between frames because the barrel turned, but the base did not move: no velocity.
            var still = Quaternion.identity;

            Vector3 before = _aim.BaseVelocityAt(new Vector3(0f, 2f, 2.3f), Vector3.zero, still, Vector3.zero, still, 1f / 60f);
            Vector3 after = _aim.BaseVelocityAt(new Vector3(1.6f, 2f, 1.6f), Vector3.zero, still, Vector3.zero, still, 1f / 60f);

            Assert.That(before.magnitude, Is.LessThan(Tolerance));
            Assert.That(after.magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void BaseVelocity_IncludesTheShipsTurn()
        {
            // A point 10 m ahead of a base turning 1 degree in 0.1 s moves sideways at about 10 * (pi / 180) / 0.1 m/s.
            Vector3 velocity = _aim.BaseVelocityAt(Quaternion.Euler(0f, 1f, 0f) * new Vector3(0f, 0f, 10f),
                Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.Euler(0f, 1f, 0f), 0.1f);

            Assert.That(velocity.x, Is.EqualTo(10f * Mathf.Deg2Rad / 0.1f).Within(0.01f));
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
