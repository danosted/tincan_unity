#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.Gates.Actions;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Gates.Tests
{
    /// <summary>
    /// <see cref="GateUseCase"/> and <see cref="ApplyEffectGateAction"/>: passing through a gate runs its actions exactly
    /// once and uses it up; a gate out of reach does nothing. Plan: hazards-and-boons.md (Gates).
    /// </summary>
    public class GateTests
    {
        private sealed class FakeGate : IGate
        {
            public readonly GameObject GameObject = new("Gate");
            public Transform? Transform => GameObject != null ? GameObject.transform : null;
            public bool IsUsed { get; private set; }
            public void ServerMarkUsed() => IsUsed = true;
        }

        private sealed class FakeSpawner : IGateSpawner
        {
            public List<FakeGate> Spawned { get; } = new();
            public List<IGate> Despawned { get; } = new();

            public IGate? Spawn(GameObject prefab, Vector3 position)
            {
                var gate = new FakeGate();
                gate.GameObject.transform.position = position;
                Spawned.Add(gate);
                return gate;
            }

            public void Despawn(IGate gate) => Despawned.Add(gate);
        }

        /// <summary>Overlap is "the gate's middle within its radius of the ship's origin", standing in for physics.</summary>
        private sealed class FakeOverlap : IShipOverlapQuery
        {
            public bool Overlaps(IAirshipView ship, Vector3 point, float radius) => Vector3.Distance(ship.Transform.position, point) <= radius;
        }

        private sealed class CountingAction : GateAction
        {
            public int Runs;
            public override void Run(in GateActionContext context) => Runs++;
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private readonly List<Object> _assets = new();
        private FakeActorRegistry _actors = null!;
        private RecordingPublisher _events = null!;
        private FakeSpawner _spawner = null!;
        private FakeAbilityController _shipController = null!;
        private FakeAirshipView _ship = null!;
        private GateDefinition _definition = null!;
        private CountingAction _action = null!;
        private GameObject _prefab = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _events = new RecordingPublisher();
            _spawner = new FakeSpawner();
            _prefab = new GameObject("GatePrefab");
            _action = new CountingAction();
            _definition = Create<GateDefinition>("Gate_Test");
            _definition.Prefab = _prefab;
            _definition.Radius = 10f;
            _definition.Actions.Add(_action);

            _shipController = new FakeAbilityController();
            _ship = new FakeAirshipView("Airship", _shipController);
            _actors.Register(_ship);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gate in _spawner.Spawned) Object.DestroyImmediate(gate.GameObject);
            _ship.Destroy();
            Object.DestroyImmediate(_prefab);
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void PassingThrough_RunsTheActionsOnce_AndUsesTheGateUp()
        {
            var gates = UseCase();
            var gate = gates.Spawn(_definition, new Vector3(0f, 0f, 5f))!;

            gates.Tick();
            gates.Tick();
            gates.Tick();

            Assert.That(_action.Runs, Is.EqualTo(1), "once while the ship sits in the gate");
            Assert.That(gate.IsUsed, Is.True);
            Assert.That(gates.Passed, Is.EqualTo(1));
            Assert.That(_events.Events.OfType<GatePassedEvent>().Select(e => e.Gate), Is.EqualTo(new[] { "Gate_Test" }));
        }

        [Test]
        public void AGateTheShipDoesNotReach_DoesNothing()
        {
            var gates = UseCase();
            var gate = gates.Spawn(_definition, new Vector3(0f, 0f, 50f))!;

            gates.Tick();

            Assert.That(gate.IsUsed, Is.False);
            Assert.That(_action.Runs, Is.Zero);
        }

        [Test]
        public void ADefinitionWithoutAPrefab_SpawnsNothing()
        {
            _definition.Prefab = null;
            Assert.That(UseCase().Spawn(_definition, Vector3.zero), Is.Null);
        }

        [Test]
        public void Despawn_TakesTheGateOutOfTheSky()
        {
            var gates = UseCase();
            var gate = gates.Spawn(_definition, new Vector3(0f, 0f, 5f))!;

            gates.Despawn(gate);
            gates.Tick();

            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { gate }));
            Assert.That(gates.Gates, Is.Empty);
            Assert.That(_action.Runs, Is.Zero, "a removed gate is never passed");
        }

        [Test]
        public void ResetForSession_ClearsTheSky()
        {
            var gates = UseCase();
            var gate = gates.Spawn(_definition, new Vector3(0f, 0f, 50f))!;

            gates.ResetForSession();

            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { gate }));
            Assert.That(gates.Gates, Is.Empty);
        }

        [Test]
        public void ApplyEffect_PutsTheEffectOnTheShip()
        {
            var speed = Create<MoveSpeedAttribute>("Attr_FlightSpeed");
            _shipController.SetAttribute(speed, new AttributeValue(10f));
            var effect = Create<GameplayEffectDefinition>("GE_Boon_SpeedBurst");
            effect.DurationType = DurationType.Instant;
            effect.GrantedTags = new List<GameplayTag>();
            effect.Modifiers = new List<AttributeModifier> { new() { Attribute = speed, Operation = ModifierOp.Add, Value = 5f } };
            _definition.Actions.Add(new ApplyEffectGateAction { Effect = effect });

            var gates = UseCase();
            gates.Spawn(_definition, new Vector3(0f, 0f, 5f));
            gates.Tick();
            gates.Tick();

            Assert.That(_shipController.TryGetAttribute(speed, out var value) ? value.CurrentValue : -1f, Is.EqualTo(15f).Within(1e-3f));
        }

        private GateUseCase UseCase()
        {
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, new FakeTimeService(), _events);
            return new GateUseCase(new FakeNetworkService(), _actors, _events, _spawner, new FakeOverlap(), abilities);
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }
    }
}
