#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipFrameGeometry"/>: triangulation of any simple outline, facing up, plus the point and segment tests.</summary>
    public class ShipFrameGeometryTests
    {
        /// <summary>An L seen from above: a 4 x 4 m square missing its +X/+Z quarter (12 m²), with a slope along X.</summary>
        private static readonly Vector3[] L =
        {
            new(0f, 0f, 0f), new(0f, 0f, 4f), new(2f, 0.2f, 4f), new(2f, 0.2f, 2f), new(4f, 0.4f, 2f), new(4f, 0.4f, 0f),
        };

        private static float TopDownArea(IReadOnlyList<Vector3> loop, List<int> triangles)
        {
            float area = 0f;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                area += ShipFrameGeometry.AreaXZ(new[] { loop[triangles[i]], loop[triangles[i + 1]], loop[triangles[i + 2]] });
            }

            return area;
        }

        [Test]
        public void AConcaveOutline_IsTriangulated_FacingUp_EitherWayRound()
        {
            foreach (var loop in new[] { L, L.Reverse().ToArray() })
            {
                var triangles = ShipFrameGeometry.Triangulate(loop);

                Assert.That(triangles, Has.Count.EqualTo(3 * 4));
                Assert.That(TopDownArea(loop, triangles), Is.EqualTo(12f).Within(1e-4f));
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    var normal = Vector3.Cross(loop[triangles[i + 1]] - loop[triangles[i]], loop[triangles[i + 2]] - loop[triangles[i]]);
                    Assert.That(normal.y, Is.GreaterThan(0f), $"triangle {i / 3}");
                }
            }
        }

        [Test]
        public void ABowtie_IsNotSimple_AndAnLIs()
        {
            var bowtie = new[] { new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 4f), new Vector3(0f, 0f, 4f), new Vector3(4f, 0f, 0f) };

            Assert.That(ShipFrameGeometry.IsSimpleXZ(bowtie), Is.False);
            Assert.That(ShipFrameGeometry.IsSimpleXZ(L), Is.True);
        }

        [Test]
        public void TheHeightOnATriangle_IsInterpolated_AndOutsideIsNotOnIt()
        {
            var a = new Vector3(0f, 0f, 0f);
            var b = new Vector3(0f, 0f, 4f);
            var c = new Vector3(4f, 1f, 0f);

            Assert.That(ShipFrameGeometry.TryHeightAt(2f, 0f, a, b, c, out float height), Is.True);
            Assert.That(height, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(ShipFrameGeometry.TryHeightAt(3f, 3f, a, b, c, out _), Is.False);
        }

        [Test]
        public void ASegment_HitsABox_OnlyWhenItPassesThrough()
        {
            var box = new Bounds(Vector3.zero, Vector3.one);

            Assert.That(ShipFrameGeometry.SegmentHitsBox(new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f), box), Is.True);
            Assert.That(ShipFrameGeometry.SegmentHitsBox(new Vector3(-2f, 0.6f, 0f), new Vector3(2f, 0.6f, 0f), box), Is.False);
            Assert.That(ShipFrameGeometry.SegmentHitsBox(new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f), box), Is.False);
        }

        [Test]
        public void StrictlyInside_ExcludesTheSides()
        {
            Assert.That(ShipFrameGeometry.StrictlyInsideXZ(1f, 1f, L), Is.True);
            Assert.That(ShipFrameGeometry.StrictlyInsideXZ(3f, 3f, L), Is.False, "the missing quarter");
            Assert.That(ShipFrameGeometry.StrictlyInsideXZ(0f, 2f, L), Is.False, "on a side");
        }
    }
}
