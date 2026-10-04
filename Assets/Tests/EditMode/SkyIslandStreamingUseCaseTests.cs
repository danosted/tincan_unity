#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.SkyIslands;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="SkyIslandStreamingUseCase"/>: the islands around the ship stand, a few built per frame, nearest first;
    /// a voyage's layout replaces them and keeps its start and destination clear.
    /// </summary>
    public class SkyIslandStreamingUseCaseTests
    {
        private sealed class RecordingBuilder : ISkyIslandBuilder
        {
            public readonly Dictionary<SkyIslandId, SkyIslandSpec> Standing = new();
            public readonly List<SkyIslandSpec> Built = new();
            public int Removed;
            public int Clears;

            public object Build(in SkyIslandSpec island)
            {
                Assert.That(Standing.ContainsKey(island.Id), Is.False, $"{island.Id} built twice");
                Standing[island.Id] = island;
                Built.Add(island);
                return island.Id;
            }

            public void Remove(object built)
            {
                Assert.That(Standing.Remove((SkyIslandId)built), Is.True, "removed something not standing");
                Removed++;
            }

            public void Clear()
            {
                Standing.Clear();
                Clears++;
            }

            public bool AnyNear(Vector3 point, float reach) => false;
        }

        private sealed class FakeSessionLayout : ISessionLayout
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public int LayoutSeed { get; set; }
            public Vector3 Origin { get; set; }
            public Vector3 Destination { get; set; }
        }

        private SkyIslandConfig _config = null!;
        private FakeActorRegistry _actors = null!;
        private RecordingBuilder _builder = null!;
        private FakeAirshipView _ship = null!;
        private SkyIslandStreamingUseCase _islands = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SkyIslandConfig>();
            _config.WorldSeed = 42;
            _config.MaxBuildsPerFrame = 3;
            _actors = new FakeActorRegistry();
            _builder = new RecordingBuilder();
            _ship = new FakeAirshipView();
            _ship.Transform.position = new Vector3(0f, 40f, 0f);
            _actors.Register(_ship);
            _islands = new SkyIslandStreamingUseCase(_actors, new SkyIslandLayoutProcessor(), _builder, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void NoShip_NoIslands()
        {
            _actors.Unregister(_ship);

            _islands.Tick();

            Assert.That(_builder.Built, Is.Empty);
            Assert.That(_islands.Seed, Is.EqualTo(0));
        }

        [Test]
        public void BuildsTheIslandsAroundTheShip_AFewPerFrame_NearestFirst()
        {
            _islands.Tick();

            Assert.That(_builder.Built.Count, Is.EqualTo(3), "MaxBuildsPerFrame");
            Assert.That(_islands.Pending, Is.GreaterThan(0));
            Assert.That(_islands.Seed, Is.EqualTo(42), "the config's seed before any voyage");

            Settle();

            var expected = Expected(42, _ship.Transform.position, Vector3.zero);
            Assert.That(_builder.Standing.Keys, Is.EquivalentTo(expected.Select(island => island.Id)));
            Assert.That(_islands.StandingCount, Is.EqualTo(expected.Count));
            var distances = _builder.Built.Select(island => Level(island.Top - _ship.Transform.position)).ToArray();
            Assert.That(distances, Is.Ordered, "nearest first");
        }

        [Test]
        public void OnAClient_TheShipItDoesNotSimulate_StillGetsIslands()
        {
            _ship.IsSimulating = false;

            Settle();

            Assert.That(_builder.Standing, Is.Not.Empty);
        }

        [Test]
        public void BeforeAnyVoyage_TheWorldOriginIsKeptClear()
        {
            Settle();

            foreach (var island in _builder.Standing.Values)
                Assert.That(Level(island.Top), Is.GreaterThanOrEqualTo(_config.KeepOutRadius + island.Radius));
        }

        [Test]
        public void AVoyageLayout_ReplacesEveryIsland_AndKeepsItsStartAndDestinationClear()
        {
            Settle();
            int before = _builder.Standing.Count;

            var origin = new Vector3(300f, 40f, -200f);
            var destination = new Vector3(300f, 40f, 1300f);
            _actors.Register(new FakeSessionLayout { LayoutSeed = 7, Origin = origin, Destination = destination });
            Settle();

            Assert.That(_islands.Seed, Is.EqualTo(7));
            Assert.That(_builder.Removed, Is.EqualTo(before), "every island of the old layout went");
            Assert.That(_builder.Standing.Values.All(island => island.Id.Seed == 7), Is.True);
            foreach (var island in _builder.Standing.Values)
            {
                Assert.That(Level(island.Top - origin), Is.GreaterThanOrEqualTo(_config.KeepOutRadius + island.Radius));
                Assert.That(Level(island.Top - destination), Is.GreaterThanOrEqualTo(_config.KeepOutRadius + island.Radius));
            }
        }

        [Test]
        public void ASessionWithoutASeedYet_KeepsTheConfigLayout()
        {
            _actors.Register(new FakeSessionLayout { LayoutSeed = 0 });

            Settle();

            Assert.That(_islands.Seed, Is.EqualTo(42));
        }

        [Test]
        public void AMovedDestination_RemovesOnlyTheIslandsInItsWay()
        {
            var session = new FakeSessionLayout { LayoutSeed = 7, Origin = new Vector3(5000f, 0f, 5000f), Destination = new Vector3(6000f, 0f, 6000f) };
            _actors.Register(session);
            Settle();
            int built = _builder.Built.Count;
            var target = _builder.Standing.Values.First(island => !island.IsSatellite);

            session.Destination = target.Top;
            Settle();

            Assert.That(_builder.Standing.Keys, Has.No.Member(target.Id));
            Assert.That(_builder.Built.Count, Is.EqualTo(built), "nothing rebuilt");
            Assert.That(_builder.Removed, Is.InRange(1, 8));
        }

        [Test]
        public void FlyingAway_RemovesTheIslandsLeftBehind_AndBuildsTheOnesAhead()
        {
            Settle();
            var behind = _builder.Standing.Keys.ToArray();

            _ship.Transform.position = new Vector3(0f, 40f, 5000f);
            Settle();

            Assert.That(_builder.Standing.Keys.Intersect(behind), Is.Empty);
            var expected = Expected(42, _ship.Transform.position, Vector3.zero);
            Assert.That(_builder.Standing.Keys, Is.EquivalentTo(expected.Select(island => island.Id)));
        }

        [Test]
        public void TheShipGoing_TakesTheIslandsWithIt()
        {
            Settle();
            Assume.That(_builder.Standing, Is.Not.Empty);

            _actors.Unregister(_ship);
            _islands.Tick();

            Assert.That(_builder.Standing, Is.Empty);
            Assert.That(_islands.StandingCount, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_ClearsTheBuilder()
        {
            Settle();

            _islands.Dispose();

            Assert.That(_builder.Clears, Is.EqualTo(1));
            Assert.That(_islands.StandingCount, Is.EqualTo(0));
        }

        private void Settle()
        {
            for (int frame = 0; frame < 1000; frame++)
            {
                _islands.Tick();
                if (_islands.Pending == 0) return;
            }
            Assert.Fail("islands never settled");
        }

        private List<SkyIslandSpec> Expected(int seed, Vector3 focus, Vector3 keepClear)
        {
            var islands = new List<SkyIslandSpec>();
            var layout = new SkyIslandLayoutProcessor();
            var centre = layout.CellCentre(layout.CellOf(focus, _config.CellSize), _config.CellSize);
            layout.Around(seed, centre, _config.StreamRadius, _config.LayoutRules, new[] { keepClear }, islands);
            return islands;
        }

        private static float Level(Vector3 vector) => new Vector2(vector.x, vector.z).magnitude;
    }
}
