#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Targeting;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class TargetingProcessorTests
    {
        private readonly TargetingProcessor _processor = new();
        private readonly List<Object> _assets = new();
        private readonly TargetingOrigin _origin = new(Vector3.zero, Quaternion.identity, eyeHeight: 1.5f);

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private TargetingDefinition Definition(TargetShape shape, TargetSelection selection = TargetSelection.Nearest)
        {
            var definition = ScriptableObject.CreateInstance<TargetingDefinition>();
            definition.Shape = shape;
            definition.Selection = selection;
            definition.Range = 2.5f;
            definition.Radius = 1f;
            definition.HorizontalAngle = 100f;
            definition.VerticalAngle = 120f;
            _assets.Add(definition);
            return definition;
        }

        private int Select(TargetingDefinition definition, params Vector3[] points)
        {
            var candidates = new List<TargetCandidate>();
            for (int i = 0; i < points.Length; i++) candidates.Add(new TargetCandidate(i, points[i]));
            Vector3 source = _processor.SourcePoint(_origin, definition);
            return _processor.TrySelect(_origin, source, definition, candidates, out var result) ? result.Index : -1;
        }

        [Test]
        public void SourcePoint_IsTheEye_OrABodyOffset()
        {
            var cone = Definition(TargetShape.Cone);
            Assert.That(_processor.SourcePoint(_origin, cone), Is.EqualTo(new Vector3(0f, 1.5f, 0f)));

            cone.Source = AimSource.BodyOffset;
            cone.SourceOffset = new Vector3(0f, 1f, 1.8f);
            var turned = new TargetingOrigin(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), 1.5f);
            Vector3 point = _processor.SourcePoint(turned, cone);
            Assert.That(Vector3.Distance(point, new Vector3(1.8f, 1f, 0f)), Is.LessThan(0.001f), "Offsets rotate with the body.");
        }

        [Test]
        public void Cone_IncludesInFrontWithinRange_ExcludesBehindAndFar()
        {
            var cone = Definition(TargetShape.Cone);

            Assert.That(Select(cone, new Vector3(0f, 1.5f, 2f)), Is.EqualTo(0), "in front");
            Assert.That(Select(cone, new Vector3(0f, 1.5f, -2f)), Is.EqualTo(-1), "behind");
            Assert.That(Select(cone, new Vector3(0f, 1.5f, 3f)), Is.EqualTo(-1), "out of range");
            Assert.That(Select(cone, new Vector3(2f, 1.5f, 0.3f)), Is.EqualTo(-1), "outside the 100 degree width");
        }

        [Test]
        public void Cone_VerticalAngleLimitsElevation()
        {
            var cone = Definition(TargetShape.Cone);
            cone.VerticalAngle = 60f;

            Assert.That(Select(cone, new Vector3(0f, 1.0f, 2f)), Is.EqualTo(0), "slightly below the eye (14 deg)");
            Assert.That(Select(cone, new Vector3(0f, 0.2f, 1f)), Is.EqualTo(-1), "steeply below (52 deg) is outside +-30");
        }

        [Test]
        public void Cone_UsesTheBodyFrame_NotWorldUp()
        {
            var cone = Definition(TargetShape.Cone);
            var tilted = new TargetingOrigin(Vector3.zero, Quaternion.Euler(0f, 0f, 30f), 1.5f);
            Vector3 eye = tilted.Eye;
            var candidates = new List<TargetCandidate> { new(0, eye + tilted.Forward * 2f) };

            Assert.That(_processor.TrySelect(tilted, eye, cone, candidates, out _), Is.True, "A rolled deck rolls the cone with it.");
        }

        [Test]
        public void Sphere_UsesRadiusAroundTheSource()
        {
            var sphere = Definition(TargetShape.Sphere);

            Assert.That(Select(sphere, new Vector3(0.5f, 1.5f, -0.5f)), Is.EqualTo(0), "behind but within the radius");
            Assert.That(Select(sphere, new Vector3(0f, 1.5f, 1.5f)), Is.EqualTo(-1));
        }

        [Test]
        public void Selection_NearestVersusBestAligned()
        {
            var near = new Vector3(0.9f, 1.5f, 0.9f);
            var aligned = new Vector3(0f, 1.5f, 2.2f);

            Assert.That(Select(Definition(TargetShape.Cone, TargetSelection.Nearest), aligned, near), Is.EqualTo(1));
            Assert.That(Select(Definition(TargetShape.Cone, TargetSelection.BestAligned), aligned, near), Is.EqualTo(0));
        }

        [Test]
        public void FirstHit_UsesTheRayDistance()
        {
            var ray = Definition(TargetShape.Ray, TargetSelection.FirstHit);
            Vector3 source = _processor.SourcePoint(_origin, ray);
            var candidates = new List<TargetCandidate>
            {
                new(0, new Vector3(0f, 1.5f, 1f), hitDistance: 2f),
                new(1, new Vector3(0f, 1.5f, 2f), hitDistance: 0.5f)
            };

            Assert.That(_processor.TrySelect(_origin, source, ray, candidates, out var result), Is.True);
            Assert.That(result.Index, Is.EqualTo(1), "The collider hit first wins, not the nearest aim point.");
        }

        [Test]
        public void EyeAim_CentresTheConeOnThePitch()
        {
            var narrow = Definition(TargetShape.Cone);
            narrow.Source = AimSource.EyeAim;
            narrow.VerticalAngle = 16f;
            Vector3 below = new(0f, 0.8f, 1.5f); // 25 degrees below the eye

            var level = new TargetingOrigin(Vector3.zero, Quaternion.identity, 1.5f, aimPitch: 0f);
            var down = new TargetingOrigin(Vector3.zero, Quaternion.identity, 1.5f, aimPitch: 25f);
            var candidates = new List<TargetCandidate> { new(0, below) };

            Assert.That(_processor.TrySelect(level, level.Eye, narrow, candidates, out _), Is.False, "Looking level misses a part 25 deg below.");
            Assert.That(_processor.TrySelect(down, down.Eye, narrow, candidates, out _), Is.True, "Looking down 25 deg (positive pitch) finds it.");
        }

        [Test]
        public void BodyForward_IgnoresPitch()
        {
            var narrow = Definition(TargetShape.Cone);
            narrow.VerticalAngle = 16f;
            var down = new TargetingOrigin(Vector3.zero, Quaternion.identity, 1.5f, aimPitch: 25f);
            var candidates = new List<TargetCandidate> { new(0, new Vector3(0f, 0.8f, 1.5f)) };

            Assert.That(_processor.TrySelect(down, down.Eye, narrow, candidates, out _), Is.False);
        }

        [Test]
        public void CameraAim_StartsAtTheOrbitCentre_AndFollowsThePitch()
        {
            var camera = Definition(TargetShape.Ray);
            camera.Source = AimSource.CameraAim;
            var origin = new TargetingOrigin(new Vector3(1f, 0f, 0f), Quaternion.identity, 1.5f, aimPitch: 90f, orbitHeight: 0.4f);

            Assert.That(_processor.SourcePoint(origin, camera), Is.EqualTo(new Vector3(1f, 0.4f, 0f)));
            Assert.That(Vector3.Dot(_processor.Direction(origin, camera), Vector3.down), Is.GreaterThan(0.999f), "Pitch 90 looks straight down.");
        }

        [Test]
        public void Ray_MeasuresRangeToTheHit_NotToThePivot()
        {
            var ray = Definition(TargetShape.Ray, TargetSelection.FirstHit);
            ray.Range = 3f;
            Vector3 source = _processor.SourcePoint(_origin, ray);
            // A tall volume whose pivot sits on the deck 3.2 m away, but the ray met its side at 2.8 m.
            var candidates = new List<TargetCandidate> { new(0, new Vector3(0f, 0f, 3.2f), hitDistance: 2.8f) };

            Assert.That(_processor.TrySelect(_origin, source, ray, candidates, out _), Is.True);
        }

        [Test]
        public void NoCandidates_SelectsNothing()
        {
            Assert.That(Select(Definition(TargetShape.Cone)), Is.EqualTo(-1));
        }
    }
}
