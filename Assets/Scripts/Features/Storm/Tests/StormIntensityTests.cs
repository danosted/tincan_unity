#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.Encounters;
using UnityEngine;

namespace TinCan.Features.Storm.Tests
{
    /// <summary>The storm's depth, strength and region (<see cref="StormIntensity"/>): from the nearest zone, or the lab's controls.</summary>
    public class StormIntensityTests
    {
        private sealed class Zone : IEncounterZone
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public string DisplayName => "Storm";
            public Vector2 Centre => Vector2.zero;
            public float Radius => 100f;
            public EncounterZoneState State { get; set; } = EncounterZoneState.Running;
            public double StateEndsAt => 0d;
            public void ServerSetCentre(Vector2 centre) { }
            public void ServerSetState(EncounterZoneState state, double endsAt) => State = state;
        }

        private sealed class Zones : IEncounterZones
        {
            public readonly Zone Zone = new();

            public (IEncounterZone Zone, float Depth)? ZoneAt(Vector3 point)
            {
                float depth = EncounterZoneProcessor.Depth(Zone.Centre, Zone.Radius, point);
                return depth > 0f ? (Zone, depth) : null;
            }

            public IEncounterZone? Nearest(Vector3 point) => Zone;
        }

        private sealed class NoView : ILocalViewCamera
        {
            public Camera? Camera => null;
        }

        private GameObject? _labObject;

        [TearDown]
        public void TearDown()
        {
            if (_labObject != null) UnityEngine.Object.DestroyImmediate(_labObject);
        }

        private StormLabControls Lab()
        {
            _labObject = new GameObject("Lab");
            return _labObject.AddComponent<StormLabControls>();
        }

        [Test]
        public void TheNearestZone_GivesTheRegion_AndTheDepthWhereTheCameraIs()
        {
            var zones = new Zones();
            var intensity = new StormIntensity(zones, new NoView());

            intensity.Step(new Vector3(0f, 50f, 50f), 0.1f);
            Assert.That(intensity.Depth, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(intensity.Region, Is.EqualTo((Vector2.zero, 100f)));

            intensity.Step(new Vector3(0f, 0f, 500f), 0.1f);
            Assert.That(intensity.Depth, Is.Zero, "outside, the storm still stands there");
            Assert.That(intensity.Region, Is.Not.Null);
        }

        [Test]
        public void ARestingZone_CalmsTheStorm_OverItsCalmSeconds()
        {
            var zones = new Zones();
            zones.Zone.State = EncounterZoneState.Resting;
            var intensity = new StormIntensity(zones, new NoView());

            intensity.Step(Vector3.zero, StormIntensity.CalmSeconds / 2f);
            Assert.That(intensity.Strength, Is.EqualTo(0.5f).Within(1e-4f));

            intensity.Step(Vector3.zero, StormIntensity.CalmSeconds);
            Assert.That(intensity.Strength, Is.Zero);
        }

        [Test]
        public void TheLabsControls_DriveIt_WithNoRegion_UnlessTheyFollowTheZone()
        {
            var lab = Lab();
            lab.Depth = 0.3f;
            lab.Strength = 0.7f;
            var intensity = new StormIntensity(new Zones(), new NoView(), lab);

            intensity.Step(new Vector3(0f, 0f, 50f), 0.1f);
            Assert.That(intensity.Depth, Is.EqualTo(0.3f));
            Assert.That(intensity.Strength, Is.EqualTo(0.7f));
            Assert.That(intensity.Region, Is.Null);

            lab.FollowZone = true;
            intensity.Step(new Vector3(0f, 0f, 50f), 0.1f);
            Assert.That(intensity.Depth, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void WithoutAZone_ThereIsNoStorm()
        {
            var intensity = new StormIntensity(null, new NoView());

            intensity.Step(Vector3.zero, 0.1f);

            Assert.That(intensity.Depth, Is.Zero);
            Assert.That(intensity.Region, Is.Null);
        }
    }
}
