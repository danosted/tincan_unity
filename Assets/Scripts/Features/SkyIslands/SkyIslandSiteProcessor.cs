#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Domain, pure: where the caves and clearings are on one main island (satellites have none), from its shape seed
    /// alone, so every peer finds the same. The middle is always a clearing. A big enough main island may get one cave
    /// (<see cref="SkyIslandSiteRules.CaveChance"/>), a landmass two or three and a few more clearings. A cave stands
    /// inside the rim, clear of the clearings and other caves, on ground that rises no more than a few metres under its
    /// wall (set into a hillside, its foot at the lowest ground). Its mouth opens downhill, where the wall's foot meets
    /// the ground, and its chamber holds a clearing that faces the mouth. A site that finds no room after a few tries is
    /// left out.
    /// </summary>
    public class SkyIslandSiteProcessor
    {
        private const int Attempts = 24;
        private const float CaveReach = 0.82f;
        private const float ClearingReach = 0.78f;
        private const int GroundSamples = 10;
        private const int EdgeSamples = 16;

        /// <summary>
        /// How far the chamber's floor may sit over the dome's foot, metres: the chamber keeps its height in the middle,
        /// and anything set down a few metres over the floor is still under the roof.
        /// </summary>
        public const float MaxFloorRise = 2.5f;

        public void Sites(in SkyIslandSpec island, in SkyIslandSiteRules rules, SkyIslandSites into)
        {
            into.Clear();
            if (island.IsSatellite) return;

            var shape = new SkyIslandShape(island);
            var random = new SkyIslandRandom(SkyIslandRandom.Hash((int)island.ShapeSeed, 4211));

            into.Clearings.Add(new Pose(new Vector3(0f, shape.Surface(0f, 0f), 0f), Quaternion.Euler(0f, island.ShapeSeed % 360u, 0f)));

            int caves = island.IsLandmass
                ? random.Range(rules.LandmassCaves.x, rules.LandmassCaves.y)
                : island.Radius >= rules.CaveMinIslandRadius && random.Value() < rules.CaveChance ? 1 : 0;
            for (int i = 0; i < caves; i++) TryCave(island, shape, rules, ref random, into);

            if (!island.IsLandmass) return;
            int clearings = random.Range(rules.LandmassClearings.x, rules.LandmassClearings.y);
            for (int i = 0; i < clearings; i++) TryClearing(island, shape, rules, ref random, into);
        }

        private static void TryCave(in SkyIslandSpec island, in SkyIslandShape shape, in SkyIslandSiteRules rules,
            ref SkyIslandRandom random, SkyIslandSites into)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float radius = random.Range(rules.CaveRadius.x, rules.CaveRadius.y);
                float height = random.Range(rules.CaveHeight.x, rules.CaveHeight.y);
                float outer = radius + rules.CaveWall;
                float angle = random.Range(0f, 2f * Mathf.PI);
                float distance = random.Range(0.3f, 0.75f) * island.Radius;
                var middle = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                uint seed = random.NextUInt();

                if (!Inside(shape, middle, outer, CaveReach)) continue;
                if (!ClearOfSites(into, middle, outer + rules.ClearingRadius + 2f, outer + 6f)) continue;
                if (!Ground(shape, middle, outer, out float lowest, out float highest, out float downhill)) continue;
                if (highest - lowest > rules.CaveMaxGroundRise) continue;

                float floor = shape.Surface(middle.x, middle.y);
                if (floor - lowest > MaxFloorRise) continue;

                var cave = new SkyIslandCave(new Vector3(middle.x, lowest - 0.3f, middle.y), floor, radius, height,
                    rules.CaveWall, downhill, seed);
                into.Caves.Add(cave);

                // Facing the mouth, as a camp in it would.
                var mouth = cave.MouthDirection;
                into.Clearings.Add(new Pose(cave.Chamber, Quaternion.LookRotation(mouth, Vector3.up)));
                return;
            }
        }

        private static void TryClearing(in SkyIslandSpec island, in SkyIslandShape shape, in SkyIslandSiteRules rules,
            ref SkyIslandRandom random, SkyIslandSites into)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float angle = random.Range(0f, 2f * Mathf.PI);
                float distance = random.Range(0.3f, 0.75f) * island.Radius;
                var at = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                float yaw = random.Range(0f, 360f);

                if (!Inside(shape, at, rules.ClearingRadius, ClearingReach)) continue;
                if (!ClearOfSites(into, at, 2f * rules.ClearingRadius + 20f, rules.ClearingRadius + 4f)) continue;

                into.Clearings.Add(new Pose(new Vector3(at.x, shape.Surface(at.x, at.y), at.y), Quaternion.Euler(0f, yaw, 0f)));
                return;
            }
        }

        /// <summary>Does a disc of this radius round the point stay within this share of the edge everywhere?</summary>
        private static bool Inside(in SkyIslandShape shape, Vector2 middle, float radius, float reach)
        {
            for (int i = 0; i < GroundSamples; i++)
            {
                float a = i * 2f * Mathf.PI / GroundSamples;
                var point = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                if (shape.Reach(point.x, point.y) > reach) return false;
            }
            return true;
        }

        /// <summary>
        /// Is the point this far from every clearing (<paramref name="fromClearings"/>), and from every cave's rock
        /// (<paramref name="fromCaves"/> plus that cave's own reach)?
        /// </summary>
        private static bool ClearOfSites(SkyIslandSites sites, Vector2 at, float fromClearings, float fromCaves)
        {
            foreach (var clearing in sites.Clearings)
            {
                var p = clearing.position;
                if ((new Vector2(p.x, p.z) - at).sqrMagnitude < fromClearings * fromClearings) return false;
            }
            foreach (var cave in sites.Caves)
            {
                float apart = fromCaves + cave.OuterRadius;
                if ((new Vector2(cave.Base.x, cave.Base.z) - at).sqrMagnitude < apart * apart) return false;
            }
            return true;
        }

        /// <summary>
        /// The lowest and highest ground under a disc's middle and round its edge, and the direction (radians) of the
        /// lowest point of the edge: downhill, where a cave's mouth opens at the foot of its wall.
        /// </summary>
        private static bool Ground(in SkyIslandShape shape, Vector2 middle, float radius, out float lowest, out float highest,
            out float downhill)
        {
            lowest = highest = shape.Surface(middle.x, middle.y);
            float lowestEdge = float.PositiveInfinity;
            downhill = 0f;
            for (int i = 0; i < EdgeSamples; i++)
            {
                float a = i * 2f * Mathf.PI / EdgeSamples;
                var point = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                float y = shape.Surface(point.x, point.y);
                if (float.IsNaN(y)) return false;
                lowest = Mathf.Min(lowest, y);
                highest = Mathf.Max(highest, y);
                if (y >= lowestEdge) continue;
                lowestEdge = y;
                downhill = a;
            }
            return true;
        }
    }
}
