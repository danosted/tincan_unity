#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.Encounters;
using UnityEngine;

namespace TinCan.Features.Storm.Tests
{
    /// <summary>The storm's depth and strength (<see cref="StormIntensity"/>): from the lab's sliders, or from the encounter zone.</summary>
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
        }

        private sealed class NoView : ILocalViewCamera
        {
            public Camera? Camera => null;
        }

        private StormLabSettings _lab = null!;
        private Zones _zones = null!;
        private StormIntensity _intensity = null!;

        [SetUp]
        public void SetUp()
        {
            _lab = ScriptableObject.CreateInstance<StormLabSettings>();
            _zones = new Zones();
            _intensity = new StormIntensity(_lab, _zones, new NoView());
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_lab);

        [Test]
        public void TheSliders_DriveIt_UnlessItFollowsTheZone()
        {
            _lab.LabDepth = 0.3f;
            _lab.LabStrength = 0.7f;

            _intensity.Step(new Vector3(0f, 0f, 50f), 0.1f);

            Assert.That(_intensity.Depth, Is.EqualTo(0.3f));
            Assert.That(_intensity.Strength, Is.EqualTo(0.7f));
        }

        [Test]
        public void FollowingTheZone_TheDepthIsWhereTheCameraIs()
        {
            _lab.FollowZone = true;

            _intensity.Step(new Vector3(0f, 0f, 50f), 0.1f);
            Assert.That(_intensity.Depth, Is.EqualTo(0.5f).Within(1e-4f));

            _intensity.Step(new Vector3(0f, 0f, 500f), 0.1f);
            Assert.That(_intensity.Depth, Is.Zero, "outside the zone");
        }

        [Test]
        public void ARestingZone_CalmsTheStorm_OverItsCalmSeconds()
        {
            _lab.FollowZone = true;
            _zones.Zone.State = EncounterZoneState.Resting;

            _intensity.Step(Vector3.zero, StormIntensity.CalmSeconds / 2f);
            Assert.That(_intensity.Strength, Is.EqualTo(0.5f).Within(1e-4f));

            _intensity.Step(Vector3.zero, StormIntensity.CalmSeconds);
            Assert.That(_intensity.Strength, Is.Zero);
        }
    }
}
