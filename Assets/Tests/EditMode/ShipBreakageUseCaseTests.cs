#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using TinCan.Features.Airship.Damage;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class ShipBreakageUseCaseTests
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
        private FakeAbilityController _shipController = null!;
        private FakeAirshipView _airship = null!;
        private FakeShipDamagePoint[] _points = null!;
        private FuelLeakRateAttribute _leak = null!;
        private HealthAttribute _health = null!;
        private MaxHealthAttribute _maxHealth = null!;
        private GameplayTag _damaged = null!;
        private GameplayTag _partDamaged = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _time = new FakeTimeService { DeltaTime = 1f };
            _events = new RecordingPublisher();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);

            _leak = Create<FuelLeakRateAttribute>("Attr_FuelLeakRate");
            _damaged = Create<GameplayTag>("State.Ship.Damaged");
            var breach = Create<GameplayEffectDefinition>("GE_HullBreach");
            breach.DurationType = DurationType.Infinite;
            breach.Modifiers = new List<AttributeModifier> { new() { Attribute = _leak, Operation = ModifierOp.Add, Value = 0.5f } };
            breach.GrantedTags = new List<GameplayTag> { _damaged };

            _config = Create<ShipDamageConfig>("ShipDamageConfig");
            _config.AutoBreak = false;
            _config.FirstBreakDelay = 5f;
            _config.MinInterval = 10f;
            _config.MaxInterval = 10f;
            _config.MaxBroken = 2;
            _config.Seed = 11;
            _config.HullBreachEffect = breach;

            var health = Create<HealthAttribute>("Attr_Health");
            var maxHealth = Create<MaxHealthAttribute>("Attr_MaxHealth");
            _partDamaged = Create<GameplayTag>("State.Damaged");
            _config.PartBrokenEffect = Effect("GE_ShipPartBroken", DurationType.Infinite, new List<AttributeModifier>(), _partDamaged);
            _config.BreakEffect = Effect("GE_ShipPartBreak", DurationType.Instant,
                new List<AttributeModifier> { new() { Attribute = health, Operation = ModifierOp.Override, Value = 0f } });
            _config.RestoreEffect = Effect("GE_ShipPartRestore", DurationType.Instant,
                new List<AttributeModifier> { new() { Attribute = health, Operation = ModifierOp.Override, Value = 1e6f, ClampMaxAttribute = maxHealth } });
            _health = health;
            _maxHealth = maxHealth;

            _shipController = new FakeAbilityController();
            _shipController.SetAttribute(_leak, new AttributeValue(0f));
            _airship = new FakeAirshipView("Airship", _shipController);
            _points = FakeShipDamage.AttachPoints(_airship.GameObject, 4, _health, _maxHealth);
            _actors.Register(_airship);
        }

        [TearDown]
        public void TearDown()
        {
            _airship.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private ShipBreakageUseCase UseCase() =>
            new(new FakeNetworkService(), _actors, _time, _events, _abilities, new ShipBreakageProcessor(), _config);

        private float LeakRate => _shipController.TryGetAttribute(_leak, out var value) ? value.CurrentValue : -1f;

        [Test]
        public void TryBreak_AppliesOneBreachPerPart_AndLeaksStack()
        {
            var useCase = UseCase();

            useCase.TryBreak(0);
            useCase.TryBreak(2);

            Assert.That(_points[0].Controller.IsDamaged() && _points[2].Controller.IsDamaged(), Is.True);
            Assert.That(useCase.BrokenCount, Is.EqualTo(2));
            Assert.That(LeakRate, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(_shipController.HasTag(_damaged), Is.True);
            Assert.That(_events.Events.OfType<ShipPartBrokenEvent>().Select(e => e.PointIndex), Is.EqualTo(new[] { 0, 2 }));
        }

        [Test]
        public void TryBreak_GoesThroughTheBreakEffect_AndMarksThePart()
        {
            var useCase = UseCase();

            useCase.TryBreak(1);

            Assert.That(FakeShipDamage.HealthFraction(_points[1]), Is.EqualTo(0f), "The instant break effect set the part's health to 0.");
            Assert.That(_points[1].FakeController.HasTag(_partDamaged), Is.True);
            Assert.That(_points[0].FakeController.HasTag(_partDamaged), Is.False);

            useCase.TryRestore(1);

            Assert.That(FakeShipDamage.HealthFraction(_points[1]), Is.EqualTo(1f), "Restore overrides to max, clamped by MaxHealth.");
            Assert.That(_points[1].FakeController.HasTag(_partDamaged), Is.False);
        }

        [Test]
        public void Repair_ReleasesOnlyThatPartsBreach()
        {
            var useCase = UseCase();
            useCase.TryBreak(0);
            useCase.TryBreak(1);

            useCase.TryRestore(0);

            Assert.That(LeakRate, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(_shipController.HasTag(_damaged), Is.True, "Part 1 is still broken.");

            useCase.TryRestore(1);

            Assert.That(LeakRate, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(_shipController.HasTag(_damaged), Is.False);
            Assert.That(_events.Events.OfType<ShipPartRepairedEvent>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void PartiallyRepairedPart_StillLeaks()
        {
            var useCase = UseCase();
            useCase.TryBreak(0);

            _points[0].SetHealthFraction(0.9f);
            useCase.Tick();

            Assert.That(LeakRate, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void HealthChangedFromOutside_IsReconciledOnTick()
        {
            var useCase = UseCase();

            _points[3].SetHealthFraction(0f);
            useCase.Tick();
            Assert.That(useCase.BrokenCount, Is.EqualTo(1));

            _points[3].SetHealthFraction(1f);
            useCase.Tick();
            Assert.That(useCase.BrokenCount, Is.EqualTo(0));
            Assert.That(LeakRate, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void AutoBreak_WaitsForTheFirstDelay_ThenBreaksOnTheInterval_UpToTheCap()
        {
            var useCase = UseCase();
            useCase.AutoBreak = true;

            for (int second = 0; second < 5; second++) useCase.Tick();
            Assert.That(useCase.BrokenCount, Is.EqualTo(0), "Nothing before FirstBreakDelay.");

            useCase.Tick();
            Assert.That(useCase.BrokenCount, Is.EqualTo(1));

            for (int second = 0; second < 10; second++) useCase.Tick();
            Assert.That(useCase.BrokenCount, Is.EqualTo(2));

            for (int second = 0; second < 30; second++) useCase.Tick();
            Assert.That(useCase.BrokenCount, Is.EqualTo(2), "MaxBroken caps random breakage.");
        }

        [Test]
        public void AutoBreakOff_NeverBreaks()
        {
            var useCase = UseCase();

            for (int second = 0; second < 100; second++) useCase.Tick();

            Assert.That(useCase.BrokenCount, Is.EqualTo(0));
        }

        [Test]
        public void TryBreak_UnknownPart_Fails()
        {
            Assert.That(UseCase().TryBreak(9), Is.False);
        }

        private GameplayEffectDefinition Effect(string name, DurationType duration, List<AttributeModifier> modifiers, params GameplayTag[] tags)
        {
            var effect = Create<GameplayEffectDefinition>(name);
            effect.DurationType = duration;
            effect.Modifiers = modifiers;
            effect.GrantedTags = new List<GameplayTag>(tags);
            return effect;
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
