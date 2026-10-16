#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Navigation;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Every peer: the sky islands on the navigator's chart, from the same seeded layout the islands are built from
    /// (<see cref="SkyIslandLayoutProcessor"/>), so nothing is replicated and islands far beyond the stream radius are
    /// charted too.
    /// <list type="bullet">
    /// <item>Every island is a rock obstacle, from the bottom of its rock to just above its top.</item>
    /// <item>Main islands at least <see cref="SkyIslandConfig.LandmarkMinRadius"/> wide along the voyage's route are
    /// landmarks: points of interest, hidden until the ship comes within <see cref="SkyIslandConfig.LandmarkDiscoverRadius"/>.</item>
    /// </list>
    /// Results are cached per layout and, for obstacles, per layout cell of the asking point. Plan:
    /// .docs/plans/navigation-compass.md (S3).
    /// </summary>
    public class SkyIslandChartSource : IChartSource
    {
        // Room above an island's top (grass, trees) that still counts as part of it.
        private const float AboveTop = 6f;

        private readonly IActorRegistry _actors;
        private readonly SkyIslandLayoutProcessor _layout;
        private readonly SkyIslandConfig _config;
        private readonly List<SkyIslandSpec> _islands = new();
        private readonly List<Vector3> _keepClear = new();
        private readonly List<PointOfInterest> _landmarks = new();
        private readonly List<ChartObstacle> _obstacles = new();

        private (int Seed, Vector3 Origin, Vector3 Destination)? _landmarkKey;
        private (int Seed, Vector3 Origin, Vector3 Destination, Vector2Int Cell, int Reach)? _obstacleKey;

        public SkyIslandChartSource(IActorRegistry actors, SkyIslandLayoutProcessor layout, SkyIslandConfig config)
        {
            _actors = actors;
            _layout = layout;
            _config = config;
        }

        public void CollectPointsOfInterest(ICollection<PointOfInterest> into)
        {
            var layout = CurrentLayout();
            if (_landmarkKey != layout) Landmarks(layout);
            foreach (var landmark in _landmarks) into.Add(landmark);
        }

        public void CollectObstacles(Vector3 center, float radius, ICollection<ChartObstacle> into)
        {
            var layout = CurrentLayout();
            var cell = _layout.CellOf(center, _config.CellSize);
            int reach = Mathf.CeilToInt(radius / _config.CellSize);
            var key = (layout.Seed, layout.Origin, layout.Destination, cell, reach);
            if (_obstacleKey != key)
            {
                _obstacleKey = key;
                Islands(layout, _layout.CellCentre(cell, _config.CellSize), (reach + 1) * _config.CellSize);
                _obstacles.Clear();
                foreach (var island in _islands)
                    _obstacles.Add(new ChartObstacle(island.Top, island.Radius, island.Top.y - island.Depth, island.Top.y + AboveTop, ChartObstacleKind.Rock));
            }

            foreach (var obstacle in _obstacles)
            {
                float dx = obstacle.Center.x - center.x, dz = obstacle.Center.z - center.z;
                float limit = radius + obstacle.Radius;
                if (dx * dx + dz * dz <= limit * limit) into.Add(obstacle);
            }
        }

        /// <summary>A landmark's id: stable for the layout and never 0 or 1 (no POI, the session's destination).</summary>
        public static int LandmarkId(in SkyIslandId island) =>
            (int)(SkyIslandRandom.Hash(island.Seed, island.CellX, island.CellZ, island.Index) & 0x3FFFFFFF) | 0x40000000;

        private void Landmarks((int Seed, Vector3 Origin, Vector3 Destination) layout)
        {
            _landmarkKey = layout;
            _landmarks.Clear();

            // Every cell along the route from the origin to the destination, the corridor wide on either side.
            Vector3 start = layout.Origin, end = layout.Destination;
            float length = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(end.x, 0f, end.z));
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / _config.LandmarkCorridor));
            var seen = new HashSet<SkyIslandId>();
            for (int i = 0; i <= steps; i++)
            {
                Islands(layout, Vector3.Lerp(start, end, i / (float)steps), _config.LandmarkCorridor);
                foreach (var island in _islands)
                {
                    if (island.IsSatellite || island.Radius < _config.LandmarkMinRadius || !seen.Add(island.Id)) continue;
                    _landmarks.Add(new PointOfInterest(LandmarkId(island.Id), "Isle", island.Top + Vector3.up * AboveTop,
                        startsKnown: false, _config.LandmarkDiscoverRadius));
                }
            }
        }

        private void Islands((int Seed, Vector3 Origin, Vector3 Destination) layout, Vector3 focus, float radius)
        {
            _keepClear.Clear();
            _keepClear.Add(layout.Origin);
            if (layout.Destination != layout.Origin) _keepClear.Add(layout.Destination);
            _islands.Clear();
            _layout.Around(layout.Seed, focus, radius, _config.LayoutRules, _keepClear, _islands);
        }

        // The same layout the streaming builds from: the session's, or the config's world seed before any voyage.
        private (int Seed, Vector3 Origin, Vector3 Destination) CurrentLayout()
        {
            foreach (var session in _actors.GetActors<ISessionLayout>())
            {
                if (session.LayoutSeed != 0) return (session.LayoutSeed, session.Origin, session.Destination);
            }
            return (_config.WorldSeed, Vector3.zero, Vector3.zero);
        }
    }
}
