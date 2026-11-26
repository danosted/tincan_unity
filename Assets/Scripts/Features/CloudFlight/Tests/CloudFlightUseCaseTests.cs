#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Ship;
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
            public float RisePerMetreZ;
            /// <summary>North of this the surface drops 400 m (a rift).</summary>
            public float RiftFromZ = float.PositiveInfinity;

            public float GetSurfaceHeight(float worldX, float worldZ) =>
                Height + RisePerMetreZ * worldZ - (worldZ >= RiftFromZ ? 400f : 0f);

            public float GetSeaHeight(float worldX, float worldZ) => Height + RisePerMetreZ * worldZ;
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
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void AShipBelowItsRestHeight_IsInTheCloud_AndASlowOneFloatsUp()
        {
            _surface.Height = 100f;
            _ship.Transform.position = new Vector3(0f, 100f + _config.RestHeight - 10f, 0f);

            Assert.That(_flight.HeightAboveRest(_ship), Is.EqualTo(-10f).Within(1e-4f));
            Assert.That(_flight.Evaluate(_ship, 0f, 0f, 0.02f).Vertical, Is.GreaterThan(0f));
        }

        [Test]
        public void ADesignedShip_RestsOnItsKeel_NotItsOrigin()
        {
            _surface.Height = 100f;
            var hulls = new Hulls { Keel = -3.5f };
            var flight = new CloudFlightUseCase(_surface, new CloudFlightProcessor(), _config, new IShipHullSurfaces[] { hulls });
            _ship.Transform.position = new Vector3(0f, 100f + _config.RestHeight + 3.5f, 0f);

            Assert.That(flight.HeightAboveRest(_ship), Is.EqualTo(0f).Within(1e-4f), "the keel is at its rest height");
            Assert.That(_flight.HeightAboveRest(_ship), Is.EqualTo(3.5f).Within(1e-4f), "without hulls, the origin is");
        }

        private sealed class Hulls : IShipHullSurfaces
        {
            public float? Keel;
            public int Version => 1;
            public IReadOnlyList<ShipHullSurface> SurfacesOf(Guid shipId) => Array.Empty<ShipHullSurface>();
            public float? KeelOf(Guid shipId) => Keel;
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

        [Test]
        public void TheSeaTilt_IsTheSurfaceReadAheadAndBehind_AlongTheHeading()
        {
            _surface.Height = 100f;
            _surface.RisePerMetreZ = 0.1f; // rises to the north

            Assert.That(_flight.SeaTilt(Vector3.zero, Vector3.forward), Is.EqualTo(Mathf.Atan(0.1f) * Mathf.Rad2Deg).Within(1e-3f));
            Assert.That(_flight.SeaTilt(Vector3.zero, Vector3.back), Is.LessThan(0f), "heading south it falls away");
            Assert.That(_flight.SeaTilt(Vector3.zero, Vector3.right), Is.EqualTo(0f).Within(1e-4f), "across the slope it is flat");
            Assert.That(_flight.SeaTilt(Vector3.zero, Vector3.down), Is.EqualTo(0f), "no heading straight down");
        }

        [Test]
        public void ARiftAhead_IsLeftOut_TheSlopeIsReadBehind()
        {
            _surface.Height = 100f;
            _surface.RisePerMetreZ = 0.1f;
            _surface.RiftFromZ = 10f; // the reading ahead falls 400 m into it

            Assert.That(_flight.SeaTilt(Vector3.zero, Vector3.forward), Is.EqualTo(Mathf.Atan(0.1f) * Mathf.Rad2Deg).Within(1e-3f));
        }
    }
}
