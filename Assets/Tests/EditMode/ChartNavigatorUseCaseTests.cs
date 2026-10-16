#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Navigation;
using TinCan.Features.Navigation;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ChartNavigatorUseCase"/> (the navigator's peer: cursor, add, drag, raise, remove, pick a POI, send) and
    /// <see cref="NavigatorHudPresenter"/>. Plan: navigation-compass.md (S2).
    /// </summary>
    public class ChartNavigatorUseCaseTests
    {
        private const ulong Navigator = 3;

        private FakeActorRegistry _actors = null!;
        private FakeSessionNetworkService _network = null!;
        private FakeInputReader _input = null!;
        private NavigatorInputContext _controls = null!;
        private NavigationConfig _config = null!;
        private FakeAirshipView _ship = null!;
        private FakeRouteState _route = null!;
        private FakeChartTable _table = null!;
        private FakeChartSource _source = null!;
        private FakeTimeService _time = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _network = new FakeSessionNetworkService { IsClient = true, LocalClientId = Navigator };
            _input = new FakeInputReader();
            _controls = ScriptableObject.CreateInstance<NavigatorInputContext>();
            _controls.Cursor = InputActionId.Create("Navigator/Cursor");
            _controls.Place = InputActionId.Create("Navigator/Place");
            _controls.Remove = InputActionId.Create("Navigator/Remove");
            _controls.Altitude = InputActionId.Create("Navigator/Altitude");
            _controls.Zoom = InputActionId.Create("Navigator/Zoom");
            _controls.Leave = InputActionId.Create("Navigator/Leave");
            _config = ScriptableObject.CreateInstance<NavigationConfig>();
            _config.ChartRange = 1000f;
            _config.CursorSpeed = 0.01f;
            _config.PickRadius = 0.05f;
            _config.SendInterval = 0f;
            _config.AltitudeRate = 10f;
            _config.MaxWaypoints = 4;
            _ship = new FakeAirshipView();
            _route = new FakeRouteState(_ship);
            _table = new FakeChartTable(_ship) { OccupantClientId = Navigator };
            _source = new FakeChartSource();
            _time = new FakeTimeService { DeltaTime = 1f };
            _actors.Register(_ship);
            _actors.Register(_route);
            _actors.Register(_table);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            Object.DestroyImmediate(_controls);
            Object.DestroyImmediate(_config);
        }

        private ChartNavigatorUseCase UseCase() =>
            new(_actors, _network, _input, _controls, _config, new ChartContents(new IChartSource[] { _source }),
                new ChartProjectionProcessor(), new RouteEditProcessor(), _time);

        /// <summary>One frame with the cursor moved by <paramref name="delta"/> (in cursor speed units) and these presses.</summary>
        private void Frame(ChartNavigatorUseCase navigator, Vector2 delta = default, bool click = false, bool hold = false,
            bool remove = false, float altitude = 0f, float zoom = 0f)
        {
            _input.Vectors[_controls.Cursor!] = delta;
            _input.Triggered.Clear();
            _input.Pressed.Clear();
            if (click) _input.Triggered.Add(_controls.Place!);
            if (click || hold) _input.Pressed.Add(_controls.Place!);
            if (remove) _input.Triggered.Add(_controls.Remove!);
            _input.Axes[_controls.Altitude!] = altitude;
            _input.Axes[_controls.Zoom!] = zoom;
            navigator.Tick();
        }

        private RouteRequest LastRequest()
        {
            RouteRequest last = default;
            while (_route.Requests.Count > 0) last = _route.Requests.Dequeue();
            return last;
        }

        [Test]
        public void NotAtATable_ThereIsNoChart()
        {
            _table.OccupantClientId = 9;
            var navigator = UseCase();

            Frame(navigator);

            Assert.That(navigator.View, Is.Null);
        }

        [Test]
        public void AClick_OnAnEmptyRoute_AddsAWaypoint_WhereTheCursorIs_AtTheShipsHeight()
        {
            _ship.Transform.position = new Vector3(0f, 80f, 0f);
            var navigator = UseCase();
            Frame(navigator);

            // The cursor starts at (0, 0.3): 300 m ahead. Move it 0.2 to starboard.
            Frame(navigator, delta: new Vector2(20f, 0f), click: true);

            var request = LastRequest();
            Assert.That(request.Waypoints.Count, Is.EqualTo(1));
            Assert.That(request.Waypoints[0].Position.x, Is.EqualTo(200f).Within(0.5f));
            Assert.That(request.Waypoints[0].Position.z, Is.EqualTo(300f).Within(0.5f));
            Assert.That(request.Waypoints[0].Position.y, Is.EqualTo(80f).Within(0.01f));
            Assert.That(request.ActiveIndex, Is.EqualTo(0));
        }

        [Test]
        public void AClick_NearTheFirstLeg_GoesBeforeTheDestination()
        {
            _route.ServerSetRoute(new[] { new RouteWaypoint(new Vector3(0f, 0f, 800f), 1) }, 0, Vector3.zero);
            var navigator = UseCase();

            Frame(navigator, click: true);

            var request = LastRequest();
            Assert.That(request.Waypoints.Count, Is.EqualTo(2));
            Assert.That(request.Waypoints[0].IsPoi, Is.False);
            Assert.That(request.Waypoints[1].PoiId, Is.EqualTo(1));
        }

        [Test]
        public void Dragging_MovesTheWaypoint_AndReleasingSendsIt()
        {
            _route.ServerSetRoute(new[] { new RouteWaypoint(new Vector3(0f, 0f, 300f)) }, 0, Vector3.zero);
            var navigator = UseCase();

            Frame(navigator, click: true);
            Frame(navigator, delta: new Vector2(0f, 20f), hold: true);
            Frame(navigator);

            var request = LastRequest();
            Assert.That(request.Waypoints.Count, Is.EqualTo(1), "picked, not added");
            Assert.That(request.Waypoints[0].Position.z, Is.EqualTo(500f).Within(0.5f));
        }

        [Test]
        public void Raise_LiftsThePickedWaypoint()
        {
            _route.ServerSetRoute(new[] { new RouteWaypoint(new Vector3(0f, 0f, 300f)) }, 0, Vector3.zero);
            var navigator = UseCase();

            Frame(navigator, click: true);
            Frame(navigator);
            Frame(navigator, altitude: 1f);
            Frame(navigator, altitude: 1f);
            Frame(navigator);

            Assert.That(LastRequest().Waypoints[0].Position.y, Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void RightClick_RemovesTheWaypointUnderTheCursor()
        {
            _route.ServerSetRoute(new[] { new RouteWaypoint(new Vector3(0f, 0f, 300f)), new RouteWaypoint(new Vector3(0f, 0f, 900f)) }, 1, Vector3.zero);
            var navigator = UseCase();

            Frame(navigator, remove: true);

            var request = LastRequest();
            Assert.That(request.Waypoints.Single().Position.z, Is.EqualTo(900f));
            Assert.That(request.ActiveIndex, Is.EqualTo(0), "still pointing at the same waypoint");
        }

        [Test]
        public void AClick_OnAPointOfInterest_EndsTheRouteThere_ButOnlyAVisibleOne()
        {
            _route.ServerSetRoute(new[] { new RouteWaypoint(new Vector3(0f, 0f, 900f), 1) }, 0, Vector3.zero);
            _source.Points.Add(new PointOfInterest(7, "Isle", new Vector3(300f, 40f, 300f), startsKnown: true));
            _source.Points.Add(new PointOfInterest(8, "Wreck", new Vector3(-300f, 0f, 300f), startsKnown: false));
            var navigator = UseCase();

            Frame(navigator, delta: new Vector2(-30f, 0f), click: true);
            Assert.That(LastRequest().Waypoints.Any(waypoint => waypoint.PoiId == 8), Is.False, "the wreck is hidden");

            Frame(navigator, delta: new Vector2(60f, 0f));
            Frame(navigator, click: true);

            var request = LastRequest();
            Assert.That(request.Waypoints[^1].PoiId, Is.EqualTo(7), "the island replaces the destination as the end");
        }

        [Test]
        public void TheRoute_NeverGrowsPastTheMaximum()
        {
            _route.ServerSetRoute(Enumerable.Range(1, 4).Select(i => new RouteWaypoint(new Vector3(i * 50f, 0f, 900f))).ToArray(), 0, Vector3.zero);
            var navigator = UseCase();

            Frame(navigator, delta: new Vector2(-50f, -50f), click: true);

            Assert.That(_route.Requests.Count, Is.EqualTo(0));
        }

        [Test]
        public void Zoom_ScalesTheChart_WithinItsLimits()
        {
            _config.ChartRangeLimits = new Vector2(800f, 1200f);
            var navigator = UseCase();

            Frame(navigator, zoom: 1f);
            Assert.That(navigator.View!.Range, Is.EqualTo(800f));
            Frame(navigator, zoom: -1f);
            Frame(navigator, zoom: -1f);
            Assert.That(navigator.View!.Range, Is.EqualTo(1200f));
        }

        [Test]
        public void TheHud_SaysTheRangeAndThePickedWaypoint()
        {
            var view = new ChartNavigatorView(_table, Vector2.zero, 2500f, 0, new[] { new RouteWaypoint(new Vector3(0f, 140f, 0f)) }, 0);

            string line = NavigatorHudPresenter.Line(view);

            StringAssert.Contains("2.5 km", line);
            StringAssert.Contains("waypoint 1 of 1, 140 m", line);
        }
    }
}
