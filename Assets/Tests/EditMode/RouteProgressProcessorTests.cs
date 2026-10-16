#nullable enable
using NUnit.Framework;
using TinCan.Features.Navigation;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="RouteProgressProcessor"/>: when a waypoint counts as reached. Plan: navigation-compass.md.</summary>
    public class RouteProgressProcessorTests
    {
        private readonly RouteProgressProcessor _progress = new();

        [Test]
        public void WithinTheRadius_IsReached()
        {
            Assert.That(_progress.HasReached(new Vector3(0f, 0f, 95f), Vector3.zero, new Vector3(0f, 0f, 100f), 10f), Is.True);
        }

        [Test]
        public void ShortOfThePlane_AndOutsideTheRadius_IsNotReached()
        {
            Assert.That(_progress.HasReached(new Vector3(50f, 0f, 80f), Vector3.zero, new Vector3(0f, 0f, 100f), 10f), Is.False);
        }

        [Test]
        public void PastThePlane_IsReached_EvenOnANearMiss()
        {
            // 40 m to the side, 1 m past the waypoint along the leg.
            Assert.That(_progress.HasReached(new Vector3(40f, 0f, 101f), Vector3.zero, new Vector3(0f, 0f, 100f), 10f), Is.True);
        }

        [Test]
        public void HeightCounts_ForTheRadius()
        {
            Assert.That(_progress.HasReached(new Vector3(0f, 50f, 100f), new Vector3(0f, 50f, 0f), new Vector3(0f, 0f, 100f), 10f), Is.False,
                "50 m above the waypoint and not past its plane");
        }

        [Test]
        public void ActiveAfter_PassesEveryReachedWaypoint_ButNeverTheLast()
        {
            var points = new[] { new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, 200f), new Vector3(0f, 0f, 300f) };

            Assert.That(_progress.ActiveAfter(points, 0, Vector3.zero, new Vector3(0f, 0f, 250f), 10f), Is.EqualTo(2));
            Assert.That(_progress.ActiveAfter(points, 0, Vector3.zero, new Vector3(0f, 0f, 1000f), 10f), Is.EqualTo(2), "the destination stays active");
            Assert.That(_progress.ActiveAfter(points, 0, Vector3.zero, new Vector3(0f, 0f, 50f), 10f), Is.EqualTo(0));
        }

        [Test]
        public void ActiveAfter_AnEmptyRoute_IsMinusOne()
        {
            Assert.That(_progress.ActiveAfter(System.Array.Empty<Vector3>(), 0, Vector3.zero, Vector3.zero, 10f), Is.EqualTo(-1));
        }
    }
}
