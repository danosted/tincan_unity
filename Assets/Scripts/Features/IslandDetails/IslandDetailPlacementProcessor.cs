#nullable enable
using System.Collections.Generic;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// Domain, pure: what stands on an island's grass, from its shape seed alone, so every peer places the same. Trees
    /// grow in groves on main islands and landmasses; boulders lie anywhere; bushes half under the trees, half in the
    /// open. Nothing stands in a clearing (open ground to camp on), on or against a cave rock, in front of a cave's mouth, or
    /// out by the rim, and nothing crowds anything else. Each stands on the drawn ground (<see cref="SkyIslandShape.Surface"/>).
    /// A detail that finds no room after a few tries is left out. Trees come first in the list, in a fixed order.
    /// </summary>
    public class IslandDetailPlacementProcessor
    {
        private const int Attempts = 10;
        private const float SatelliteReach = 0.75f;
        private const float BushNearTree = 5f;
        private const float MouthClearance = 7f;

        private readonly List<IslandDetail> _placed = new();
        private readonly List<Vector2> _groves = new();

        public void Place(in SkyIslandSpec island, SkyIslandSites sites, in IslandDetailRules rules, List<IslandDetail> into)
        {
            into.Clear();
            _placed.Clear();
            var shape = new SkyIslandShape(island);
            var random = new SkyIslandRandom(SkyIslandRandom.Hash((int)island.ShapeSeed, 9157));
            float hectares = Mathf.PI * island.Radius * island.Radius / 10000f;
            float reach = island.IsSatellite ? Mathf.Min(SatelliteReach, rules.RimReach) : rules.RimReach;

            if (!island.IsSatellite && rules.TreeVariants > 0)
            {
                int trees = Mathf.Min(Count(hectares * rules.TreesPerHectare, ref random),
                    island.IsLandmass ? rules.MaxTreesOnALandmass : rules.MaxTrees);
                Groves(island, shape, sites, rules, hectares, reach, ref random);
                for (int i = 0; i < trees && _groves.Count > 0; i++)
                {
                    var grove = _groves[i % _groves.Count];
                    TryPlace(IslandDetailKind.Tree, rules.TreeVariants, rules.TreeScale, shape, sites, rules, reach, ref random,
                        grove, rules.GroveSpread, island.Radius);
                }
            }

            if (rules.BoulderVariants > 0)
            {
                int boulders = Mathf.Min(Count(hectares * rules.BouldersPerHectare, ref random), rules.MaxBoulders);
                for (int i = 0; i < boulders; i++)
                    TryPlace(IslandDetailKind.Boulder, rules.BoulderVariants, rules.BoulderScale, shape, sites, rules, reach,
                        ref random, null, 0f, island.Radius);
            }

            if (rules.BushVariants > 0)
            {
                int bushes = Mathf.Min(Count(hectares * rules.BushesPerHectare, ref random), rules.MaxBushes);
                int trees = CountTrees();
                for (int i = 0; i < bushes; i++)
                {
                    if (trees > 0 && i % 2 == 0)
                    {
                        var tree = _placed[(int)(random.NextUInt() % (uint)trees)].Position;
                        TryPlace(IslandDetailKind.Bush, rules.BushVariants, rules.BushScale, shape, sites, rules, reach, ref random,
                            new Vector2(tree.x, tree.z), BushNearTree, island.Radius);
                    }
                    else
                    {
                        TryPlace(IslandDetailKind.Bush, rules.BushVariants, rules.BushScale, shape, sites, rules, reach, ref random,
                            null, 0f, island.Radius);
                    }
                }
            }

            into.AddRange(_placed);
        }

        /// <summary>A whole number near <paramref name="expected"/>: its fraction is a chance of one more.</summary>
        private static int Count(float expected, ref SkyIslandRandom random)
        {
            int whole = Mathf.FloorToInt(expected);
            return whole + (random.Value() < expected - whole ? 1 : 0);
        }

        /// <summary>The groves' middles: about one per <see cref="IslandDetailRules.GroveHectares"/>, each in open ground.</summary>
        private void Groves(in SkyIslandSpec island, in SkyIslandShape shape, SkyIslandSites sites, in IslandDetailRules rules,
            float hectares, float reach, ref SkyIslandRandom random)
        {
            _groves.Clear();
            int groves = Mathf.Max(1, Mathf.RoundToInt(hectares / Mathf.Max(0.01f, rules.GroveHectares)));
            for (int g = 0; g < groves; g++)
            {
                for (int attempt = 0; attempt < Attempts; attempt++)
                {
                    var at = RandomPoint(island.Radius * reach, ref random);
                    if (!Open(at, shape, sites, rules, reach, 0f)) continue;
                    _groves.Add(at);
                    break;
                }
            }
        }

        private void TryPlace(IslandDetailKind kind, int variants, Vector2 scaleRange, in SkyIslandShape shape,
            SkyIslandSites sites, in IslandDetailRules rules, float reach, ref SkyIslandRandom random,
            Vector2? around, float spread, float islandRadius)
        {
            float spacing = rules.Spacing(kind);
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                // Round a point (a grove, a tree), or anywhere on the top (the edge never reaches past the radius).
                var at = around is { } middle
                    ? middle + RandomPoint(spread, ref random)
                    : RandomPoint(islandRadius * reach, ref random);

                int variant = (int)(random.NextUInt() % (uint)variants);
                float yaw = random.Range(0f, 360f);
                float scale = random.Range(scaleRange.x, scaleRange.y);
                if (!Open(at, shape, sites, rules, reach, spacing * scale) || Crowded(at, kind, spacing * scale, rules)) continue;

                _placed.Add(new IslandDetail(kind, variant, new Vector3(at.x, shape.Surface(at.x, at.y), at.y), yaw, scale));
                return;
            }
        }

        /// <summary>Inside the rim, out of every clearing, off every cave rock and out of the way into its mouth.</summary>
        private static bool Open(Vector2 at, in SkyIslandShape shape, SkyIslandSites sites, in IslandDetailRules rules,
            float reach, float room)
        {
            if (shape.Reach(at.x, at.y) > reach) return false;
            foreach (var clearing in sites.Clearings)
            {
                float apart = rules.ClearingRadius + room;
                var p = clearing.position;
                if ((new Vector2(p.x, p.z) - at).sqrMagnitude < apart * apart) return false;
            }
            foreach (var cave in sites.Caves)
            {
                var middle = new Vector2(cave.Base.x, cave.Base.z);
                float apart = cave.OuterRadius + 1.5f + room;
                if ((middle - at).sqrMagnitude < apart * apart) return false;

                var mouth = middle + new Vector2(cave.MouthDirection.x, cave.MouthDirection.z) * (cave.OuterRadius + MouthClearance * 0.6f);
                float clear = MouthClearance + room;
                if ((mouth - at).sqrMagnitude < clear * clear) return false;
            }
            return true;
        }

        /// <summary>Too close to something already placed (half of each one's spacing)?</summary>
        private bool Crowded(Vector2 at, IslandDetailKind kind, float room, in IslandDetailRules rules)
        {
            foreach (var other in _placed)
            {
                float apart = 0.5f * (room + rules.Spacing(other.Kind) * other.Scale);
                if (kind == IslandDetailKind.Bush && other.Kind == IslandDetailKind.Tree) apart *= 0.5f; // under the canopy
                float dx = other.Position.x - at.x, dz = other.Position.z - at.y;
                if (dx * dx + dz * dz < apart * apart) return true;
            }
            return false;
        }

        private int CountTrees()
        {
            int trees = 0;
            while (trees < _placed.Count && _placed[trees].Kind == IslandDetailKind.Tree) trees++;
            return trees;
        }

        /// <summary>A point spread evenly over a disc of this radius round the origin.</summary>
        private static Vector2 RandomPoint(float radius, ref SkyIslandRandom random)
        {
            float angle = random.Range(0f, 2f * Mathf.PI);
            float distance = radius * Mathf.Sqrt(random.Value());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }
}
