#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>ShipHullSurfaceProcessor: a frame's planked faces as hull surfaces (what designed ships offer ship damage).</summary>
    public class ShipHullSurfaceProcessorTests
    {
        [Test]
        public void TwoStoryBox_GivesTwoFloorsAndFourWalls_WithPointsInsideTheirFaces()
        {
            var surfaces = ShipHullSurfaceProcessor.Surfaces(ShipFrameShape.From(ShipFrameTestDesigns.TwoStoryBox()));

            Assert.That(surfaces.Count(s => !s.IsWall), Is.EqualTo(2));
            Assert.That(surfaces.Count(s => s.IsWall), Is.EqualTo(4));

            var floors = surfaces.Where(s => !s.IsWall).OrderBy(s => s.Story).ToArray();
            Assert.That(floors.Select(s => s.Story), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(floors[1].LocalPoint.y, Is.GreaterThan(floors[0].LocalPoint.y), "the upper floor is higher");
            foreach (var floor in floors)
            {
                Assert.That(floor.LocalNormal, Is.EqualTo(Vector3.up));
                Assert.That(Mathf.Abs(floor.LocalPoint.x), Is.LessThan(2f), "inside the 4 x 4 m floor");
                Assert.That(Mathf.Abs(floor.LocalPoint.z), Is.LessThan(2f));
                Assert.That(floor.Area, Is.EqualTo(16f).Within(0.01f));
            }
        }

        [Test]
        public void OpenFaces_AreLeftOut()
        {
            var shape = ShipFrameShape.From(ShipFrameTestDesigns.DeckFrame(ShipFrameFill.Open));
            Assert.That(ShipHullSurfaceProcessor.Surfaces(shape), Is.Empty);
        }
    }
}
