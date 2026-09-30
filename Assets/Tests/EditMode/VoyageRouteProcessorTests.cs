#nullable enable
using NUnit.Framework;
using TinCan.Features.Voyage;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="VoyageRouteProcessor"/>: the route is measured level, from where the ship starts.</summary>
    public class VoyageRouteProcessorTests
    {
        private readonly VoyageRouteProcessor _route = new();

        [Test]
        public void Destination_IsAheadAlongTheLevelHeading_AtTheShipsAltitude()
        {
            var pitchedAndTurned = Quaternion.Euler(-20f, 90f, 0f); // nose up, heading +X

            Vector3 destination = _route.Destination(new Vector3(0f, 40f, 0f), pitchedAndTurned, 1000f);

            Assert.That(Vector3.Distance(destination, new Vector3(1000f, 40f, 0f)), Is.LessThan(1e-2f));
        }

        [Test]
        public void Progress_RunsFromZeroToOne_AndIgnoresAltitude()
        {
            var destination = new Vector3(0f, 0f, 1000f);

            Assert.That(_route.Progress(Vector3.zero, destination, 1000f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(_route.Progress(new Vector3(0f, 300f, 250f), destination, 1000f), Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(_route.Progress(new Vector3(0f, 0f, -500f), destination, 1000f), Is.Zero, "flying away never goes below 0");
        }

        [Test]
        public void Arrival_WithinTheRadius_MeasuredLevel()
        {
            var destination = new Vector3(0f, 0f, 1000f);

            Assert.That(_route.HasArrived(new Vector3(0f, 200f, 950f), destination, 60f), Is.True, "height does not count");
            Assert.That(_route.HasArrived(new Vector3(0f, 0f, 900f), destination, 60f), Is.False);
        }

        [Test]
        public void Bearing_IsPositiveToStarboard()
        {
            var destination = new Vector3(100f, 0f, 100f);

            Assert.That(_route.Bearing(Vector3.zero, Quaternion.identity, destination), Is.EqualTo(45f).Within(1e-3f));
            Assert.That(_route.Bearing(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), destination), Is.EqualTo(-45f).Within(1e-3f));
        }
    }
}
