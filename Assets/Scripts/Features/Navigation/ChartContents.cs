#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Navigation;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Every peer: what is on the chart, gathered from every registered <see cref="IChartSource"/> (the session's
    /// destination, sky islands, hazard zones). The lists are reused between calls: read them before asking again.
    /// </summary>
    public class ChartContents
    {
        private readonly IReadOnlyList<IChartSource> _sources;
        private readonly List<PointOfInterest> _points = new();
        private readonly List<ChartObstacle> _obstacles = new();

        public ChartContents(IEnumerable<IChartSource> sources) => _sources = new List<IChartSource>(sources);

        public IReadOnlyList<PointOfInterest> PointsOfInterest()
        {
            _points.Clear();
            foreach (var source in _sources) source.CollectPointsOfInterest(_points);
            return _points;
        }

        public IReadOnlyList<ChartObstacle> Obstacles(Vector3 center, float radius)
        {
            _obstacles.Clear();
            foreach (var source in _sources) source.CollectObstacles(center, radius, _obstacles);
            return _obstacles;
        }

        public bool TryGetPointOfInterest(int id, out PointOfInterest point)
        {
            foreach (var candidate in PointsOfInterest())
            {
                if (candidate.Id != id) continue;
                point = candidate;
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>On the chart for this route's crew: known from the start, or discovered since.</summary>
        public static bool IsVisible(in PointOfInterest point, IRouteState route) => point.StartsKnown || route.IsDiscovered(point.Id);
    }
}
