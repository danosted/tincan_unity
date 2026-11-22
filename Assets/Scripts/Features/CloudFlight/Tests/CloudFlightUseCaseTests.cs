#nullable enable
using NUnit.Framework;
using TinCan.Features.CloudBoundary;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.CloudFlight.Tests
{
    /// <summary>
    /// <see cref="CloudFlightUseCase"/>: the ship's height is measured against the cloud surface under it, so over a hole
    /// (where the surface drops away) there is no cloud to hold it. Plan: .docs/plans/cloud-sea.md, "Soaring and cloud shelves".
    /// </summary>
    public class CloudFlightUseCaseTests
    {
        private sealed class FixedSurface : ICloudSurfaceQuery
        {
            public float Height;

            public float GetSurfaceHeight(float worldX, float worldZ) => Height;
        }

        private readonly FixedSurface _surface = new();
        private CloudFlightConfig _config = null!;
        private FakeAirshipView _ship = null!;
        private CloudFlightUseCase _flight = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CloudFlightConfig>();
            _ship = new FakeAirshipView();
            _flight = new CloudFlightUseCase(_surface, new CloudFlightProcessor(), _config);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AShipBelowItsRestHeight_IsInTheCloud_AndASlowOneFloatsUp()
        {
            _surface.Height = 100f;
            _ship.Transform.position = new Vector3(0f, 100f + _config.RestHeight - 10f, 0f);

            Assert.That(_flight.HeightAboveRest(0f, _ship.Transform.position.y, 0f), Is.EqualTo(-10f).Within(1e-4f));
            Assert.That(_flight.Evaluate(_ship, 0f, 0f, 0.02f).Vertical, Is.GreaterThan(0f));
        }

        [Test]
        public void OverAHole_TheSameShipHasNoCloud_AndThePullBuildsStepByStep()
        {
            _surface.Height = 100f - 400f; // the hole's drop
            _ship.Transform.position = new Vector3(0f, 100f + _config.RestHeight - 10f, 0f);

            float first = _flight.Evaluate(_ship, 15f, 0f, 0.02f).Vertical;
            float later = first;
            for (int i = 0; i < 50; i++) later = _flight.Evaluate(_ship, 15f, 0f, 0.02f).Vertical;

            Assert.That(first, Is.LessThan(0f));
            Assert.That(later, Is.LessThan(first - 1f), "the fall carries from step to step");
        }
    }
}
