#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="SkyIslandLayoutProcessor"/>: the same islands on every peer, apart, and clear of the voyage.</summary>
    public class SkyIslandLayoutProcessorTests
    {
        private static readonly Vector3[] NoKeepOuts = System.Array.Empty<Vector3>();

        private SkyIslandConfig _config = null!;
        private SkyIslandLayoutProcessor _layout = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SkyIslandConfig>();
            _layout = new SkyIslandLayoutProcessor();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void SameSeedAndCell_GiveTheSameIslands()
        {
            for (int x = -4; x <= 4; x++)
            {
                for (int z = -4; z <= 4; z++)
                {
                    var first = Cell(17, x, z);
                    var second = Cell(17, x, z);
                    Assert.That(second.Select(Describe), Is.EqualTo(first.Select(Describe)), $"cell {x},{z}");
                }
            }
        }

        [Test]
        public void AnotherSeed_GivesAnotherLayout()
        {
            var one = Area(17).Select(Describe).ToArray();
            var other = Area(18).Select(Describe).ToArray();

            Assert.That(one, Is.Not.Empty);
            Assert.That(other, Is.Not.EqualTo(one));
        }

        [Test]
        public void TheDefaults_MakeAFewMainIslands_WithSatellites()
        {
            var islands = Area(3);

            int mains = islands.Count(island => !island.IsSatellite);
            Assert.That(mains, Is.InRange(81 * 0.3f, 81 * 0.8f), "about MainChance of 81 cells");
            Assert.That(islands.Count(island => island.IsSatellite), Is.GreaterThan(mains), "rocks around the islands");
        }

        [Test]
        public void Islands_StayWithinTheirSizesAndAltitudes()
        {
            foreach (var island in Area(5))
            {
                var radius = island.IsSatellite ? _config.SatelliteRadius : _config.MainRadius;
                Assert.That(island.Radius, Is.InRange(radius.x, radius.y), island.ToString());
                Assert.That(island.Depth, Is.GreaterThan(island.Radius * 0.5f), island.ToString());
                if (!island.IsSatellite) Assert.That(island.Top.y, Is.InRange(_config.TopAltitude.x, _config.TopAltitude.y));
            }
        }

        [Test]
        public void EveryIsland_StaysInsideItsCell_SoNeighbouringCellsNeverTouch()
        {
            float size = _config.CellSize;
            foreach (var island in Area(9))
            {
                float minX = island.Id.CellX * size, minZ = island.Id.CellZ * size;
                Assert.That(island.Top.x - island.Radius, Is.GreaterThanOrEqualTo(minX - 1e-3f), island.ToString());
                Assert.That(island.Top.x + island.Radius, Is.LessThanOrEqualTo(minX + size + 1e-3f), island.ToString());
                Assert.That(island.Top.z - island.Radius, Is.GreaterThanOrEqualTo(minZ - 1e-3f), island.ToString());
                Assert.That(island.Top.z + island.Radius, Is.LessThanOrEqualTo(minZ + size + 1e-3f), island.ToString());
            }
        }

        [Test]
        public void IslandsInOneCell_DoNotOverlap()
        {
            foreach (var cell in Area(11).GroupBy(island => (island.Id.CellX, island.Id.CellZ)))
            {
                var islands = cell.ToArray();
                for (int a = 0; a < islands.Length; a++)
                {
                    for (int b = a + 1; b < islands.Length; b++)
                    {
                        float apart = Level(islands[a].Top - islands[b].Top).magnitude;
                        Assert.That(apart, Is.GreaterThan(islands[a].Radius + islands[b].Radius), $"{islands[a]} and {islands[b]}");
                    }
                }
            }
        }

        [Test]
        public void KeepClearPoints_LeaveOutTheIslandsNearThem()
        {
            var all = new List<SkyIslandSpec>();
            _layout.Around(21, Vector3.zero, 2000f, _config.LayoutRules, NoKeepOuts, all);
            var near = all.First(island => !island.IsSatellite);
            var keepClear = new[] { near.Top };

            var kept = new List<SkyIslandSpec>();
            _layout.Around(21, Vector3.zero, 2000f, _config.LayoutRules, keepClear, kept);

            Assert.That(kept.Select(island => island.Id), Has.No.Member(near.Id));
            Assert.That(kept.Count, Is.LessThan(all.Count));
            foreach (var island in kept)
                Assert.That(Level(island.Top - near.Top).magnitude, Is.GreaterThanOrEqualTo(_config.KeepOutRadius + island.Radius));
            Assert.That(all.Count(island => !kept.Any(other => other.Id.Equals(island.Id))),
                Is.LessThan(8), "only the islands around the point go");
        }

        [Test]
        public void Around_TakesTheCellsWithinTheRadius()
        {
            var focus = new Vector3(100f, 40f, 100f);
            var own = new List<SkyIslandSpec>();
            _layout.Around(4, focus, 1f, _config.LayoutRules, NoKeepOuts, own);
            Assert.That(own.All(island => island.Id.CellX == 0 && island.Id.CellZ == 0), Is.True, "a tiny radius: the focus's own cell");

            var wide = new List<SkyIslandSpec>();
            _layout.Around(4, focus, 1000f, _config.LayoutRules, NoKeepOuts, wide);
            Assert.That(wide.Select(island => island.Id.CellX).Distinct().Count(), Is.GreaterThan(3));
            Assert.That(wide.All(island => Mathf.Abs(island.Id.CellX) <= 3 && Mathf.Abs(island.Id.CellZ) <= 3), Is.True);
        }

        [Test]
        public void CellOf_FloorsNegativeCoordinates()
        {
            Assert.That(_layout.CellOf(new Vector3(-1f, 0f, 449f), 450f), Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(_layout.CellOf(new Vector3(450f, 0f, -450f), 450f), Is.EqualTo(new Vector2Int(1, -1)));
        }

        [Test]
        public void Random_IsTheSameEveryRun()
        {
            var random = new SkyIslandRandom(SkyIslandRandom.Hash(1, 2, 3));
            Assert.That(random.NextUInt(), Is.EqualTo(new SkyIslandRandom(SkyIslandRandom.Hash(1, 2, 3)).NextUInt()));
            for (int i = 0; i < 1000; i++) Assert.That(random.Value(), Is.InRange(0f, 0.99999995f));
        }

        private List<SkyIslandSpec> Cell(int seed, int x, int z)
        {
            var islands = new List<SkyIslandSpec>();
            _layout.Cell(seed, x, z, _config.LayoutRules, NoKeepOuts, islands);
            return islands;
        }

        /// <summary>The islands of 9 x 9 cells around the origin.</summary>
        private List<SkyIslandSpec> Area(int seed)
        {
            var islands = new List<SkyIslandSpec>();
            for (int x = -4; x <= 4; x++)
                for (int z = -4; z <= 4; z++)
                    _layout.Cell(seed, x, z, _config.LayoutRules, NoKeepOuts, islands);
            return islands;
        }

        private static string Describe(SkyIslandSpec island) =>
            $"{island.Id} {island.Top.x:R} {island.Top.y:R} {island.Top.z:R} {island.Radius:R} {island.Depth:R} {island.ShapeSeed}";

        private static Vector3 Level(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
