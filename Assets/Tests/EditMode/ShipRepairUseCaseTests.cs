#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Features.Airship.Damage;
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
            _config.RepairReach = 2.5f;
            _config.RepairConeDegrees = 100f;

            _airship = new FakeAirshipView("Airship");
            _points = FakeShipDamage.AttachPoints(_airship.GameObject, 2, health, maxHealth);
            _points[0].transform.position = new Vector3(0f, 1f, 2f);
            _points[1].transform.position = new Vector3(0f, 1f, -2f);
            _actors.Register(_airship);

            _movement = new FakeHumanoidMovementView("Player");
            _movement.Transform.position = Vector3.zero;
            _movement.Transform.rotation = Quaternion.identity;
            _player = new FakeNetHumanoidView(_movement);
            _actors.Register(_player);

            _useCase = new ShipRepairUseCase(new FakeNetworkService(), _actors, _time, _events, _abilities, new RepairTargetProcessor(), _config);
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
            _points[0].SetHealth01(0f);
            _player.AddTag(_repairing);

            _useCase.Tick();
            _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(0.5f).Within(0.001f), "Two intervals of +25 on a 100 max.");
            Assert.That(_events.Events.OfType<ShipPartRepairTickEvent>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void RepairStopsAtFullHealth()
        {
            _points[0].SetHealth01(0.9f);
            _player.AddTag(_repairing);

            for (int i = 0; i < 5; i++) _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(1f));
            Assert.That(_points[0].IsBroken, Is.False);
            Assert.That(_events.Events.OfType<ShipPartRepairTickEvent>().Count(), Is.EqualTo(1), "No ticks on a repaired part.");
        }

        [Test]
        public void NotRepairing_DoesNothing()
        {
            _points[0].SetHealth01(0f);

            _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(0f));
        }

        [Test]
        public void PartBehindThePlayer_IsNotRepaired()
        {
            _points[1].SetHealth01(0f);
            _player.AddTag(_repairing);

            _useCase.Tick();

            Assert.That(_points[1].Health01, Is.EqualTo(0f));
        }

        [Test]
        public void ShortTicks_AccumulateToOneInterval()
        {
            _time.DeltaTime = 0.1f;
            _points[0].SetHealth01(0f);
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
            _points[0].SetHealth01(0f);
            _player.AddTag(_repairing);
            _useCase.Tick();

            _player.RemoveTag(_repairing);
            _useCase.Tick();
            _player.AddTag(_repairing);
            _useCase.Tick();

            Assert.That(_points[0].Health01, Is.EqualTo(0f), "Banked progress is dropped when the player lets go.");
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
