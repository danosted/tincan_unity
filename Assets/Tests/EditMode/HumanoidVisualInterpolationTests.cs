#nullable enable
using NUnit.Framework;
using TinCan.Features.HumanoidMovement;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class HumanoidVisualInterpolationTests
    {
        private const float Tick = 1f / 30f;

        private static Vector3 Draw(HumanoidVisualInterpolation interpolation, float time, float deltaTime = 0f)
        {
            interpolation.Evaluate(time, deltaTime, out var position, out _);
            return position;
        }

        [Test]
        public void BetweenTicks_BlendsLastTwoPoses()
        {
            var interpolation = new HumanoidVisualInterpolation();
            interpolation.Commit(null, Vector3.zero, Quaternion.identity, 0f);
            interpolation.Commit(null, new Vector3(0.3f, 0f, 0f), Quaternion.identity, Tick);

            Assert.That(Draw(interpolation, Tick).x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Draw(interpolation, Tick * 1.5f).x, Is.EqualTo(0.15f).Within(1e-4f));
            Assert.That(Draw(interpolation, Tick * 3f).x, Is.EqualTo(0.3f).Within(1e-5f)); // holds the latest pose, no overshoot
        }

        [Test]
        public void OnPlatform_FollowsThePlatformsCurrentPose()
        {
            var ship = new GameObject("Ship");
            try
            {
                var interpolation = new HumanoidVisualInterpolation();
                interpolation.Commit(ship.transform, new Vector3(1f, 0f, 0f), Quaternion.identity, 0f);
                interpolation.Commit(ship.transform, new Vector3(1f, 0f, 0f), Quaternion.identity, Tick);

                ship.transform.position = new Vector3(0f, 0f, 5f); // the ship moved between ticks

                Assert.That(Vector3.Distance(Draw(interpolation, Tick * 1.5f), new Vector3(1f, 0f, 5f)), Is.LessThan(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        [Test]
        public void Correction_IsHiddenThenFadesOut()
        {
            var interpolation = new HumanoidVisualInterpolation();
            interpolation.Commit(null, Vector3.zero, Quaternion.identity, 0f);
            interpolation.Commit(null, Vector3.zero, Quaternion.identity, Tick);

            interpolation.AbsorbCorrection(new Vector3(0.5f, 0f, 0f));

            Assert.That(Draw(interpolation, Tick).x, Is.EqualTo(0f).Within(1e-4f), "no pop when the body jumps");
            Assert.That(Draw(interpolation, Tick, 0.5f).x, Is.EqualTo(0.5f).Within(0.01f), "fades to the corrected pose");
        }

        [Test]
        public void TeleportSizedCorrection_IsShownAtOnce()
        {
            var interpolation = new HumanoidVisualInterpolation();
            interpolation.Commit(null, Vector3.zero, Quaternion.identity, 0f);
            interpolation.Commit(null, Vector3.zero, Quaternion.identity, Tick);

            interpolation.AbsorbCorrection(new Vector3(10f, 0f, 0f));

            Assert.That(Draw(interpolation, Tick).x, Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void TeleportBetweenTicks_DoesNotStreak()
        {
            var interpolation = new HumanoidVisualInterpolation();
            interpolation.Commit(null, Vector3.zero, Quaternion.identity, 0f);
            interpolation.Commit(null, new Vector3(50f, 0f, 0f), Quaternion.identity, Tick);

            Assert.That(Draw(interpolation, Tick * 1.5f).x, Is.EqualTo(50f).Within(1e-4f));
        }

        [Test]
        public void NewPlatform_StartsFresh()
        {
            var ship = new GameObject("Ship");
            try
            {
                var interpolation = new HumanoidVisualInterpolation();
                interpolation.Commit(null, Vector3.zero, Quaternion.identity, 0f);
                interpolation.Commit(ship.transform, new Vector3(2f, 0f, 0f), Quaternion.identity, Tick);

                Assert.That(Draw(interpolation, Tick * 1.5f).x, Is.EqualTo(2f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        [Test]
        public void NoRecentTicks_IsInactive()
        {
            var interpolation = new HumanoidVisualInterpolation();
            Assert.That(interpolation.IsActive(0f), Is.False);

            interpolation.Commit(null, Vector3.zero, Quaternion.identity, 1f);
            Assert.That(interpolation.IsActive(1.1f), Is.True);
            Assert.That(interpolation.IsActive(2f), Is.False);
        }
    }
}
