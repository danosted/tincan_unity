#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Ship;
using TinCan.Features.Airship.Damage;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>ShipDamageSpotProcessor: where a designed ship's hull damage points go.</summary>
    public class ShipDamageSpotProcessorTests
    {
        private static ShipHullSurface Floor(int id, int story, float area) => new(id, story, false, new Vector3(id, story * 3f, 0f), Vector3.up, area);
        private static ShipHullSurface Wall(int id, int story) => new(id, story, true, Vector3.zero, Vector3.right, 100f);

        [Test]
        public void SpreadsOverStories_TopFirst_LargestFloorOfEachStoryFirst()
        {
            var surfaces = new List<ShipHullSurface>
            {
                Floor(1, 0, 10f), Floor(2, 0, 30f), Floor(3, 1, 5f), Floor(4, 1, 8f), Floor(5, 2, 1f),
            };

            var picked = new ShipDamageSpotProcessor().Pick(surfaces, 4);

            Assert.That(picked.Select(s => s.FaceId), Is.EqualTo(new[] { 5, 4, 2, 3 }));
        }

        [Test]
        public void SkipsWalls_AndGivesFewerWhenThereAreNotEnoughFloors()
        {
            var picked = new ShipDamageSpotProcessor().Pick(new[] { Wall(1, 0), Floor(2, 0, 4f), Wall(3, 1) }, 5);
            Assert.That(picked.Select(s => s.FaceId), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void SameSurfaces_SameSpots()
        {
            var surfaces = new[] { Floor(7, 0, 4f), Floor(3, 0, 4f), Floor(9, 1, 4f) };
            var processor = new ShipDamageSpotProcessor();
            Assert.That(processor.Pick(surfaces, 3).Select(s => s.FaceId), Is.EqualTo(processor.Pick(surfaces.Reverse().ToArray(), 3).Select(s => s.FaceId)));
            Assert.That(processor.Pick(surfaces, 3).Select(s => s.FaceId), Is.EqualTo(new[] { 9, 3, 7 }), "equal areas: lower face id first");
        }
    }
}
