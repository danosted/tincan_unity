#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Features.SkyIslands.Tests
{
    /// <summary><see cref="SkyIslandSiteProcessor"/>: clearings and caves on an island's top, the same every time, with room.</summary>
    public class SkyIslandSiteProcessorTests
    {
        private SkyIslandConfig _config = null!;
        private readonly SkyIslandSiteProcessor _processor = new();

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<SkyIslandConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void ASatellite_HasNoSites()
        {
            var sites = Sites(new SkyIslandSpec(new SkyIslandId(1, 0, 0, 2), Vector3.zero, 12f, 20f, 3u));

            Assert.That(sites.Clearings, Is.Empty);
            Assert.That(sites.Caves, Is.Empty);
        }

        [Test]
        public void TheMiddle_IsTheFirstClearing_OnTheGround_FacingByTheShapeSeed()
        {
            var island = Main(1234u, 60f);
            var sites = Sites(island);

            var middle = sites.Clearings[0];
            Assert.That(middle.position.x, Is.Zero);
            Assert.That(middle.position.z, Is.Zero);
            Assert.That(middle.position.y, Is.EqualTo(new SkyIslandShape(island).Surface(0f, 0f)).Within(1e-4f));
            Assert.That(Quaternion.Angle(middle.rotation, Quaternion.Euler(0f, 1234u % 360u, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void ASmallMainIsland_HasNoCave()
        {
            for (uint seed = 0; seed < 50; seed++)
                Assert.That(Sites(Main(seed, _config.CaveMinIslandRadius - 1f)).Caves, Is.Empty, $"seed {seed}");
        }

        [Test]
        public void BigMainIslands_HaveACaveAboutAsOftenAsTheChance()
        {
            int withCave = Enumerable.Range(0, 200).Count(seed => Sites(Main((uint)seed, 85f)).Caves.Count == 1);

            Assert.That(withCave, Is.InRange(200 * _config.CaveChance * 0.6f, 200 * _config.CaveChance * 1.1f));
        }

        [Test]
        public void ALandmass_HasSeveralCavesAndClearings()
        {
            int caves = 0, clearings = 0;
            for (uint seed = 0; seed < 20; seed++)
            {
                var sites = Sites(Landmass(seed));
                Assert.That(sites.Caves.Count, Is.InRange(0, _config.LandmassCaves.y), $"seed {seed}");
                caves += sites.Caves.Count;
                clearings += sites.Clearings.Count - 1 - sites.Caves.Count;
            }

            Assert.That(caves, Is.GreaterThanOrEqualTo(20 * _config.LandmassCaves.x * 0.8f), "most landmasses find room for their caves");
            Assert.That(clearings, Is.GreaterThanOrEqualTo(20 * _config.LandmassClearings.x * 0.8f), "and their open clearings");
        }

        [Test]
        public void EveryCave_StandsInsideTheRim_OnFlatGround_ClearOfTheOtherSites()
        {
            int checkedCaves = 0;
            for (uint seed = 0; seed < 60; seed++)
            {
                var island = seed % 3 == 0 ? Landmass(seed) : Main(seed, 90f);
                var shape = new SkyIslandShape(island);
                var sites = Sites(island);
                for (int c = 0; c < sites.Caves.Count; c++)
                {
                    var cave = sites.Caves[c];
                    checkedCaves++;
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8f;
                        var edge = new Vector2(cave.Base.x, cave.Base.z) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cave.OuterRadius;
                        Assert.That(shape.Reach(edge.x, edge.y), Is.LessThan(0.9f), $"seed {seed}: {cave} reaches the rim");
                        float ground = shape.Surface(edge.x, edge.y);
                        Assert.That(cave.Base.y, Is.LessThanOrEqualTo(ground + 0.5f), $"seed {seed}: {cave} floats over the ground");
                        Assert.That(ground - cave.Base.y, Is.LessThan(_config.CaveMaxGroundRise + 1.5f), $"seed {seed}: {cave} on a slope");
                    }

                    foreach (var other in sites.Caves.Where((_, i) => i != c))
                    {
                        float apart = new Vector2(cave.Base.x - other.Base.x, cave.Base.z - other.Base.z).magnitude;
                        Assert.That(apart, Is.GreaterThan(cave.OuterRadius + other.OuterRadius), $"seed {seed}: caves overlap");
                    }

                    var middle = sites.Clearings[0].position;
                    float fromMiddle = new Vector2(cave.Base.x - middle.x, cave.Base.z - middle.z).magnitude;
                    Assert.That(fromMiddle, Is.GreaterThan(cave.OuterRadius + _config.ClearingRadius), $"seed {seed}: cave on the middle's camp");
                }
            }
            Assert.That(checkedCaves, Is.GreaterThan(30));
        }

        [Test]
        public void EveryCave_HasAClearingInItsChamber_FacingItsMouth_WhichOpensDownhill()
        {
            for (uint seed = 0; seed < 30; seed++)
            {
                var island = Landmass(seed);
                var shape = new SkyIslandShape(island);
                var sites = Sites(island);
                foreach (var cave in sites.Caves)
                {
                    var camp = sites.Clearings.Single(clearing => (clearing.position - cave.Chamber).sqrMagnitude < 1e-4f);
                    Assert.That(Vector3.Angle(camp.rotation * Vector3.forward, cave.MouthDirection), Is.LessThan(0.5f));

                    var mouth = cave.Base + cave.MouthDirection * cave.OuterRadius;
                    float atMouth = shape.Surface(mouth.x, mouth.z);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8f;
                        var edge = cave.Base + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * cave.OuterRadius;
                        Assert.That(atMouth, Is.LessThanOrEqualTo(shape.Surface(edge.x, edge.z) + 1e-3f), $"seed {seed}: mouth not downhill");
                    }
                    Assert.That(cave.Floor, Is.GreaterThanOrEqualTo(cave.Base.y), "the floor is on or over the foot");
                    Assert.That(cave.Floor - cave.Base.y, Is.LessThanOrEqualTo(SkyIslandSiteProcessor.MaxFloorRise + 0.31f));
                }
            }
        }

        [Test]
        public void TheSameIsland_GivesTheSameSites()
        {
            var first = Sites(Landmass(77));
            var second = Sites(Landmass(77));

            Assert.That(second.Caves.Select(cave => cave.ToString()), Is.EqualTo(first.Caves.Select(cave => cave.ToString())));
            Assert.That(second.Clearings, Is.EqualTo(first.Clearings));
        }

        private SkyIslandSites Sites(in SkyIslandSpec island)
        {
            var sites = new SkyIslandSites();
            _processor.Sites(island, _config.SiteRules, sites);
            return sites;
        }

        private static SkyIslandSpec Main(uint shapeSeed, float radius) =>
            new(new SkyIslandId(1, (int)shapeSeed, 0, 0), Vector3.zero, radius, radius * 1.2f, shapeSeed);

        private static SkyIslandSpec Landmass(uint shapeSeed) =>
            new(new SkyIslandId(1, (int)shapeSeed, 0, 0), Vector3.zero, 175f, 120f, shapeSeed, isLandmass: true);
    }
}
