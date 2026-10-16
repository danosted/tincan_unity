#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Ship;
using TinCan.Features.Navigation;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A ship's route held in plain lists; <see cref="Requests"/> stands in for route requests sent over the network.</summary>
    public sealed class FakeRouteState : IRouteState
    {
        public FakeRouteState(IAirshipView? ship = null) => Ship = ship;

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public IAirshipView? Ship { get; set; }
        public List<RouteWaypoint> Waypoints { get; } = new();
        public HashSet<int> Discovered { get; } = new();
        public Queue<RouteRequest> Requests { get; } = new();
        public int SetRouteCalls { get; private set; }

        public int Count => Waypoints.Count;
        public RouteWaypoint Waypoint(int index) => Waypoints[index];
        public int ActiveIndex { get; private set; } = -1;
        public Vector3 Origin { get; private set; }
        public bool IsDiscovered(int poiId) => Discovered.Contains(poiId);
        public int DiscoveredCount => Discovered.Count;

        public void RequestRoute(IReadOnlyList<RouteWaypoint> waypoints, int activeIndex) =>
            Requests.Enqueue(new RouteRequest(0, new List<RouteWaypoint>(waypoints), activeIndex));

        public bool TryConsumeRouteRequest(out RouteRequest request) => Requests.TryDequeue(out request);

        public void ServerSetRoute(IReadOnlyList<RouteWaypoint> waypoints, int activeIndex, Vector3 origin)
        {
            SetRouteCalls++;
            Waypoints.Clear();
            Waypoints.AddRange(waypoints);
            Origin = origin;
            ActiveIndex = waypoints.Count == 0 ? -1 : Mathf.Clamp(activeIndex, 0, waypoints.Count - 1);
        }

        public void ServerSetWaypoint(int index, RouteWaypoint waypoint) => Waypoints[index] = waypoint;
        public void ServerSetActiveIndex(int index) => ActiveIndex = index;
        public void ServerDiscover(int poiId) => Discovered.Add(poiId);
        public void ServerClearDiscovered() => Discovered.Clear();
    }
}
