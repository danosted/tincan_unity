#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Domain, pure: where the islands are. The world is a grid of square cells; what a cell holds depends only on the
    /// seed and the cell, so every peer computes the same islands without asking anyone. A cell holds at most one
    /// cluster (a main island and its satellites, or a few lone rocks), kept far enough inside the cell that clusters in
    /// neighbouring cells never touch. Islands within the keep-out radius (level distance) of a keep-clear point are
    /// left out.
    /// </summary>
    public class SkyIslandLayoutProcessor
    {
        private const float LoneClusterSpread = 60f;

        public Vector2Int CellOf(Vector3 position, float cellSize) =>
            new(Mathf.FloorToInt(position.x / cellSize), Mathf.FloorToInt(position.z / cellSize));

        /// <summary>
        /// The middle of a cell, at altitude 0. Streaming measures its radius from here, so the islands wanted stay the
        /// same while the ship crosses the cell.
        /// </summary>
        public Vector3 CellCentre(Vector2Int cell, float cellSize) => new((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);

        /// <summary>Every island in the cells whose square comes within <paramref name="radius"/> of the focus (level).</summary>
        public void Around(int seed, Vector3 focus, float radius, in SkyIslandLayoutRules rules,
            IReadOnlyList<Vector3> keepClear, List<SkyIslandSpec> into)
        {
            float size = rules.CellSize;
            int reach = Mathf.CeilToInt(radius / size);
            var centre = CellOf(focus, size);
            for (int x = centre.x - reach; x <= centre.x + reach; x++)
            {
                for (int z = centre.y - reach; z <= centre.y + reach; z++)
                {
                    float nearestX = Mathf.Clamp(focus.x, x * size, (x + 1) * size);
                    float nearestZ = Mathf.Clamp(focus.z, z * size, (z + 1) * size);
                    float dx = focus.x - nearestX, dz = focus.z - nearestZ;
                    if (dx * dx + dz * dz > radius * radius) continue;
                    Cell(seed, x, z, rules, keepClear, into);
                }
            }
        }

        /// <summary>The islands of one cell, main island first.</summary>
        public void Cell(int seed, int cellX, int cellZ, in SkyIslandLayoutRules rules, IReadOnlyList<Vector3> keepClear,
            List<SkyIslandSpec> into)
        {
            var random = new SkyIslandRandom(SkyIslandRandom.Hash(seed, cellX, cellZ));
            var corner = new Vector2(cellX * rules.CellSize, cellZ * rules.CellSize);

            if (random.Value() < rules.MainChance) MainCluster(seed, cellX, cellZ, rules, keepClear, ref random, corner, into);
            else if (random.Value() < rules.LoneSatelliteChance) LoneCluster(seed, cellX, cellZ, rules, keepClear, ref random, corner, into);
        }

        /// <summary>Is this island clear of every keep-clear point (level distance, counting its radius)?</summary>
        public bool IsClear(in SkyIslandSpec island, float keepOutRadius, IReadOnlyList<Vector3> keepClear)
        {
            float limit = keepOutRadius + island.Radius;
            for (int i = 0; i < keepClear.Count; i++)
            {
                float dx = island.Top.x - keepClear[i].x, dz = island.Top.z - keepClear[i].z;
                if (dx * dx + dz * dz < limit * limit) return false;
            }
            return true;
        }

        private void MainCluster(int seed, int cellX, int cellZ, in SkyIslandLayoutRules rules,
            IReadOnlyList<Vector3> keepClear, ref SkyIslandRandom random, Vector2 corner, List<SkyIslandSpec> into)
        {
            float radius = random.Range(rules.MainRadius.x, rules.MainRadius.y);
            float spread = random.Range(rules.SatelliteSpread.x, rules.SatelliteSpread.y);
            // This cluster's own reach: its satellites sit at most spread * radius out, plus their own size.
            float margin = Mathf.Min(radius * spread + 2f * rules.SatelliteRadius.y, rules.CellSize * 0.5f);
            var centre = corner + new Vector2(
                random.Range(margin, rules.CellSize - margin),
                random.Range(margin, rules.CellSize - margin));
            float altitude = random.Range(rules.TopAltitude.x, rules.TopAltitude.y);
            float depth = radius * random.Range(rules.DepthPerRadius.x, rules.DepthPerRadius.y);

            Add(new SkyIslandSpec(new SkyIslandId(seed, cellX, cellZ, 0), new Vector3(centre.x, altitude, centre.y),
                radius, depth, random.NextUInt()), rules, keepClear, into);

            int count = random.Range(rules.SatellitesPerMain.x, rules.SatellitesPerMain.y);
            Satellites(seed, cellX, cellZ, rules, keepClear, ref random, centre, altitude, radius * spread, count, into);
        }

        private void LoneCluster(int seed, int cellX, int cellZ, in SkyIslandLayoutRules rules,
            IReadOnlyList<Vector3> keepClear, ref SkyIslandRandom random, Vector2 corner, List<SkyIslandSpec> into)
        {
            float margin = Mathf.Min(LoneClusterSpread + 2f * rules.SatelliteRadius.y, rules.CellSize * 0.5f);
            var centre = corner + new Vector2(
                random.Range(margin, rules.CellSize - margin),
                random.Range(margin, rules.CellSize - margin));
            float altitude = random.Range(rules.TopAltitude.x, rules.TopAltitude.y);
            int count = random.Range(rules.LoneSatellites.x, rules.LoneSatellites.y);
            Satellites(seed, cellX, cellZ, rules, keepClear, ref random, centre, altitude, LoneClusterSpread, count, into);
        }

        /// <summary>
        /// Rocks evenly round a centre, each turned a little off its slot (never into the next), so they never touch
        /// each other or the island in the middle.
        /// </summary>
        private void Satellites(int seed, int cellX, int cellZ, in SkyIslandLayoutRules rules,
            IReadOnlyList<Vector3> keepClear, ref SkyIslandRandom random, Vector2 centre, float altitude, float ring,
            int count, List<SkyIslandSpec> into)
        {
            if (count <= 0) return;
            float slot = 2f * Mathf.PI / count;
            float start = random.Range(0f, 2f * Mathf.PI);
            for (int i = 0; i < count; i++)
            {
                float radius = random.Range(rules.SatelliteRadius.x, rules.SatelliteRadius.y);
                float angle = start + i * slot + random.Range(-0.25f, 0.25f) * slot;
                float distance = ring + radius;
                var top = new Vector3(
                    centre.x + Mathf.Cos(angle) * distance,
                    altitude + random.Range(-rules.SatelliteAltitudeJitter, rules.SatelliteAltitudeJitter),
                    centre.y + Mathf.Sin(angle) * distance);
                float depth = radius * random.Range(rules.DepthPerRadius.x, rules.DepthPerRadius.y) * 1.3f;
                Add(new SkyIslandSpec(new SkyIslandId(seed, cellX, cellZ, i + 1), top, radius, depth, random.NextUInt()),
                    rules, keepClear, into);
            }
        }

        private void Add(in SkyIslandSpec island, in SkyIslandLayoutRules rules, IReadOnlyList<Vector3> keepClear,
            List<SkyIslandSpec> into)
        {
            if (IsClear(island, rules.KeepOutRadius, keepClear)) into.Add(island);
        }
    }
}
