#nullable enable
using NUnit.Framework;
using TinCan.Features.Navigation;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="RouteEditProcessor"/>: picking, bending the nearest leg ahead, removing. Plan: navigation-compass.md.</summary>
    public class RouteEditProcessorTests
    {
        private readonly RouteEditProcessor _edit = new();

        [Test]
        public void Nearest_PicksTheClosestWithinTheRadius()
        {
            var points = new[] { new Vector2(0f, 0.5f), new Vector2(0.3f, 0.5f) };

            Assert.That(_edit.Nearest(points, new Vector2(0.27f, 0.5f), 0.05f), Is.EqualTo(1));
            Assert.That(_edit.Nearest(points, new Vector2(0.15f, 0.5f), 0.05f), Is.EqualTo(-1));
        }

        [Test]
        public void Insertion_IntoAnEmptyRoute_IsTheFirst()
        {
            Assert.That(_edit.Insertion(System.Array.Empty<Vector2>(), -1, new Vector2(0.2f, 0.2f)).Index, Is.EqualTo(0));
        }

        [Test]
        public void Insertion_BendsTheNearestLegAhead()
        {
            // Ship (0,0) -> A (0,0.5) -> B (0.5,0.5).
            var points = new[] { new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f) };

            var first = _edit.Insertion(points, 0, new Vector2(0.05f, 0.25f));
            Assert.That(first.Index, Is.EqualTo(0), "the leg from the ship to A");
            Assert.That(first.Along, Is.EqualTo(0.5f).Within(0.01f));

            var second = _edit.Insertion(points, 0, new Vector2(0.25f, 0.55f));
            Assert.That(second.Index, Is.EqualTo(1), "the leg from A to B");
        }

        [Test]
        public void Insertion_NeverGoesBehindTheActiveWaypoint()
        {
            var points = new[] { new Vector2(0f, -0.5f), new Vector2(0f, 0.5f) };

            Assert.That(_edit.Insertion(points, 1, new Vector2(0f, -0.25f)).Index, Is.EqualTo(1), "the passed leg is not bent");
        }

        [Test]
        public void Removal_KeepsTheCompassOnItsTarget()
        {
            Assert.That(_edit.ActiveAfterRemoval(active: 2, removed: 0, countAfter: 3), Is.EqualTo(1));
            Assert.That(_edit.ActiveAfterRemoval(active: 1, removed: 1, countAfter: 2), Is.EqualTo(1), "the next one takes over");
            Assert.That(_edit.ActiveAfterRemoval(active: 0, removed: 0, countAfter: 0), Is.EqualTo(-1));
        }
    }
}
