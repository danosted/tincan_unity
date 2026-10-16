#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Navigation;
using TinCan.Features.SkyHazards;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="HazardZoneLayoutProcessor"/>, <see cref="HazardZoneMap"/> and <see cref="SkyHazardChartSource"/>: a
    /// voyage's hazard zones along its route, the same for the same seed, on the chart. Plan: navigation-compass.md (S4).
    /// </summary>
    public class HazardZoneLayoutProcessorTests
    {
        private sealed class FakeSessionLayout : ISessionLayout
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public int LayoutSeed { get; set; } = 42;
            public Vector3 Origin { get; set; } = new(0f, 40f, 0f);
            public Vector3 Destination { get; set; } = new(0f, 40f, 4000f);
        }

        private static readonly HazardZoneRules Rules = new(900f, new Vector2(220f, 400f), 250f, new Vector2(50f, 110f), 40f, 300f);
        private readonly HazardZoneLayoutProcessor _layout = new();

        private List<HazardZone> Zones(int seed)
        {
            var zones = new List<HazardZone>();
            _layout.Zones(seed, new Vector3(0f, 40f, 0f), new Vector3(0f, 40f, 4000f), Rules, zones);
            return zones;
        }

        [Test]
        public void Zones_LieAlongTheRoute_ClearOfItsEnds()
        {
            var zones = Zones(42);

            Assert.That(zones.Count, Is.InRange(2, 4));
            foreach (var zone in zones)
            {
                Assert.That(Mathf.Abs(zone.Center.x), Is.LessThanOrEqualTo(250f), zone.ToString());
                Assert.That(zone.Center.z - zone.Radius, Is.GreaterThan(300f - 0.01f), zone.ToString());
                Assert.That(4000f - zone.Center.z - zone.Radius, Is.GreaterThan(300f - 0.01f), zone.ToString());
                Assert.That(zone.Bottom, Is.LessThan(zone.Center.y).And.LessThan(zone.Top));
            }
        }

        [Test]
        public void TheSameSeed_GivesTheSameZones_AnotherDoesNot()
        {
            Assert.That(Zones(42).Select(z => z.Center), Is.EqualTo(Zones(42).Select(z => z.Center)));
            Assert.That(Zones(43).Select(z => z.Center), Is.Not.EqualTo(Zones(42).Select(z => z.Center)));
        }

        [Test]
        public void Contains_CountsTheAltitudeBand()
        {
            var zone = new HazardZone(new Vector3(0f, 50f, 0f), 100f, 0f, 100f);

            Assert.That(zone.Contains(new Vector3(50f, 50f, 50f)), Is.True);
            Assert.That(zone.Contains(new Vector3(50f, 150f, 50f)), Is.False, "over it");
            Assert.That(zone.Contains(new Vector3(150f, 50f, 0f)), Is.False, "beside it");
        }

        [Test]
        public void TheMap_FollowsTheSession_AndHasNoneWithoutOne()
        {
            var actors = new FakeActorRegistry();
            var config = ScriptableObject.CreateInstance<SkyHazardConfig>();
            try
            {
                var map = new HazardZoneMap(actors, _layout, config);
                Assert.That(map.Zones, Is.Empty, "no voyage");

                var session = new FakeSessionLayout();
                actors.Register(session);
                Assert.That(map.Zones, Is.Not.Empty);

                config.ZonesEnabled = false;
                Assert.That(map.Zones, Is.Empty, "switched off");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TheChart_ShowsZonesInRange_AsHazards()
        {
            var actors = new FakeActorRegistry();
            actors.Register(new FakeSessionLayout());
            var config = ScriptableObject.CreateInstance<SkyHazardConfig>();
            try
            {
                var source = new SkyHazardChartSource(new HazardZoneMap(actors, _layout, config));
                var near = new List<ChartObstacle>();
                var far = new List<ChartObstacle>();

                source.CollectObstacles(new Vector3(0f, 40f, 2000f), 3000f, near);
                source.CollectObstacles(new Vector3(20000f, 40f, 0f), 1000f, far);

                Assert.That(near, Is.Not.Empty);
                Assert.That(near.All(o => o.Kind == ChartObstacleKind.Hazard && o.Bottom < o.Top), Is.True);
                Assert.That(far, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
