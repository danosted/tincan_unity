#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.SkyHazards;
using UnityEngine;
using TinCan.Tests.Fakes;

namespace TinCan.Features.SkyHazards.Tests
{
    /// <summary>
    /// <see cref="SkyHazardUseCase"/>: hazards on demand, shot down, and ship contact (plans <c>cannon-and-hazards.md</c> S1,
    /// <c>first-voyage.md</c> V1; the field and homing were removed in <c>hazards-and-boons.md</c> H0).
    /// </summary>
    public class SkyHazardTests
    {
        /// <summary>A hazard with real health on its own controller, so "shot down" is read from GAS as in the game.</summary>
        private sealed class FakeHazard : ISkyHazard
        {
            private readonly FakeAbilityController _controller = new();
            private readonly HealthAttributeSet _health;

            public FakeHazard(HealthAttribute health, MaxHealthAttribute maxHealth)
            {
                _health = new HealthAttributeSet(_controller, health, maxHealth);
                _health.InitializeBaseValues(100f);
                _controller.RegisterAttributeSet(_health);
            }

            public readonly GameObject GameObject = new("Hazard");
            public Transform? Transform => GameObject != null ? GameObject.transform : null;
            public IAbilityControllerBase? Controller => _controller;

            public void ShootDown() => _controller.SetAttribute(_health.HealthDef, new AttributeValue(0f));
        }

        private sealed class FakeSpawner : ISkyHazardSpawner
        {
            private readonly HealthAttribute _health;
            private readonly MaxHealthAttribute _maxHealth;

            public FakeSpawner(HealthAttribute health, MaxHealthAttribute maxHealth)
            {
                _health = health;
                _maxHealth = maxHealth;
            }

            public List<FakeHazard> Spawned { get; } = new();
            public List<ISkyHazard> Despawned { get; } = new();

            public ISkyHazard? Spawn(Vector3 position)
            {
                var hazard = new FakeHazard(_health, _maxHealth);
                hazard.GameObject.transform.position = position;
                Spawned.Add(hazard);
                return hazard;
            }

            public void Despawn(ISkyHazard hazard) => Despawned.Add(hazard);
        }

        /// <summary>Contact is "within Reach metres of the ship's origin", standing in for the physics overlap.</summary>
        private sealed class FakeContact : IShipContactQuery
        {
            public float Reach = 5f;
            public bool Touches(IAirshipView ship, Vector3 point, float radius) => Vector3.Distance(ship.Transform.position, point) <= Reach;
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private readonly List<Object> _assets = new();
        private FakeTimeService _time = null!;
        private FakeActorRegistry _actors = null!;
        private RecordingPublisher _events = null!;
        private FakeSpawner _spawner = null!;
        private FakeContact _contact = null!;
        private SkyHazardConfig _config = null!;
        private FakeAbilityController _shipController = null!;
        private FakeAirshipView _ship = null!;
        private HealthAttribute _health = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService { DeltaTime = 0.5f };
            _actors = new FakeActorRegistry();
            _events = new RecordingPublisher();
            _contact = new FakeContact();
            _config = Create<SkyHazardConfig>("SkyHazardConfig");
            _config.DespawnDelay = 1f;

            _health = Create<HealthAttribute>("Attr_Health");
            var maxHealth = Create<MaxHealthAttribute>("Attr_MaxHealth");
            _spawner = new FakeSpawner(_health, maxHealth);
            _config.ImpactEffect = Create<GameplayEffectDefinition>("GE_HazardImpact");
            _config.ImpactEffect.DurationType = DurationType.Instant;
            _config.ImpactEffect.GrantedTags = new List<GameplayTag>();
            _config.ImpactEffect.Modifiers = new List<AttributeModifier>
            {
                new() { Attribute = _health, Operation = ModifierOp.Add, Value = -100f, ClampMaxAttribute = maxHealth }
            };

            _shipController = new FakeAbilityController();
            _shipController.SetAttribute(maxHealth, new AttributeValue(1000f));
            _shipController.SetAttribute(_health, new AttributeValue(1000f));
            _ship = new FakeAirshipView("Airship", _shipController);
            _actors.Register(_ship);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var hazard in _spawner.Spawned) Object.DestroyImmediate(hazard.GameObject);
            Object.DestroyImmediate(_ship.GameObject);
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private float ShipHealth => _shipController.TryGetAttribute(_health, out var value) ? value.CurrentValue : -1f;

