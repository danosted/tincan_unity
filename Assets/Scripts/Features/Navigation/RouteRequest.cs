#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.Navigation
{
    /// <summary>Server: a player's request to replace their ship's route (from the chart table), not yet checked.</summary>
    public readonly struct RouteRequest
    {
        public readonly ulong ClientId;
        public readonly IReadOnlyList<RouteWaypoint> Waypoints;
        public readonly int ActiveIndex;

        public RouteRequest(ulong clientId, IReadOnlyList<RouteWaypoint> waypoints, int activeIndex)
        {
            ClientId = clientId;
            Waypoints = waypoints;
            ActiveIndex = activeIndex;
        }
    }
}
