#nullable enable
using System;

namespace TinCan.Features.Navigation
{
    /// <summary>Server: a ship's route was replaced (by a navigator, the default course, or dev tooling).</summary>
    public readonly struct RouteChangedEvent
    {
        public readonly Guid RouteId;
        public readonly int Waypoints;
        public readonly string Reason;

        public RouteChangedEvent(Guid routeId, int waypoints, string reason)
        {
            RouteId = routeId;
            Waypoints = waypoints;
            Reason = reason;
        }

        public override string ToString() => $"RouteChanged({Waypoints} waypoints, {Reason})";
    }
}
