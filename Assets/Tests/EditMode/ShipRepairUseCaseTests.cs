#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Features.Airship.Damage;
using TinCan.Features.Targeting;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class ShipRepairUseCaseTests
    {
        private sealed class RecordingPublisher : TinCan.Core.Domain.Events.IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private readonly List<Object> _assets = new();
        private FakeActorRegistry _actors = null!;
        private FakeTimeService _time = null!;
        private RecordingPublisher _events = null!;
        private AbilitySystemUseCase _abilities = null!;
        private ShipDamageConfig _config = null!;
        private GameplayTag _repairing = null!;
        private FakeAirshipView _airship = null!;
        private FakeShipDamagePoint[] _points = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeNetHumanoidView _player = null!;
        private ShipRepairUseCase _useCase = null!;
        private TargetableRegistry _targetables = null!;
        private GameplayTag _damaged = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _time = new FakeTimeService { DeltaTime = 0.25f };
            _events = new RecordingPublisher();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);

            var health = Create<HealthAttribute>("Attr_Health");
            var maxHealth = Create<MaxHealthAttribute>("Attr_MaxHealth");
            _repairing = Create<GameplayTag>("State.Repairing");
            var repair = Create<GameplayEffectDefinition>("GE_RepairTick");
            repair.DurationType = DurationType.Instant;
            repair.Modifiers = new List<AttributeModifier> { new() { Attribute = health, Operation = ModifierOp.Add, Value = 25f, ClampMaxAttribute = maxHealth } };
            repair.GrantedTags = new List<GameplayTag>();

            _config = Create<ShipDamageConfig>("ShipDamageConfig");
            _config.RepairingTag = _repairing;
            _config.RepairEffect = repair;
            _config.RepairInterval = 0.25f;
            _damaged = Create<GameplayTag>("State.Damaged");
            var scan = Create<TargetingDefinition>("TD_RepairScan");
            scan.Source = AimSource.BodyForward;
            scan.Shape = TargetShape.Cone;
            scan.Range = 2.5f;
            scan.HorizontalAngle = 100f;
            scan.VerticalAngle = 120f;
            scan.RequiredTags = new List<GameplayTag> { _damaged };
            scan.Selection = TargetSelection.Nearest;
            var ability = Create<AbilityDefinition>("GA_RepairShip");
            ability.Targeting = scan;
            _config.RepairAbility = ability;

            _airship = new FakeAirshipView("Airship");
            _points = FakeShipDamage.AttachPoints(_airship.GameObject, 2, health, maxHealth);
            _points[0].transform.position = new Vector3(0f, 1f, 2f);
            _points[1].transform.position = new Vector3(0f, 1f, -2f);
            _targetables = new TargetableRegistry();
            foreach (var point in _points) _targetables.Register(point);
            _actors.Register(_airship);

            _movement = new FakeHumanoidMovementView("Player");
            _movement.Transform.position = Vector3.zero;
            _movement.Transform.rotation = Quaternion.identity;
            _player = new FakeNetHumanoidView(_movement);
            _actors.Register(_player);

            _useCase = new ShipRepairUseCase(new FakeNetworkService(), _actors, _time, _events, _abilities, new TargetingUseCase(_targetables, new TargetingProcessor()), _config);
        }

        [TearDown]
        public void TearDown()
        {
            _airship.Destroy();
            _movement.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Repairing_AddsHealthEveryIntervalToThePartInFront()
        {
            Break(_points[0], 0f);
            _player.AddTag(_repairing);

            _useCase.Tick();
            _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(0.5f).Within(0.001f), "Two intervals of +25 on a 100 max.");
            Assert.That(_events.Events.OfType<ShipPartRepairTickEvent>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void RepairStopsAtFullHealth()
        {
            Break(_points[0], 0.9f);
            _player.AddTag(_repairing);

            for (int i = 0; i < 5; i++) _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(1f));
            Assert.That(_points[0].IsBroken, Is.False);
            Assert.That(_events.Events.OfType<ShipPartRepairTickEvent>().Count(), Is.EqualTo(1), "No ticks on a repaired part.");
        }

        [Test]
        public void NotRepairing_DoesNothing()
        {
            Break(_points[0], 0f);

            _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(0f));
        }

        [Test]
        public void OnAClient_PredictsTheLocalPlayersTarget_WithoutRepairing()
        {
            Break(_points[0], 0f);
            _player.AddTag(_repairing);
            _actors.LocalPlayer = _player;
            var client = new ShipRepairUseCase(new ClientNetwork(), _actors, _time, _events, _abilities,
                new TargetingUseCase(_targetables, new TargetingProcessor()), _config);

            client.Tick();

            Assert.That(client.PredictedTarget, Is.SameAs(_points[0]));
            Assert.That(_points[0].Health01, Is.EqualTo(0f), "Only the server applies repair effects.");

            _player.RemoveTag(_repairing);
            client.Tick();
            Assert.That(client.PredictedTarget, Is.Null);
        }

        private sealed class ClientNetwork : TinCan.Core.Domain.Networking.INetworkService
        {
            public TinCan.Core.Domain.Networking.NetworkState State => TinCan.Core.Domain.Networking.NetworkState.Client;
            public bool IsActive => true;
            public bool IsServer => false;
            public bool IsClient => true;
            public bool IsHost => false;
            public ulong LocalClientId => 1;
            public void SetPlayerPrefab(GameObject prefab) { }
            public void SetConnection(string address, ushort port) { }
            public void StartHost() { }
            public void StartServer() { }
            public void StartClient() { }
            public void Shutdown() { }
        }

        [Test]
        public void PartWithoutTheDamagedTag_IsNotTargeted()
        {
            _points[0].SetHealth01(0f);
            _player.AddTag(_repairing);

            _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(0f), "TD_RepairScan requires State.Damaged on the target.");
        }

        [Test]
        public void PartBehindThePlayer_IsNotRepaired()
        {
            Break(_points[1], 0f);
            _player.AddTag(_repairing);

            _useCase.Tick();

            Assert.That(_points[1].Health01, Is.EqualTo(0f));
        }

        [Test]
        public void ShortTicks_AccumulateToOneInterval()
        {
            _time.DeltaTime = 0.1f;
            Break(_points[0], 0f);
            _player.AddTag(_repairing);

            _useCase.Tick();
            _useCase.Tick();
            Assert.That(_points[0].Health01, Is.EqualTo(0f), "0.2 s is less than one interval.");

            _useCase.Tick();
            Assert.That(_points[0].Health01, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void StoppingResetsProgress()
        {
            _time.DeltaTime = 0.2f;
            Break(_points[0], 0f);
            _player.AddTag(_repairing);
            _useCase.Tick();

            _player.RemoveTag(_repairing);
            _useCase.Tick();
            _player.AddTag(_repairing);
            _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(0f), "Banked progress is dropped when the player lets go.");
        }

        // In the game the breakage reconcile grants State.Damaged to a broken part; the repair scan filters on it.
        private void Break(FakeShipDamagePoint point, float health01)
        {
            point.SetHealth01(health01);
            point.FakeController.AddTag(_damaged);
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
