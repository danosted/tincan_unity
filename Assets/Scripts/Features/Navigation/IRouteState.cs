#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// A ship's route as every peer sees it: replicated by the NavigationState fixture on the ship, so hosts, clients and
    /// late joiners agree. The waypoints in order, the one the compass points at, where the first leg starts, and which
    /// hidden points of interest the crew has discovered. The server writes it (the Server* calls do nothing elsewhere);
    /// any peer may ask for a new route, and the server checks the asker sits at the ship's chart table.
    /// </summary>
    public interface IRouteState : IActor
    {
        /// <summary>The ship this route belongs to (the one the fixture is fixed to); null before it is parented.</summary>
        IAirshipView? Ship { get; }

        int Count { get; }
        RouteWaypoint Waypoint(int index);

        /// <summary>The waypoint the compass points at; -1 while the route is empty.</summary>
        int ActiveIndex { get; }

        /// <summary>Where the first leg starts (the ship's position when the route was set).</summary>
        Vector3 Origin { get; }

        bool IsDiscovered(int poiId);
        int DiscoveredCount { get; }

        /// <summary>Any peer: ask the server for this route, keeping <paramref name="activeIndex"/> as the active waypoint.</summary>
        void RequestRoute(IReadOnlyList<RouteWaypoint> waypoints, int activeIndex);

        /// <summary>Server: the oldest route request since the last call, if any.</summary>
        bool TryConsumeRouteRequest(out RouteRequest request);

        void ServerSetRoute(IReadOnlyList<RouteWaypoint> waypoints, int activeIndex, Vector3 origin);
        void ServerSetWaypoint(int index, RouteWaypoint waypoint);
        void ServerSetActiveIndex(int index);
        void ServerDiscover(int poiId);
        void ServerClearDiscovered();
    }
}