        [Test]
        public void NothingSpawnsByItself()
        {
            var hazards = UseCase();

            for (int i = 0; i < 20; i++) hazards.Tick();

            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void ShotDown_IsCounted_ThenDespawnedAfterTheDelay()
        {
            var hazards = UseCase();
            var hazard = (FakeHazard)hazards.SpawnAt(new Vector3(40f, 0f, 0f))!;

            hazard.ShootDown();
            hazards.Tick();
            Assert.That(hazards.Destroyed, Is.EqualTo(1));
            Assert.That(_spawner.Despawned, Is.Empty, "kept for the delay");

            hazards.Tick();
            hazards.Tick();
            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { hazard }));
            Assert.That(hazards.Alive, Is.Empty);
            Assert.That(hazards.Destroyed, Is.EqualTo(1), "counted once");
        }

        [Test]
        public void Target_PlacedOnDemand_HangsStill()
        {
            var hazards = UseCase();
            var target = hazards.SpawnAt(new Vector3(40f, 0f, 0f))!;

            for (int i = 0; i < 10; i++) hazards.Tick();

            Assert.That(target.Transform!.position, Is.EqualTo(new Vector3(40f, 0f, 0f)));
            Assert.That(hazards.Alive, Has.Member(target));
        }

        [Test]
        public void Launched_FliesOnItsVelocity_ThenLeavesTheSky()
        {
            var hazards = UseCase(); // ticks of 0.5 s
            var debris = hazards.Launch(new Vector3(100f, 0f, 0f), new Vector3(0f, 0f, 4f), lifetime: 2f)!;

            hazards.Tick();
            hazards.Tick();
            Assert.That(debris.Transform!.position, Is.EqualTo(new Vector3(100f, 0f, 4f)), "two ticks at 4 m/s");

            hazards.Tick();
            hazards.Tick();
            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { debris }), "gone after its lifetime");
            Assert.That(hazards.Alive, Is.Empty);
            Assert.That(hazards.Hits, Is.Zero);
        }

        [Test]
        public void Launched_IntoTheShip_HurtsIt()
        {
            var hazards = UseCase();
            hazards.Launch(new Vector3(0f, 0f, -20f), new Vector3(0f, 0f, 20f), lifetime: 10f);

            for (int i = 0; i < 4; i++) hazards.Tick();

            Assert.That(hazards.Hits, Is.EqualTo(1));
            Assert.That(ShipHealth, Is.EqualTo(900f).Within(1e-3f));
        }

        [Test]
        public void ResetForSession_ClearsTheSky()
        {
            var hazards = UseCase();
            var first = hazards.SpawnAt(new Vector3(40f, 0f, 0f))!;
            var second = hazards.SpawnAt(new Vector3(0f, 0f, 40f))!;

            hazards.ResetForSession();

            Assert.That(hazards.Alive, Is.Empty);
            Assert.That(_spawner.Despawned, Is.EquivalentTo(new[] { first, second }));
        }

        [Test]
        public void Contact_HurtsTheShipOnce_CountsTheHit_AndRemovesTheHazard()
        {
            var hazards = UseCase();
            var hazard = hazards.SpawnAt(new Vector3(4f, 0f, 0f))!; // inside the 5 m reach

            hazards.Tick();
            hazards.Tick();

            Assert.That(ShipHealth, Is.EqualTo(900f).Within(1e-3f));
            Assert.That(hazards.Hits, Is.EqualTo(1));
            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { hazard }));
            Assert.That(hazards.Alive, Is.Empty);
            Assert.That(_events.Events.OfType<SkyHazardHitShipEvent>().Select(e => e.Hits), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ShotDown_NeverHitsTheShip()
        {
            var hazards = UseCase();
            var hazard = (FakeHazard)hazards.SpawnAt(new Vector3(1f, 0f, 0f))!;
            hazard.ShootDown();

            hazards.Tick();

            Assert.That(hazards.Hits, Is.Zero);
            Assert.That(ShipHealth, Is.EqualTo(1000f));
        }

        private SkyHazardUseCase UseCase()
        {
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);
            return new SkyHazardUseCase(new FakeNetworkService(), _actors, _time, _events, _spawner, _contact, abilities, _config);
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
