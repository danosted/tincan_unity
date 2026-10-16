#nullable enable
using NUnit.Framework;
using TinCan.Features.Respawn;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class LeashProcessorTests
    {
        private RespawnRules _rules = null!;

        [SetUp]
        public void SetUp()
        {
            _rules = ScriptableObject.CreateInstance<RespawnRules>();
            _rules.CanClimb = true;
            _rules.ClimbSpeed = 2f;
            _rules.WinchSpeed = 6f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rules);

        [Test]
        public void Shorten_ClimbingAndTheWinchAddUp_NeverBelowZero()
        {
            Assert.That(LeashProcessor.Shorten(10f, climbing: true, winching: false, _rules, 0.5f), Is.EqualTo(9f).Within(1e-4f));
            Assert.That(LeashProcessor.Shorten(10f, climbing: true, winching: true, _rules, 0.5f), Is.EqualTo(6f).Within(1e-4f));
            Assert.That(LeashProcessor.Shorten(1f, climbing: false, winching: true, _rules, 1f), Is.Zero);
        }

        [Test]
        public void Shorten_WhenTheRulesForbidClimbing_OnlyTheWinchHelps()
        {
            _rules.CanClimb = false;

            Assert.That(LeashProcessor.Shorten(10f, climbing: true, winching: false, _rules, 1f), Is.EqualTo(10f));
        }

        [Test]
        public void Constrain_LeavesASlackRopeAlone()
        {
            var velocity = new Vector3(0f, -5f, 0f);

            Assert.That(LeashProcessor.Constrain(Vector3.zero, velocity, new Vector3(0f, 10f, 0f), 12f, 40f, 0.1f), Is.EqualTo(velocity));
        }

        [Test]
        public void Constrain_ATautRope_HoldsTheBodyAtItsLength()
        {
            var anchor = new Vector3(0f, 10f, 0f);
            var position = new Vector3(0f, -2f, 0f); // 12 m below the anchor: the rope is taut
            var velocity = LeashProcessor.Constrain(position, new Vector3(0f, -5f, 0f), anchor, 12f, 40f, 0.1f);

            Vector3 next = position + velocity * 0.1f;
            Assert.That(Vector3.Distance(next, anchor), Is.EqualTo(12f).Within(1e-3f), "Falling further is stopped by the rope.");
        }

        [Test]
        public void Constrain_DragsTheBodyAfterAMovingAnchor_NoFasterThanTheLimit()
        {
            var velocity = LeashProcessor.Constrain(Vector3.zero, Vector3.zero, new Vector3(100f, 0f, 0f), 12f, 40f, 0.1f);

            Assert.That(velocity.magnitude, Is.EqualTo(40f).Within(1e-3f));
            Assert.That(velocity.x, Is.GreaterThan(0f), "Toward the anchor.");
        }

        [Test]
        public void HasArrived_NearThePoint_OrWithTheRopeAllIn()
        {
            Assert.That(LeashProcessor.HasArrived(Vector3.zero, new Vector3(0f, 1f, 0f), 5f, 1.5f), Is.True);
            Assert.That(LeashProcessor.HasArrived(Vector3.zero, new Vector3(0f, 9f, 0f), 1f, 1.5f), Is.True);
            Assert.That(LeashProcessor.HasArrived(Vector3.zero, new Vector3(0f, 9f, 0f), 9f, 1.5f), Is.False);
        }
    }
}
