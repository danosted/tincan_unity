#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Domain Layer: when a ship has reached the waypoint it flies to. A waypoint before the last is reached when the ship
    /// comes within the radius, or once it has passed the plane through the waypoint square to the leg (so a near miss
    /// still moves on, and the pilot never circles back to tag a point). The last waypoint is never passed: the compass
    /// keeps pointing at the destination.
    /// </summary>
    public class RouteProgressProcessor
    {
        public bool HasReached(Vector3 ship, Vector3 legStart, Vector3 target, float radius)
        {
            if ((ship - target).sqrMagnitude <= radius * radius) return true;

            Vector3 leg = target - legStart;
            return leg.sqrMagnitude > 1e-4f && Vector3.Dot(ship - target, leg) > 0f;
        }

        /// <summary>The active index after the ship at <paramref name="ship"/> has reached as many waypoints as it has.</summary>
        public int ActiveAfter(IReadOnlyList<Vector3> points, int active, Vector3 origin, Vector3 ship, float radius)
        {
            if (points.Count == 0) return -1;

            active = Mathf.Clamp(active, 0, points.Count - 1);
            while (active < points.Count - 1)
            {
                Vector3 legStart = active == 0 ? origin : points[active - 1];
                if (!HasReached(ship, legStart, points[active], radius)) break;
                active++;
            }
            return active;
        }
    }
}
