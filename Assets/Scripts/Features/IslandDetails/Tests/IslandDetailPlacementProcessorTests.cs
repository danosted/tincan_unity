#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Features.IslandDetails.Tests
{
    /// <summary><see cref="IslandDetailPlacementProcessor"/>: the same details every time, on the ground, out of the way.</summary>
    public class IslandDetailPlacementProcessorTests
    {
        private IslandDetailsConfig _config = null!;
        private SkyIslandConfig _islands = null!;
        private readonly IslandDetailPlacementProcessor _placement = new();
        private readonly SkyIslandSiteProcessor _siteProcessor = new();

        [SetUp]
        public void SetUp()
        {
            _config = IslandDetailsTestKit.Config();
            _islands = ScriptableObject.CreateInstance<SkyIslandConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_islands);
        }

        [Test]
        public void TheSameIsland_GetsTheSameDetails()
        {
            var first = Place(IslandDetailsTestKit.Main(12)).Select(detail => detail.ToString()).ToArray();
            var second = Place(IslandDetailsTestKit.Main(12)).Select(detail => detail.ToString()).ToArray();
            var other = Place(IslandDetailsTestKit.Main(13)).Select(detail => detail.ToString()).ToArray();

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        }

        [Test]
        public void Trees_GrowOnMainIslands_NotOnSatellites()
        {
            var main = Place(IslandDetailsTestKit.Main(3));
            Assert.That(main.Count(d => d.Kind == IslandDetailKind.Tree), Is.InRange(5, _config.MaxTrees));

            for (uint seed = 0; seed < 20; seed++)
                Assert.That(Place(IslandDetailsTestKit.Satellite(seed)).Any(d => d.Kind == IslandDetailKind.Tree), Is.False);
        }

        [Test]
        public void ALandmass_IsWooded_UpToItsOwnCap()
        {
            var details = Place(IslandDetailsTestKit.Landmass(5));
            int trees = details.Count(d => d.Kind == IslandDetailKind.Tree);

            Assert.That(trees, Is.GreaterThan(_config.MaxTrees).And.LessThanOrEqualTo(_config.MaxTreesOnALandmass));
            Assert.That(details.Count(d => d.Kind == IslandDetailKind.Boulder), Is.InRange(20, _config.MaxBoulders));
            Assert.That(details.Count(d => d.Kind == IslandDetailKind.Bush), Is.InRange(40, _config.MaxBushes));
        }

        [Test]
        public void EveryDetail_StandsOnTheGround_InsideTheRim_WithAVariantItHas()
        {
            foreach (var island in Islands())
            {
                var shape = new SkyIslandShape(island);
                foreach (var detail in Place(island))
                {
                    Assert.That(detail.Position.y, Is.EqualTo(shape.Surface(detail.Position.x, detail.Position.z)).Within(1e-4f), detail.ToString());
                    Assert.That(shape.Reach(detail.Position.x, detail.Position.z), Is.LessThanOrEqualTo(_config.RimReach + 1e-4f), detail.ToString());
                    int variants = detail.Kind switch
                    {
                        IslandDetailKind.Tree => _config.TreePrefabs.Count,
                        IslandDetailKind.Boulder => _config.BoulderPrefabs.Count,
                        _ => _config.BushPrefabs.Count
                    };
                    Assert.That(detail.Variant, Is.InRange(0, variants - 1), detail.ToString());
                }
            }
        }

        [Test]
        public void NothingStands_InAClearing_OnACave_OrInFrontOfItsMouth()
        {
            int caves = 0;
            foreach (var island in Islands())
            {
                var sites = new SkyIslandSites();
                _siteProcessor.Sites(island, _islands.SiteRules, sites);
                caves += sites.Caves.Count;
                foreach (var detail in Place(island))
                {
                    var at = new Vector2(detail.Position.x, detail.Position.z);
                    foreach (var clearing in sites.Clearings)
                        Assert.That((new Vector2(clearing.position.x, clearing.position.z) - at).magnitude,
                            Is.GreaterThanOrEqualTo(_islands.ClearingRadius), $"{detail} in a clearing");
                    foreach (var cave in sites.Caves)
                    {
                        var middle = new Vector2(cave.Base.x, cave.Base.z);
                        Assert.That((middle - at).magnitude, Is.GreaterThan(cave.OuterRadius + 1f), $"{detail} on {cave}");
                        var mouth = middle + new Vector2(cave.MouthDirection.x, cave.MouthDirection.z) * (cave.OuterRadius + 3f);
                        Assert.That((mouth - at).magnitude, Is.GreaterThan(5f), $"{detail} in front of {cave}'s mouth");
                    }
                }
            }
            Assert.That(caves, Is.GreaterThan(5), "the islands tested have caves");
        }

        [Test]
        public void Trees_KeepTheirRoom()
        {
            foreach (var island in Islands())
            {
                var trees = Place(island).Where(d => d.Kind == IslandDetailKind.Tree).ToArray();
                for (int a = 0; a < trees.Length; a++)
                    for (int b = a + 1; b < trees.Length; b++)
                    {
                        float apart = new Vector2(trees[a].Position.x - trees[b].Position.x, trees[a].Position.z - trees[b].Position.z).magnitude;
                        float room = 0.5f * _config.TreeSpacing * (trees[a].Scale + trees[b].Scale);
                        Assert.That(apart, Is.GreaterThanOrEqualTo(room - 1e-3f), $"{trees[a]} and {trees[b]}");
                    }
            }
        }

        [Test]
        public void AKindWithNoPrefabs_IsNotPlaced()
        {
            _config.TreePrefabs.Clear();
            _config.BushPrefabs.Clear();

            var details = Place(IslandDetailsTestKit.Main(8));

            Assert.That(details.All(d => d.Kind == IslandDetailKind.Boulder), Is.True);
            Assert.That(details, Is.Not.Empty);
        }

        [Test]
        public void Veins_ComeLast_AndLeaveEveryOtherDetailWhereItWas()
        {
            var island = IslandDetailsTestKit.Main(7);
            var without = Place(island).Select(detail => detail.ToString()).ToArray();

            _config.VeinPrefabs.Add(null!);
            var with = Place(island);

            Assert.That(with.Take(without.Length).Select(detail => detail.ToString()), Is.EqualTo(without));
            Assert.That(with.Skip(without.Length).All(detail => detail.Kind == IslandDetailKind.Vein), Is.True);
        }

        [Test]
        public void MainIslandsAndLandmasses_GetVeins_SatellitesNone()
        {
            _config.VeinPrefabs.Add(null!);

            Assert.That(Place(IslandDetailsTestKit.Main(3)).Count(d => d.Kind == IslandDetailKind.Vein), Is.GreaterThan(0));
            Assert.That(Place(IslandDetailsTestKit.Landmass(2)).Count(d => d.Kind == IslandDetailKind.Vein),
                Is.GreaterThan(_config.VeinsPerIsland));
            for (uint seed = 0; seed < 5; seed++)
                Assert.That(Place(IslandDetailsTestKit.Satellite(seed)).Any(d => d.Kind == IslandDetailKind.Vein), Is.False);
        }

        [Test]
        public void EachCave_HasAVeinInside_OnItsFloor()
        {
            _config.VeinPrefabs.Add(null!);
            int checkedCaves = 0;
            for (uint seed = 0; seed < 4; seed++)
            {
                var island = IslandDetailsTestKit.Landmass(seed);
                var sites = new SkyIslandSites();
                _siteProcessor.Sites(island, _islands.SiteRules, sites);
                var details = Place(island);
                foreach (var cave in sites.Caves)
                {
                    checkedCaves++;
                    Assert.That(details.Any(d => d.Kind == IslandDetailKind.Vein &&
                                                 new Vector2(d.Position.x - cave.Base.x, d.Position.z - cave.Base.z).magnitude < cave.Radius &&
                                                 Mathf.Abs(d.Position.y - cave.Floor) < 1e-3f), Is.True, cave.ToString());
                }
            }
            Assume.That(checkedCaves, Is.GreaterThan(0), "no landmass had a cave");
        }

        private List<IslandDetail> Place(in SkyIslandSpec island)
        {
            var sites = new SkyIslandSites();
            _siteProcessor.Sites(island, _islands.SiteRules, sites);
            var details = new List<IslandDetail>();
            _placement.Place(island, sites, _config.Rules(_islands.ClearingRadius), details);
            return details;
        }

        private static IEnumerable<SkyIslandSpec> Islands()
        {
            for (uint seed = 0; seed < 12; seed++)
            {
                yield return IslandDetailsTestKit.Main(seed, 60f + seed * 3f);
                yield return IslandDetailsTestKit.Satellite(seed);
            }
            for (uint seed = 0; seed < 4; seed++) yield return IslandDetailsTestKit.Landmass(seed);
        }
    }
}
