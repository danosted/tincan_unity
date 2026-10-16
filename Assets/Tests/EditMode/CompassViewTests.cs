#nullable enable
using NUnit.Framework;
using TinCan.Features.Navigation;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="CompassView"/>'s math: the arrow, the level water line, on course. Plan: navigation-compass.md.</summary>
    public class CompassViewTests
    {
        private static void AssertDirection(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Angle(actual, expected), Is.LessThan(0.5f), $"{actual} vs {expected}");

        [Test]
        public void Arrow_PointsAtTheTarget_InTheWorld()
        {
            var compass = Quaternion.Euler(10f, 35f, -5f);
            var from = new Vector3(3f, 4f, 5f);
            var to = new Vector3(-200f, 90f, 400f);

            var local = CompassView.ArrowLocalRotation(compass, from, to);

            AssertDirection(compass * local * Vector3.forward, to - from);
        }

        [Test]
        public void Arrow_DeadAhead_IsStraightOverTheBow()
        {
            var ship = Quaternion.Euler(0f, 90f, 0f);
            var local = CompassView.ArrowLocalRotation(ship, Vector3.zero, new Vector3(500f, 0f, 0f));

            AssertDirection(local * Vector3.forward, Vector3.forward);
        }

        [Test]
        public void Arrow_TargetAbove_TiltsUpInTheCompass()
        {
            var local = CompassView.ArrowLocalRotation(Quaternion.identity, Vector3.zero, new Vector3(0f, 100f, 100f));

            Assert.That((local * Vector3.forward).y, Is.GreaterThan(0.6f));
        }

        [Test]
        public void WaterLine_StaysLevel_WhileTheShipPitchesAndBanks()
        {
            var ship = Quaternion.Euler(-20f, 70f, 15f);

            var local = CompassView.WaterLineLocalRotation(ship);

            AssertDirection(ship * local * Vector3.up, Vector3.up);
            var forward = ship * local * Vector3.forward;
            Assert.That(Mathf.Abs(forward.y), Is.LessThan(1e-4f), "lies level");
            AssertDirection(forward, Vector3.ProjectOnPlane(ship * Vector3.forward, Vector3.up));
        }

        [Test]
        public void OffCourse_IsZero_WhenTheBowPointsAtTheTarget_InEveryDirection()
        {
            var to = new Vector3(100f, 60f, 300f);
            var aimed = Quaternion.LookRotation(to);

            Assert.That(CompassView.OffCourseDegrees(aimed, Vector3.zero, to), Is.LessThan(0.01f));
            Assert.That(CompassView.OffCourseDegrees(Quaternion.identity, Vector3.zero, to), Is.GreaterThan(10f), "level heading misses the climb");
        }
    }
}
