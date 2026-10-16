#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Navigation;
using TinCan.Features.Navigation;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="NavigationUseCase"/> (server: routes, the default course, progress, discovery, the navigator's requests)
    /// and <see cref="SessionDestinationChartSource"/>. Plan: navigation-compass.md.
    /// </summary>
    public class NavigationUseCaseTests
    {
        private sealed class FakeSessionLayout : ISessionLayout
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public int LayoutSeed { get; set; } = 7;
            public Vector3 Origin { get; set; }
            public Vector3 Destination { get; set; } = new(0f, 0f, 1000f);
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private FakeActorRegistry _actors = null!;
        private RecordingPublisher _events = null!;
        private NavigationConfig _config = null!;
        private FakeAirshipView _ship = null!;
        private FakeRouteState _route = null!;
        private FakeSessionLayout _layout = null!;
        private FakeChartSource _source = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _events = new RecordingPublisher();
            _config = ScriptableObject.CreateInstance<NavigationConfig>();
            _config.MaxWaypoints = 4;
            _config.WaypointRadius = 20f;
            _ship = new FakeAirshipView();
            _route = new FakeRouteState(_ship);
            _layout = new FakeSessionLayout();
            _source = new FakeChartSource();
            _actors.Register(_ship);
            _actors.Register(_route);
            _actors.Register(_layout);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        private NavigationUseCase UseCase() =>
            new(new FakeNetworkService(), _actors, _events,
                new ChartContents(new IChartSource[] { new SessionDestinationChartSource(_actors), _source }),
                new RouteProgressProcessor(), _config);

        [Test]
        public void AShipWithoutARoute_GetsOneToTheSessionDestination()
        {
            UseCase().Tick();

            Assert.That(_route.Count, Is.EqualTo(1));
            Assert.That(_route.Waypoint(0).PoiId, Is.EqualTo(SessionDestinationChartSource.PoiId));
            Assert.That(_route.Waypoint(0).Position, Is.EqualTo(_layout.Destination));
            Assert.That(_route.ActiveIndex, Is.EqualTo(0));
            Assert.That(_events.Events.OfType<RouteChangedEvent>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void BeforeASessionBegins_ThereIsNoDefaultCourse()
        {
            _layout.LayoutSeed = 0;

            UseCase().Tick();

            Assert.That(_route.Count, Is.EqualTo(0));
        }

        [Test]
        public void APointOfInterestWaypoint_FollowsItsPoint()
        {
            var navigation = UseCase();
            navigation.Tick();

            _layout.Destination = new Vector3(400f, 50f, 2000f);
            navigation.Tick();

            Assert.That(_route.Waypoint(0).Position, Is.EqualTo(_layout.Destination));
        }

        [Test]
        public void PassingAWaypoint_MovesTheCompassOn_AndTheLastStaysActive()
        {
            var navigation = UseCase();
            navigation.SetRoute(new[] { new RouteWaypoint(new Vector3(0f, 0f, 100f)), new RouteWaypoint(new Vector3(0f, 0f, 500f)) });

            _ship.Transform.position = new Vector3(5f, 0f, 95f);
            navigation.Tick();
            Assert.That(_route.ActiveIndex, Is.EqualTo(1));
            Assert.That(_events.Events.OfType<WaypointReachedEvent>().Single().Next, Is.EqualTo(1));

            _ship.Transform.position = new Vector3(0f, 0f, 900f);
            navigation.Tick();
            Assert.That(_route.ActiveIndex, Is.EqualTo(1), "the destination is never passed");
        }

        [Test]
        public void ANavigatorsRoute_IsTaken_WhenTheyAreAtThisShipsChartTable()
        {
            _actors.Register(new FakeChartTable(_ship) { OccupantClientId = 0 });
            _route.RequestRoute(new[] { new RouteWaypoint(new Vector3(100f, 30f, 200f)), new RouteWaypoint(Vector3.zero, SessionDestinationChartSource.PoiId) }, 0);

            UseCase().Tick();

            Assert.That(_route.Count, Is.EqualTo(2));
            Assert.That(_route.Waypoint(0).Position, Is.EqualTo(new Vector3(100f, 30f, 200f)));
            Assert.That(_route.Waypoint(1).Position, Is.EqualTo(_layout.Destination), "a POI waypoint snaps to its point");
        }

        [Test]
        public void ARoute_FromSomeoneNotAtTheTable_IsRefused()
        {
            _actors.Register(new FakeChartTable(_ship) { OccupantClientId = 5 });
            _actors.Register(new FakeChartTable(new FakeAirshipView("Other")) { OccupantClientId = 0 });
            _route.RequestRoute(new[] { new RouteWaypoint(new Vector3(100f, 30f, 200f)) }, 0);

            UseCase().Tick();

            Assert.That(_route.Count, Is.EqualTo(1), "only the default course");
            Assert.That(_route.Waypoint(0).PoiId, Is.EqualTo(SessionDestinationChartSource.PoiId));
        }

        [Test]
        public void ARoute_TooLong_OrToAHiddenPoint_IsRefused()
        {
            _actors.Register(new FakeChartTable(_ship) { OccupantClientId = 0 });
            _source.Points.Add(new PointOfInterest(42, "Wreck", new Vector3(0f, 0f, 3000f), startsKnown: false, discoverRadius: 100f));
            var navigation = UseCase();
            navigation.Tick();
            int before = _route.SetRouteCalls;

            _route.RequestRoute(Enumerable.Repeat(new RouteWaypoint(Vector3.one), 5).ToArray(), 0);
            _route.RequestRoute(new[] { new RouteWaypoint(Vector3.zero, 42) }, 0);
            navigation.Tick();

            Assert.That(_route.SetRouteCalls, Is.EqualTo(before));
        }

        [Test]
        public void AHiddenPoint_IsDiscovered_WithinItsRadius_Once()
        {
            _source.Points.Add(new PointOfInterest(42, "Wreck", new Vector3(0f, 0f, 300f), startsKnown: false, discoverRadius: 100f));
            var navigation = UseCase();

            navigation.Tick();
            Assert.That(_route.IsDiscovered(42), Is.False);

            _ship.Transform.position = new Vector3(0f, 0f, 220f);
            navigation.Tick();
            navigation.Tick();

            Assert.That(_route.IsDiscovered(42), Is.True);
            Assert.That(_events.Events.OfType<PointOfInterestDiscoveredEvent>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ANewSession_ClearsTheRouteAndDiscoveries_ThenLaysANewCourse()
        {
            var navigation = UseCase();
            navigation.SetRoute(new[] { new RouteWaypoint(new Vector3(0f, 0f, 100f)) });
            _route.ServerDiscover(42);

            navigation.ResetForSession();
            Assert.That(_route.Count, Is.EqualTo(0));
            Assert.That(_route.DiscoveredCount, Is.EqualTo(0));

            navigation.Tick();
            Assert.That(_route.Waypoint(0).PoiId, Is.EqualTo(SessionDestinationChartSource.PoiId));
        }

        [Test]
        public void SetRoute_KeepsAtMostTheMaximum()
        {
            UseCase().SetRoute(Enumerable.Range(1, 9).Select(i => new RouteWaypoint(new Vector3(0f, 0f, i * 100f))).ToArray());

            Assert.That(_route.Count, Is.EqualTo(_config.MaxWaypoints));
        }
    }
}
