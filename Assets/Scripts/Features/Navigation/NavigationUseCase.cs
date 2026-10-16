#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Navigation;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Application Layer, server only, after airship movement: runs every ship's route.
    /// <list type="bullet">
    /// <item>A navigator's new route is taken only from the player at that ship's chart table, at most
    /// <see cref="NavigationConfig.MaxWaypoints"/> long, and only with points of interest the crew can see.</item>
    /// <item>A waypoint that stands for a point of interest follows it (a new voyage moves the destination).</item>
    /// <item>A ship without a route gets one to the session's destination.</item>
    /// <item>Waypoints are passed in order (<see cref="RouteProgressProcessor"/>); the last one stays active.</item>
    /// <item>Hidden points of interest are discovered when the ship comes within their radius.</item>
    /// </list>
    /// A new session clears every route and discovery (<see cref="ISessionParticipant"/>). Plan:
    /// .docs/plans/navigation-compass.md.
    /// </summary>
    public class NavigationUseCase : ISimulationTickable, ISessionParticipant, INavigation
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Navigation";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IEventPublisher _events;
        private readonly ChartContents _chart;
        private readonly RouteProgressProcessor _progress;
        private readonly NavigationConfig _config;
        private readonly List<Vector3> _points = new();
        private readonly List<RouteWaypoint> _route = new();

        public NavigationUseCase(INetworkService network, IActorRegistry actors, IEventPublisher events, ChartContents chart,
            RouteProgressProcessor progress, NavigationConfig config)
        {
            _network = network;
            _actors = actors;
            _events = events;
            _chart = chart;
            _progress = progress;
            _config = config;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var state in _actors.GetActors<IRouteState>().ToArray())
            {
                var ship = state.Ship;
                if (ship == null || ship.Transform == null) continue;

                var points = _chart.PointsOfInterest().ToArray();
                Vector3 position = ship.Transform.position;
                TakeRequests(state, ship, points);
                FollowPointsOfInterest(state, points);
                LayDefaultCourse(state, position, points);
                Advance(state, position);
                Discover(state, position, points);
            }
        }

        public bool SetRoute(IReadOnlyList<RouteWaypoint> waypoints)
        {
            if (!_network.IsServer) return false;
            var state = _actors.GetActors<IRouteState>().FirstOrDefault(candidate => candidate.Ship?.Transform != null);
            if (state == null) return false;

            Replace(state, waypoints.Take(_config.MaxWaypoints).ToList(), 0, state.Ship!.Transform.position, "set by tooling");
            return true;
        }

        public void ResetForSession()
        {
            if (!_network.IsServer) return;
            foreach (var state in _actors.GetActors<IRouteState>())
            {
                state.ServerSetRoute(System.Array.Empty<RouteWaypoint>(), -1, state.Origin);
                state.ServerClearDiscovered();
            }
        }

        public void SetSessionActive(bool active) { }

        private void TakeRequests(IRouteState state, IAirshipView ship, PointOfInterest[] points)
        {
            while (state.TryConsumeRouteRequest(out var request))
            {
                string? refusal = Refusal(state, ship, request, points);
                if (refusal != null)
                {
                    _events.LogInfo(LogSource, $"Route from client {request.ClientId} refused: {refusal}.");
                    continue;
                }

                _route.Clear();
                foreach (var waypoint in request.Waypoints)
                    _route.Add(waypoint.IsPoi ? new RouteWaypoint(points.First(p => p.Id == waypoint.PoiId).Position, waypoint.PoiId) : waypoint);
                Replace(state, _route, request.ActiveIndex, ship.Transform.position, $"plotted by client {request.ClientId}");
            }
        }

        private string? Refusal(IRouteState state, IAirshipView ship, RouteRequest request, PointOfInterest[] points)
        {
            bool atTable = _actors.GetActors<IChartTable>().Any(table => ReferenceEquals(table.Ship, ship) && table.OccupantClientId == request.ClientId);
            if (!atTable) return "not at this ship's chart table";
            if (request.Waypoints.Count > _config.MaxWaypoints) return $"{request.Waypoints.Count} waypoints, at most {_config.MaxWaypoints}";

            foreach (var waypoint in request.Waypoints)
            {
                if (!waypoint.IsPoi) continue;
                if (!points.Any(point => point.Id == waypoint.PoiId && ChartContents.IsVisible(point, state)))
                    return $"point of interest #{waypoint.PoiId} is not on the chart";
            }
            return null;
        }

        private void FollowPointsOfInterest(IRouteState state, PointOfInterest[] points)
        {
            for (int i = 0; i < state.Count; i++)
            {
                var waypoint = state.Waypoint(i);
                if (!waypoint.IsPoi) continue;
                foreach (var point in points)
                {
                    if (point.Id == waypoint.PoiId) state.ServerSetWaypoint(i, new RouteWaypoint(point.Position, point.Id));
                }
            }
        }

        private void LayDefaultCourse(IRouteState state, Vector3 position, PointOfInterest[] points)
        {
            if (state.Count > 0) return;
            foreach (var point in points)
            {
                if (point.Id != SessionDestinationChartSource.PoiId) continue;
                Replace(state, new[] { new RouteWaypoint(point.Position, point.Id) }, 0, position, "course to the destination");
                return;
            }
        }

        private void Advance(IRouteState state, Vector3 position)
        {
            if (state.Count < 2) return;

            _points.Clear();
            for (int i = 0; i < state.Count; i++) _points.Add(state.Waypoint(i).Position);
            int active = state.ActiveIndex;
            int next = _progress.ActiveAfter(_points, active, state.Origin, position, _config.WaypointRadius);
            if (next == active) return;

            state.ServerSetActiveIndex(next);
            _events.Publish(new WaypointReachedEvent(state.Id, active, next));
        }

        private void Discover(IRouteState state, Vector3 position, PointOfInterest[] points)
        {
            foreach (var point in points)
            {
                if (point.StartsKnown || state.IsDiscovered(point.Id)) continue;
                if ((point.Position - position).sqrMagnitude > point.DiscoverRadius * point.DiscoverRadius) continue;

                state.ServerDiscover(point.Id);
                _events.Publish(new PointOfInterestDiscoveredEvent(state.Id, point.Id, point.Name));
            }
        }

        private void Replace(IRouteState state, IReadOnlyList<RouteWaypoint> waypoints, int active, Vector3 origin, string reason)
        {
            state.ServerSetRoute(waypoints, active, origin);
            _events.Publish(new RouteChangedEvent(state.Id, waypoints.Count, reason));
        }
    }
}
