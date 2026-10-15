#nullable enable
using NUnit.Framework;
using TinCan.Features.GrappleHook;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class GrappleHookProcessorTests
    {
        private GrappleHookProcessor _processor = null!;
        private GrappleHookConfig _config = null!;

        [SetUp]
        public void SetUp()
        {
            _processor = new GrappleHookProcessor();
            _config = ScriptableObject.CreateInstance<GrappleHookConfig>();
            _config.ReelSpeed = 18f;
            _config.ReelAcceleration = 60f;
            _config.ArriveDistance = 1.5f;
            _config.ArriveSpeed = 4f;
            _config.ArriveHopSpeed = 5f;
            _config.BreakDistance = 70f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void Reel_TurnsTheVelocityTowardTheHookAtTheReelAcceleration()
        {
            var (velocity, outcome) = _processor.Reel(Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 20f), _config, 0.1f);

            Assert.That(outcome, Is.EqualTo(GrappleReelOutcome.Reeling));
            Assert.That(velocity.z, Is.EqualTo(6f).Within(1e-4f), "60 m/s² for 0.1 s.");
            Assert.That(velocity.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Reel_SettlesAtTheReelSpeed()
        {
            var velocity = Vector3.zero;
            for (int i = 0; i < 100; i++) velocity = _processor.Reel(velocity, Vector3.zero, new Vector3(0f, 30f, 0f), _config, 1f / 30f).Velocity;

            Assert.That(velocity.y, Is.EqualTo(18f).Within(1e-3f));
        }

        [Test]
        public void Reel_OvercomesAFall()
        {
            var (velocity, _) = _processor.Reel(new Vector3(0f, -10f, 0f), Vector3.zero, new Vector3(0f, 30f, 0f), _config, 0.5f);

            Assert.That(velocity.y, Is.EqualTo(18f).Within(1e-3f), "Half a second at 60 m/s² covers the 28 m/s from falling to reeling up.");
        }

        [Test]
        public void Reel_WithinArriveDistance_Arrives_KeepingLittleSpeedAndHoppingUp()
        {
            var (velocity, outcome) = _processor.Reel(new Vector3(0f, 0f, 18f), Vector3.zero, new Vector3(0f, 0f, 1f), _config, 1f / 30f);

            Assert.That(outcome, Is.EqualTo(GrappleReelOutcome.Arrived));
            Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.LessThanOrEqualTo(4f + 1e-4f));
            Assert.That(velocity.y, Is.EqualTo(5f).Within(1e-4f), "The hop that carries the holder over a rail.");
        }

        [Test]
        public void Reel_BeyondBreakDistance_Snaps_AndLeavesTheVelocityAlone()
        {
            var falling = new Vector3(0f, -3f, 0f);
            var (velocity, outcome) = _processor.Reel(falling, Vector3.zero, new Vector3(0f, 0f, 80f), _config, 1f / 30f);

            Assert.That(outcome, Is.EqualTo(GrappleReelOutcome.Snapped));
            Assert.That(velocity, Is.EqualTo(falling));
        }

        [Test]
        public void Reach_IsInRangeUpToMaxRange_OutOfRangeBeyond_NoneWithoutASurface()
        {
            Assert.That(GrappleHookProcessor.Reach(null, 50f), Is.EqualTo(GrappleAimReach.None));
            Assert.That(GrappleHookProcessor.Reach(50f, 50f), Is.EqualTo(GrappleAimReach.InRange));
            Assert.That(GrappleHookProcessor.Reach(50.01f, 50f), Is.EqualTo(GrappleAimReach.OutOfRange));
        }

        [Test]
        public void AimDirection_FollowsYawAndPitch_PositivePitchLooksDown()
        {
            Vector3 level = GrappleHookProcessor.AimDirection(Quaternion.Euler(0f, 90f, 0f), 0f);
            Vector3 down = GrappleHookProcessor.AimDirection(Quaternion.identity, 30f);

            Assert.That(Vector3.Distance(level, Vector3.right), Is.LessThan(1e-4f));
            Assert.That(down.y, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(down.z, Is.GreaterThan(0f));
        }
    }
}
