#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.Navigation
{
    /// <summary>Server-side control over the first ship's route, for dev tooling (scenarios).</summary>
    public interface INavigation
    {
        /// <summary>Replace the first ship's route (the first leg starts where the ship is now).</summary>
        bool SetRoute(IReadOnlyList<RouteWaypoint> waypoints);
    }
}
