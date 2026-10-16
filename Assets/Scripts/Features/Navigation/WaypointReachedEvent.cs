#nullable enable
using System;

namespace TinCan.Features.Navigation
{
    /// <summary>Server: a ship reached a waypoint, and its compass now points at the next one.</summary>
    public readonly struct WaypointReachedEvent
    {
        public readonly Guid RouteId;
        public readonly int Reached;
        public readonly int Next;

        public WaypointReachedEvent(Guid routeId, int reached, int next)
        {
            RouteId = routeId;
            Reached = reached;
            Next = next;
        }

        public override string ToString() => $"WaypointReached({Reached} -> {Next})";
    }
}
