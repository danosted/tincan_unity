#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.Helm;
using TinCan.Features.Navigation;
using TinCan.Features.SkyHazards;
using UnityEngine;
using VContainer;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for navigation.
    /// <list type="bullet">
    /// <item>Server commands plot a route through a point off to starboard and up, put its first waypoint on the ship (so
    /// it is reached), and stand the subject at the chart table.</item>
    /// <item><c>FaceChartTable</c> turns the subject's camera to the chart (subject peer).</item>
    /// <item>Probes read the replicated route and table, and this peer's compass and chart, so they answer on host and
    /// client alike.</item>
    /// </list>
    /// </summary>
    public sealed class NavigationScenarioLibrary : IScenarioLibrary
    {
        // The side waypoint: this far to starboard of the ship, this far up, this far ahead.
        private static readonly Vector3 SideOffset = new(600f, 120f, 300f);
        private const float ArrowToleranceDegrees = 4f;
        private const float StandBehindTable = 0.85f;
        private const float TableTolerance = 0.8f;

        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IHumanoidRespawnService _respawn;
        private readonly INavigation _navigation;
        private readonly CompassPresenter _compasses;
        private readonly IObjectResolver _resolver;

        public NavigationScenarioLibrary(ScenarioSubject subject, INetworkService network, IActorRegistry actors, IHumanoidRespawnService respawn,
            INavigation navigation, CompassPresenter compasses, IObjectResolver resolver)
        {
            _subject = subject;
            _network = network;
            _actors = actors;
            _respawn = respawn;
            _navigation = navigation;
            _compasses = compasses;
            _resolver = resolver;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("PlotSideRoute", _ => PlotSideRoute()),
            new ScenarioCommand("ReachFirstWaypoint", _ => ReachFirstWaypoint()),
            new ScenarioCommand("PlaceSubjectAtChartTable", _ => PlaceAtTable()),
            new ScenarioCommand("FaceChartTable", _ => FaceTable()),
            new ScenarioCommand("DiscoverNearestLandmark", _ => DiscoverNearest())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("RouteEndsAtDestination", _ => CheckEndsAtDestination()),
            new ScenarioProbe("RouteWaypoints", CheckWaypoints),
            new ScenarioProbe("ActiveWaypoint", CheckActive),
            new ScenarioProbe("CompassAtHelm", _ => CompassView() != null ? ScenarioCheck.Pass("a compass at the helm") : ScenarioCheck.Fail("no compass at the helm")),
            new ScenarioProbe("CompassPointsAtActive", _ => CheckCompassPoints()),
            new ScenarioProbe("CompassArrowSide", CheckArrowSide),
            new ScenarioProbe("SubjectAtChartTable", _ => CheckAtTable()),
            new ScenarioProbe("ChartTableManned", _ => CheckManned(expected: true)),
            new ScenarioProbe("ChartTableFree", _ => CheckManned(expected: false)),
            new ScenarioProbe("ChartShowsWaypoints", CheckChartWaypoints),
            new ScenarioProbe("FirstWaypointToStarboard", _ => CheckFirstWaypointToStarboard()),
            new ScenarioProbe("FirstWaypointAboveShip", CheckFirstWaypointAbove),
            new ScenarioProbe("ChartShowsRocks", argument => CheckChartCount(argument, counts => counts.Rocks, "rocks")),
            new ScenarioProbe("ChartShowsPoints", argument => CheckChartCount(argument, counts => counts.Points, "points of interest")),
            new ScenarioProbe("ChartShowsHazards", argument => CheckChartCount(argument, counts => counts.Hazards, "hazard zones")),
            new ScenarioProbe("DiscoveredPoints", CheckDiscovered),
            new ScenarioProbe("HazardFieldScale", CheckFieldScale)
        };

        /// <summary>Server: [a point off to starboard and up, the destination].</summary>
        private ScenarioCheck PlotSideRoute()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Ship();
            var route = Route();
            if (ship == null || route == null || route.Count == 0) return ScenarioCheck.Fail("no ship, or no route to the destination yet");

            Vector3 side = ship.Transform.TransformPoint(SideOffset);
            var destination = route.Waypoint(route.Count - 1);
            return _navigation.SetRoute(new[] { new RouteWaypoint(side), destination })
                ? ScenarioCheck.Pass($"route through {side} to {destination}")
                : ScenarioCheck.Fail("SetRoute refused");
        }

        /// <summary>Server: moves the first waypoint onto the ship, so the next tick passes it.</summary>
        private ScenarioCheck ReachFirstWaypoint()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Ship();
            var route = Route();
            if (ship == null || route == null || route.Count < 2) return ScenarioCheck.Fail("no ship, or a route shorter than two");

            var waypoints = Enumerable.Range(0, route.Count).Select(route.Waypoint).ToArray();
            waypoints[0] = new RouteWaypoint(ship.Transform.position);
            return _navigation.SetRoute(waypoints) ? ScenarioCheck.Pass("first waypoint on the ship") : ScenarioCheck.Fail("SetRoute refused");
        }

        /// <summary>Server: stands the subject behind the chart table (on its seat's side), through the respawn service.</summary>
        private ScenarioCheck PlaceAtTable()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var table = TableTransform();
            if (subject == null || body == null || table == null) return ScenarioCheck.Fail("no subject or no chart table");

            // Searched from 1 m up: a table in a hold has the deck above it within the usual 3 m.
            Vector3 stand = ScenarioPlacement.OnGround(body, table.position - table.forward * StandBehindTable, table.up, 1f);
            _respawn.ResetCharacter(subject, stand, table.rotation);
            return ScenarioCheck.Pass($"subject placed {StandBehindTable} m behind the chart table");
        }

        /// <summary>Subject peer: turns the subject's camera toward the chart on the table.</summary>
        private ScenarioCheck FaceTable()
        {
            var subject = _subject.Resolve();
            var body = subject?.Movement?.Transform;
            var chart = Table()?.Chart;
            if (subject?.Look == null || body == null || chart == null) return ScenarioCheck.Fail("no subject look view, or no chart table");
            if (((IPossessable)subject).OwnerId != _network.LocalClientId) return ScenarioCheck.Fail("subject-peer command: only the owner can turn its camera");

            var (pitch, yaw) = ScenarioAim.LookAt(subject.Look, body, chart);
            return ScenarioCheck.Pass($"camera yaw {yaw:0} deg, pitch {pitch:0} deg toward the chart");
        }

        private ScenarioCheck CheckEndsAtDestination()
        {
            var route = Route();
            if (route == null || route.Count == 0) return ScenarioCheck.Fail("no route");
            var last = route.Waypoint(route.Count - 1);
            return last.PoiId == SessionDestinationChartSource.PoiId
                ? ScenarioCheck.Pass($"{route.Count} waypoint(s), ending at the destination {last.Position}")
                : ScenarioCheck.Fail($"ends at {last}");
        }

        private ScenarioCheck CheckWaypoints(string expected)
        {
            int count = Route()?.Count ?? 0;
            return count == int.Parse(expected, CultureInfo.InvariantCulture)
                ? ScenarioCheck.Pass($"{count} waypoints")
                : ScenarioCheck.Fail($"{count} waypoints");
        }

        private ScenarioCheck CheckActive(string expected)
        {
            int active = Route()?.ActiveIndex ?? -1;
            return active == int.Parse(expected, CultureInfo.InvariantCulture)
                ? ScenarioCheck.Pass($"active waypoint {active}")
                : ScenarioCheck.Fail($"active waypoint {active}");
        }

        /// <summary>This peer's compass arrow points at the active waypoint (within a few degrees).</summary>
        private ScenarioCheck CheckCompassPoints()
        {
            var arrow = CompassView()?.Arrow;
            var route = Route();
            if (arrow == null || route == null || route.ActiveIndex < 0) return ScenarioCheck.Fail("no compass arrow, or no active waypoint");
            if (!arrow.gameObject.activeInHierarchy) return ScenarioCheck.Fail("the arrow is hidden");

            Vector3 target = route.Waypoint(route.ActiveIndex).Position;
            float off = Vector3.Angle(arrow.forward, target - arrow.position);
            string detail = $"arrow {off:0.0} deg off waypoint {route.ActiveIndex}";
            return off <= ArrowToleranceDegrees ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The arrow leans to "starboard" or "port" of the bow, as seen on this peer.</summary>
        private ScenarioCheck CheckArrowSide(string side)
        {
            var view = CompassView();
            var arrow = view?.Arrow;
            if (view == null || arrow == null) return ScenarioCheck.Fail("no compass arrow");

            float x = view.transform.InverseTransformDirection(arrow.forward).x;
            string actual = x > 0f ? "starboard" : "port";
            return actual == side ? ScenarioCheck.Pass($"arrow to {actual} ({x:0.00})") : ScenarioCheck.Fail($"arrow to {actual} ({x:0.00})");
        }

        private ScenarioCheck CheckAtTable()
        {
            var body = _subject.Body;
            var table = TableTransform();
            if (body == null || table == null) return ScenarioCheck.Fail("no subject or no chart table");

            float distance = Vector3.ProjectOnPlane(body.position - table.position, table.up).magnitude;
            string detail = $"{distance:0.00} m from the chart table";
            return Mathf.Abs(distance - StandBehindTable) <= TableTolerance ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckManned(bool expected)
        {
            var subject = _subject.Resolve();
            var table = Table();
            if (table == null) return ScenarioCheck.Fail("no chart table");
            ulong? owner = subject != null ? ((IPossessable)subject).OwnerId : null;

            bool manned = table.OccupantClientId != null && table.OccupantClientId == owner;
            string detail = table.OccupantClientId is { } occupant ? $"manned by client {occupant}" : "free";
            return manned == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>The chart on this peer's table shows at least this many waypoint pins.</summary>
        private ScenarioCheck CheckChartWaypoints(string atLeast)
        {
            var table = Table();
            var presenter = _resolver.ResolveOrDefault<ChartPresenter>();
            if (table == null || presenter == null) return ScenarioCheck.Fail("no chart table, or no chart presenter");

            var counts = presenter.Counts(table);
            string detail = $"{counts.Waypoints} waypoint pins, {counts.Points} points of interest, {counts.Rocks} rocks, {counts.Hazards} hazards";
            return counts.Waypoints >= int.Parse(atLeast, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckFirstWaypointToStarboard()
        {
            var ship = Ship();
            var route = Route();
            if (ship == null || route == null || route.Count == 0) return ScenarioCheck.Fail("no ship or no route");

            Vector3 local = ship.Transform.InverseTransformPoint(route.Waypoint(0).Position);
            string detail = $"first waypoint at {local.x:0} m starboard, {local.z:0} m ahead";
            return local.x > 1f ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckFirstWaypointAbove(string metres)
        {
            var ship = Ship();
            var route = Route();
            if (ship == null || route == null || route.Count == 0) return ScenarioCheck.Fail("no ship or no route");

            float above = route.Waypoint(0).Position.y - ship.Transform.position.y;
            string detail = $"first waypoint {above:0.0} m above the ship";
            return above >= float.Parse(metres, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server: the hidden point of interest nearest the ship counts as discovered (as if the ship had flown by).</summary>
        private ScenarioCheck DiscoverNearest()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var ship = Ship();
            var route = Route();
            var chart = _resolver.ResolveOrDefault<ChartContents>();
            if (ship == null || route == null || chart == null) return ScenarioCheck.Fail("no ship, route or chart");

            Vector3 at = ship.Transform.position;
            var hidden = chart.PointsOfInterest().Where(point => !ChartContents.IsVisible(point, route)).ToArray();
            if (hidden.Length == 0) return ScenarioCheck.Fail("no hidden point of interest (are sky islands in the profile?)");

            var nearest = hidden.OrderBy(point => (point.Position - at).sqrMagnitude).First();
            route.ServerDiscover(nearest.Id);
            return ScenarioCheck.Pass($"discovered {nearest}, {Vector3.Distance(nearest.Position, at):0} m away");
        }

        private ScenarioCheck CheckChartCount(string atLeast, System.Func<(int Waypoints, int Points, int Rocks, int Hazards), int> count, string what)
        {
            var table = Table();
            var presenter = _resolver.ResolveOrDefault<ChartPresenter>();
            if (table == null || presenter == null) return ScenarioCheck.Fail("no chart table, or no chart presenter");

            int shown = count(presenter.Counts(table));
            string detail = $"{shown} {what} on the chart";
            return shown >= int.Parse(atLeast, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckDiscovered(string atLeast)
        {
            int discovered = Route()?.DiscoveredCount ?? 0;
            string detail = $"{discovered} discovered";
            return discovered >= int.Parse(atLeast, CultureInfo.InvariantCulture) ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        /// <summary>Server: the hazard field's strength where the ship is (1 inside a zone, the outside share elsewhere).</summary>
        private ScenarioCheck CheckFieldScale(string expected)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only probe");
            var hazards = _resolver.ResolveOrDefault<ISkyHazards>();
            if (hazards == null) return ScenarioCheck.Fail("no sky hazards in the profile");

            string detail = $"field scale {hazards.FieldScale:0.##}";
            return Mathf.Abs(hazards.FieldScale - float.Parse(expected, CultureInfo.InvariantCulture)) < 0.01f
                ? ScenarioCheck.Pass(detail)
                : ScenarioCheck.Fail(detail);
        }

        private CompassView? CompassView()
        {
            var helm = _actors.GetActors<IHelm>().FirstOrDefault(candidate => candidate is Component component && component != null);
            return helm != null ? _compasses.CompassAt(helm) : null;
        }

        private IChartTable? Table() => _actors.GetActors<IChartTable>().FirstOrDefault(table => table is Component component && component != null);

        private Transform? TableTransform() => (Table() as Component)?.transform;

        private IRouteState? Route() => _actors.GetActors<IRouteState>().FirstOrDefault(route => route.Ship != null);

        private IAirshipView? Ship() => Route()?.Ship;
    }
}
