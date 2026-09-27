#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.Targeting;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class TargetingGizmosTests
    {
        private sealed class FakeTargetable : ITargetable
        {
            public Vector3 AimPoint { get; set; }
            public bool IsTargetable => true;
            public IAbilityControllerBase? Controller => null;
        }

        private readonly List<GizmoLine> _lines = new();
        private readonly TargetingOrigin _origin = new(Vector3.zero, Quaternion.identity, eyeHeight: 1.5f);
        private TargetingDefinition _definition = null!;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<TargetingDefinition>();
            _definition.Range = 3f;
            _definition.Radius = 1f;
            _definition.HorizontalAngle = 90f;
            _definition.VerticalAngle = 60f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_definition);

        [Test]
        public void Ray_IsDrawnFromTheSourceAlongTheAim_ForItsRange()
        {
            _definition.Shape = TargetShape.Ray;

            TargetingGizmos.Build(_origin, _definition, null, _lines);

            var eye = new Vector3(0f, 1.5f, 0f);
            Assert.That(_lines.Any(line => line.From == eye && Vector3.Distance(line.To, eye + Vector3.forward * 3f) < 0.001f), Is.True);
            Assert.That(_lines.All(line => line.Color == TargetingGizmos.Miss), Is.True, "Nothing acquired draws red.");
        }

        [Test]
        public void AcquiredTarget_TurnsTheShapeGreen_AndAddsALineToIt()
        {
            _definition.Shape = TargetShape.Ray;
            var target = new FakeTargetable { AimPoint = new Vector3(0f, 1.5f, 2f) };

            TargetingGizmos.Build(_origin, _definition, target, _lines);

            Assert.That(_lines.Any(line => line.Color == TargetingGizmos.Hit), Is.True);
            Assert.That(_lines.Any(line => line.Color == TargetingGizmos.Target && line.To == target.AimPoint), Is.True);
        }

        [Test]
        public void Cone_EdgesReachTheRange_AtHalfItsAngles()
        {
            _definition.Shape = TargetShape.Cone;
            var eye = new Vector3(0f, 1.5f, 0f);

            TargetingGizmos.Build(_origin, _definition, null, _lines);

            var edgeRight = eye + Quaternion.AngleAxis(45f, Vector3.up) * Vector3.forward * 3f;
            Assert.That(_lines.Any(line => line.From == eye && Vector3.Distance(line.To, edgeRight) < 0.001f), Is.True, "Horizontal edge at +45 deg.");
            Assert.That(_lines.Where(line => line.From == eye).All(line => Mathf.Abs(Vector3.Distance(line.From, line.To) - 3f) < 0.001f), Is.True);
        }

        [Test]
        public void Cone_FollowsThePitch_ForEyeAim()
        {
            _definition.Shape = TargetShape.Cone;
            _definition.Source = AimSource.EyeAim;
            var down = new TargetingOrigin(Vector3.zero, Quaternion.identity, 1.5f, aimPitch: 30f);

            TargetingGizmos.Build(down, _definition, null, _lines);

            var eye = new Vector3(0f, 1.5f, 0f);
            var centre = eye + Quaternion.Euler(30f, 0f, 0f) * Vector3.forward * 3f;
            Assert.That(_lines.Any(line => line.From == eye && Vector3.Distance(line.To, centre) < 0.001f), Is.True, "The centre line points down the aim.");
        }

        [Test]
        public void Sphere_RingsSitAtTheRadius()
        {
            _definition.Shape = TargetShape.Sphere;
            var eye = new Vector3(0f, 1.5f, 0f);

            TargetingGizmos.Build(_origin, _definition, null, _lines);

            var ringPoints = _lines.Where(line => Vector3.Distance(line.From, eye) > 0.5f).Select(line => line.From).ToList();
            Assert.That(ringPoints, Is.Not.Empty);
            Assert.That(ringPoints.All(point => Mathf.Abs(Vector3.Distance(point, eye) - 1f) < 0.001f), Is.True);
        }
    }
}
