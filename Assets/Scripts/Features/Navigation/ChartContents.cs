#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Navigation;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Every peer: what is on the chart, gathered from every registered <see cref="IChartSource"/> (the session's
    /// destination, sky islands, hazard zones, gas pockets), less what any <see cref="IChartFilter"/> hides. The lists are
    /// reused between calls: read them before asking again.
    /// </summary>
    public class ChartContents
    {
        private readonly IReadOnlyList<IChartSource> _sources;
        private readonly IReadOnlyList<IChartFilter> _filters;
        private readonly List<PointOfInterest> _points = new();
        private readonly List<ChartObstacle> _obstacles = new();

        public ChartContents(IEnumerable<IChartSource> sources) : this(sources, Array.Empty<IChartFilter>())
        {
        }

        [Inject]
        public ChartContents(IEnumerable<IChartSource> sources, IEnumerable<IChartFilter> filters)
        {
            _sources = new List<IChartSource>(sources);
            _filters = new List<IChartFilter>(filters);
        }

        public IReadOnlyList<PointOfInterest> PointsOfInterest()
        {
            _points.Clear();
            foreach (var source in _sources) source.CollectPointsOfInterest(_points);
            if (_filters.Count > 0) _points.RemoveAll(point => !Shows(point));
            return _points;
        }

        public IReadOnlyList<ChartObstacle> Obstacles(Vector3 center, float radius)
        {
            _obstacles.Clear();
            foreach (var source in _sources) source.CollectObstacles(center, radius, _obstacles);
            if (_filters.Count > 0) _obstacles.RemoveAll(obstacle => !Shows(obstacle));
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

        private bool Shows(PointOfInterest point)
        {
            foreach (var filter in _filters)
            {
                if (!filter.ShowsPoint(point)) return false;
            }

            return true;
        }

        private bool Shows(ChartObstacle obstacle)
        {
            foreach (var filter in _filters)
            {
                if (!filter.ShowsObstacle(obstacle)) return false;
            }

            return true;
        }
    }
}
