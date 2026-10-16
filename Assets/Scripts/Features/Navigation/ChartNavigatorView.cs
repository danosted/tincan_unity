#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>The local navigator's chart this frame: what <see cref="ChartPresenter"/> draws at their table.</summary>
    public sealed class ChartNavigatorView
    {
        public ChartNavigatorView(IChartTable table, Vector2 cursor, float range, int selected, IReadOnlyList<RouteWaypoint> waypoints, int active)
        {
            Table = table;
            Cursor = cursor;
            Range = range;
            Selected = selected;
            Waypoints = waypoints;
            Active = active;
        }

        public IChartTable Table { get; }

        /// <summary>The cursor in chart points (radius 1 is the edge).</summary>
        public Vector2 Cursor { get; }

        /// <summary>Metres from the ship to the chart's edge.</summary>
        public float Range { get; }

        /// <summary>The picked waypoint, -1 for none.</summary>
        public int Selected { get; }

        public IReadOnlyList<RouteWaypoint> Waypoints { get; }
        public int Active { get; }
    }
}
