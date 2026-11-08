#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Navigation;
using TinCan.Features.Gates;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Boons.Tests
{
    /// <summary>
    /// <see cref="BoonGateLayoutProcessor"/>, <see cref="BoonGateUseCase"/> and <see cref="BoonGateChartSource"/>: gates
    /// laid off the route from the seed, replaced by the next voyage, on the chart. What passing a gate does is the Gates
    /// feature's (GateTests). Plan: hazards-and-boons.md (B1).
    /// </summary>
    public class BoonGateTests
    {
        private sealed class FakeGate : IGate
        {
            public Transform? Transform => null;
            public bool IsUsed => false;
            public void ServerMarkUsed() { }
        }

        private sealed class FakeGates : IGates
        {
            public List<(GateDefinition Definition, Vector3 Position)> Spawned { get; } = new();
            public List<IGate> Despawned { get; } = new();
            private readonly List<IGate> _gates = new();

            public IReadOnlyList<IGate> Gates => _gates;
            public int Passed => 0;

            public IGate? Spawn(GateDefinition definition, Vector3 position)
            {
                Spawned.Add((definition, position));
                var gate = new FakeGate();
                _gates.Add(gate);
                return gate;
            }

            public void Despawn(IGate gate)
            {
                _gates.Remove(gate);
                Despawned.Add(gate);
            }
        }

        private sealed class FakeSessionLayout : ISessionLayout
        {
            public System.Guid Id { get; } = System.Guid.NewGuid();
            public bool IsSimulating => true;
            public int LayoutSeed { get; set; } = 42;
            public Vector3 Origin => Vector3.zero;
            public Vector3 Destination => new(0f, 0f, 4000f);
        }

        private sealed class Rock : IWorldObstacleQuery
        {
            public bool Blocked;
            public bool IsBlocked(Vector3 point, float radius) => Blocked;
        }

        private sealed class NullPublisher : IEventPublisher
        {
            public void Publish<TEvent>(TEvent evt) { }
        }

        private readonly BoonGateLayoutProcessor _layout = new();
        private FakeActorRegistry _actors = null!;
        private FakeGates _gates = null!;
        private BoonsConfig _config = null!;
        private GateDefinition _speedBurst = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _gates = new FakeGates();
            _config = ScriptableObject.CreateInstance<BoonsConfig>();
            _speedBurst = ScriptableObject.CreateInstance<GateDefinition>();
            _speedBurst.Radius = 10f;
            _config.Gates.Add(_speedBurst);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_speedBurst);
        }

        [Test]
        public void Layout_SameSeed_SameGates_AndADifferentSeed_Others()
        {
            var first = Placements(42);
            var again = Placements(42);
            var other = Placements(7);

            Assert.That(first, Is.Not.Empty);
            Assert.That(again.Select(g => g.Position), Is.EqualTo(first.Select(g => g.Position)));
            Assert.That(other.Select(g => g.Position), Is.Not.EqualTo(first.Select(g => g.Position)));
        }

        [Test]
        public void Layout_EveryGate_SitsOffTheRoute_AndClearOfBothEnds()
        {
            _config.Spacing = 500f;
            foreach (var gate in Placements(42))
            {
                Assert.That(Mathf.Abs(gate.Position.x), Is.InRange(_config.LateralOffset.x - 1e-3f, _config.LateralOffset.y + 1e-3f), "off the straight line");
                Assert.That(gate.Position.z, Is.InRange(_config.KeepClear, 4000f - _config.KeepClear));
                Assert.That(gate.GateIndex, Is.EqualTo(0), "one gate to pick from");
            }
        }

        [Test]
        public void Layout_WithoutGates_LaysNothing()
        {
            _config.Gates.Clear();
            Assert.That(Placements(42), Is.Empty);
        }

        [Test]
        public void NoVoyage_NoGates()
        {
            var boons = UseCase();
            for (int i = 0; i < 5; i++) boons.Tick();
            Assert.That(_gates.Spawned, Is.Empty);
        }

        [Test]
        public void AVoyage_LaysItsGates_Once_AndANewVoyageReplacesThem()
        {
            var session = new FakeSessionLayout();
            _actors.Register(session);
            var boons = UseCase();

            boons.Tick();
            boons.Tick();
            int laid = Placements(42).Count;
            Assert.That(_gates.Spawned, Has.Count.EqualTo(laid));
            Assert.That(_gates.Spawned.Select(s => s.Definition), Is.All.SameAs(_speedBurst));

            session.LayoutSeed = 7;
            boons.Tick();
            Assert.That(_gates.Despawned, Has.Count.EqualTo(laid), "the old voyage's gates leave");
            Assert.That(_gates.Spawned, Has.Count.EqualTo(laid + Placements(7).Count));
        }

        [Test]
        public void GatesInsideRock_AreSkipped()
        {
            _actors.Register(new FakeSessionLayout());
            var boons = UseCase(new Rock { Blocked = true });

            boons.Tick();

            Assert.That(_gates.Spawned, Is.Empty);
        }

        [Test]
        public void Chart_ShowsTheVoyagesGates_AsBoons_AtTheirReach()
        {
            _actors.Register(new FakeSessionLayout());
            var source = new BoonGateChartSource(new BoonGateMap(_actors, _layout, _config), _config);
            var obstacles = new List<ChartObstacle>();

            source.CollectObstacles(new Vector3(0f, 0f, 2000f), 5000f, obstacles);

            Assert.That(obstacles, Has.Count.EqualTo(Placements(42).Count));
            Assert.That(obstacles.Select(o => o.Kind), Is.All.EqualTo(ChartObstacleKind.Boon));
            Assert.That(obstacles.Select(o => o.Radius), Is.All.EqualTo(_speedBurst.Radius));
        }

        private List<BoonGatePlacement> Placements(int seed)
        {
            var gates = new List<BoonGatePlacement>();
            _layout.Gates(seed, Vector3.zero, new Vector3(0f, 0f, 4000f), _config.Rules, gates);
            return gates;
        }

        private BoonGateUseCase UseCase(params IWorldObstacleQuery[] obstacles) =>
            new(new FakeNetworkService(), new NullPublisher(), _gates, _config, new BoonGateMap(_actors, _layout, _config), obstacles);
    }
}
