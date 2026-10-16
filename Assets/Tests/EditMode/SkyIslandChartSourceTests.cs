#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Navigation;
using TinCan.Features.SkyIslands;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="SkyIslandChartSource"/>: the seeded islands as chart obstacles and hidden landmarks, the same as the
    /// layout the islands are built from. Plan: navigation-compass.md (S3).
    /// </summary>
    public class SkyIslandChartSourceTests
    {
        private sealed class FakeSessionLayout : ISessionLayout
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public int LayoutSeed { get; set; } = 1234;
            public Vector3 Origin { get; set; }
            public Vector3 Destination { get; set; } = new(0f, 40f, 4000f);
        }

        private FakeActorRegistry _actors = null!;
        private SkyIslandConfig _config = null!;
        private FakeSessionLayout _session = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _config = ScriptableObject.CreateInstance<SkyIslandConfig>();
            _config.MainChance = 1f;
            _config.LandmarkMinRadius = 0f;
            _session = new FakeSessionLayout();
            _actors.Register(_session);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);

        private SkyIslandChartSource Source() => new(_actors, new SkyIslandLayoutProcessor(), _config);

        [Test]
        public void Obstacles_AreTheIslandsOfTheLayout_AsRockSpanningTheirHeight()
        {
            var obstacles = new List<ChartObstacle>();
            Source().CollectObstacles(new Vector3(0f, 0f, 2000f), 1500f, obstacles);

            var islands = new List<SkyIslandSpec>();
            new SkyIslandLayoutProcessor().Around(_session.LayoutSeed, new Vector3(0f, 0f, 2000f), 1500f, _config.LayoutRules,
                new[] { _session.Origin, _session.Destination }, islands);

            Assert.That(obstacles, Is.Not.Empty);
            Assert.That(obstacles.All(o => o.Kind == ChartObstacleKind.Rock && o.Bottom < o.Top), Is.True);
            foreach (var island in islands.Where(i => Level(i.Top, new Vector3(0f, 0f, 2000f)) + i.Radius < 1500f))
                Assert.That(obstacles.Any(o => o.Center == island.Top), Is.True, island.ToString());
        }

        [Test]
        public void Obstacles_OutsideTheRadius_AreLeftOut()
        {
            var obstacles = new List<ChartObstacle>();
            Source().CollectObstacles(new Vector3(0f, 0f, 2000f), 400f, obstacles);

            Assert.That(obstacles.All(o => Level(o.Center, new Vector3(0f, 0f, 2000f)) <= 400f + o.Radius + 0.01f), Is.True);
        }

        [Test]
        public void Landmarks_AreHiddenMainIslandsAlongTheRoute_WithStableIds()
        {
            var first = new List<PointOfInterest>();
            var second = new List<PointOfInterest>();
            Source().CollectPointsOfInterest(first);
            Source().CollectPointsOfInterest(second);

            Assert.That(first, Is.Not.Empty);
            Assert.That(first.All(p => !p.StartsKnown && p.DiscoverRadius == _config.LandmarkDiscoverRadius), Is.True);
            Assert.That(first.All(p => p.Id != 0 && p.Id != 1), Is.True);
            Assert.That(first.Select(p => p.Id).Distinct().Count(), Is.EqualTo(first.Count), "one per island");
            Assert.That(second.Select(p => p.Id), Is.EqualTo(first.Select(p => p.Id)), "every peer charts the same");
        }

        [Test]
        public void Landmarks_FollowTheSession()
        {
            var before = new List<PointOfInterest>();
            var source = Source();
            source.CollectPointsOfInterest(before);

            _session.LayoutSeed = 99;
            var after = new List<PointOfInterest>();
            source.CollectPointsOfInterest(after);

            Assert.That(after.Select(p => p.Id), Is.Not.EqualTo(before.Select(p => p.Id)));
        }

        [Test]
        public void SmallIslands_AreNotLandmarks()
        {
            _config.LandmarkMinRadius = 1000f;
            var points = new List<PointOfInterest>();

            Source().CollectPointsOfInterest(points);

            Assert.That(points, Is.Empty);
        }

        private static float Level(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
