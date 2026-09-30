#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.SkyHazards;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="SkyHazardUseCase"/>, <see cref="SkyHazardFieldProcessor"/> and <see cref="HazardDriftProcessor"/>
    /// (plans <c>cannon-and-hazards.md</c> S1 targets, <c>first-voyage.md</c> V1 drift and ship contact).
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
            public Vector3 BodyOffset;
            public Vector3 Center(IAirshipView ship) => ship.Transform.position + BodyOffset;
            public bool Touches(IAirshipView ship, Vector3 point, float radius) => Vector3.Distance(ship.Transform.position, point) <= Reach;
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private readonly SkyHazardFieldProcessor _field = new();
        private readonly HazardDriftProcessor _drift = new();
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
            _config.MaxAlive = 3;
            _config.SpawnInterval = 1f;
            _config.DespawnDelay = 1f;
            _config.RemoveDistance = 500f;
            _config.DriftSpeed = 4f;

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
        public void FieldOff_SpawnsNothingByItself()
        {
            var hazards = UseCase(fieldEnabled: false);

            for (int i = 0; i < 20; i++) hazards.Tick();

            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void FieldOn_FillsUpToMaxAlive_OnePerInterval()
        {
            _config.DriftSpeed = 0f; // none reaches the ship and gets replaced
            var hazards = UseCase(fieldEnabled: true);

            hazards.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(1));
            hazards.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(1), "waits for the interval");

            for (int i = 0; i < 20; i++) hazards.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(3));
            Assert.That(hazards.Alive, Has.Count.EqualTo(3));
        }

        [Test]
        public void FieldOn_ABiggerCrew_KeepsMoreHazards()
        {
            _config.DriftSpeed = 0f;
            _config.MaxAlivePerExtraPlayer = 2;
            var movement = new FakeHumanoidMovementView("Player");
            try
            {
                _actors.Register(new FakeHumanoidCharacterView(movement));
                _actors.Register(new FakeHumanoidCharacterView(movement));
                var hazards = UseCase(fieldEnabled: true);

                for (int i = 0; i < 40; i++) hazards.Tick();

                Assert.That(hazards.Alive, Has.Count.EqualTo(5), "3 for the first player, 2 more for the second");
            }
            finally
            {
                movement.Destroy();
            }
        }

        [Test]
        public void ForCrew_EachExtraPlayer_RaisesTheLimit_AndShortensTheInterval()
        {
            var scaling = new SkyHazardCrewScaling(maxAlive: 4, maxAlivePerExtraPlayer: 2, spawnInterval: 6f, spawnIntervalScalePerExtraPlayer: 0.5f);

            Assert.That(_field.ForCrew(1, scaling), Is.EqualTo((4, 6f)));
            Assert.That(_field.ForCrew(0, scaling), Is.EqualTo((4, 6f)), "an empty ship counts as one player");
            var (maxAlive, interval) = _field.ForCrew(3, scaling);
            Assert.That(maxAlive, Is.EqualTo(8));
            Assert.That(interval, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void ShotDown_IsCounted_ThenDespawnedAfterTheDelay()
        {
            var hazards = UseCase(fieldEnabled: false);
            var hazard = (FakeHazard)hazards.SpawnAt(Vector3.zero)!;

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
        public void FarBehind_IsRemoved_WhileTheFieldIsOn()
        {
            var hazards = UseCase(fieldEnabled: true);
            var far = hazards.SpawnAt(new Vector3(0f, 0f, -1000f))!;

            hazards.Tick();

            Assert.That(_spawner.Despawned, Has.Member(far));
            Assert.That(hazards.Alive, Has.No.Member(far));
        }

        [Test]
        public void SpawnPoint_IsAheadOfTheShip_LevelWithTheHorizon()
        {
            var shape = new SkyHazardFieldShape(new Vector3(-20f, 0f, 50f), new Vector3(20f, 10f, 100f));
            var pitchedAndTurned = Quaternion.Euler(-30f, 90f, 0f); // nose up 30 deg, heading +X

            Vector3 point = _field.SpawnPoint(new Vector3(0f, 100f, 0f), pitchedAndTurned, shape, lateral01: 0.5f, height01: 0f, ahead01: 0f);

            Assert.That(Vector3.Distance(point, new Vector3(50f, 100f, 0f)), Is.LessThan(1e-3f), "50 m along the heading, not up the pitched nose");
        }

        [Test]
        public void SpawnPoint_SpreadsWithinTheBox_OnTheStarboardSide()
        {
            var shape = new SkyHazardFieldShape(new Vector3(30f, -5f, -20f), new Vector3(80f, 10f, 100f));

            Vector3 low = _field.SpawnPoint(Vector3.zero, Quaternion.identity, shape, lateral01: 0f, height01: 0f, ahead01: 0f);
            Vector3 high = _field.SpawnPoint(Vector3.zero, Quaternion.identity, shape, lateral01: 1f, height01: 1f, ahead01: 1f);

            Assert.That(Vector3.Distance(low, new Vector3(30f, -5f, -20f)), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(high, new Vector3(80f, 10f, 100f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void TooFar_BeyondTheRemoveDistance()
        {
            Assert.That(_field.IsTooFar(Vector3.zero, new Vector3(0f, 0f, 99f), 100f), Is.False);
            Assert.That(_field.IsTooFar(Vector3.zero, new Vector3(0f, 0f, 101f), 100f), Is.True);
        }

        [Test]
        public void Drift_ClosesOnTheShip_AtItsSpeed_WithoutOvershooting()
        {
            Vector3 step = _drift.Step(new Vector3(10f, 0f, 0f), Vector3.zero, speed: 4f, deltaTime: 0.5f);
            Assert.That(Vector3.Distance(step, new Vector3(8f, 0f, 0f)), Is.LessThan(1e-4f));

            Assert.That(_drift.Step(new Vector3(1f, 0f, 0f), Vector3.zero, 4f, 0.5f), Is.EqualTo(Vector3.zero), "stops at the ship");
            Assert.That(_drift.Step(new Vector3(10f, 0f, 0f), Vector3.zero, 0f, 0.5f), Is.EqualTo(new Vector3(10f, 0f, 0f)), "speed 0 hangs still");
        }

        [Test]
        public void Target_PlacedOnDemand_HangsStill()
        {
            var hazards = UseCase(fieldEnabled: false);
            var target = hazards.SpawnAt(new Vector3(40f, 0f, 0f))!;

            for (int i = 0; i < 10; i++) hazards.Tick();

            Assert.That(target.Transform!.position, Is.EqualTo(new Vector3(40f, 0f, 0f)));
        }

        [Test]
        public void Drifting_HomesOnTheShip()
        {
            var hazards = UseCase(fieldEnabled: false);
            var hazard = hazards.SpawnAt(new Vector3(40f, 0f, 0f), drifts: true)!;

            hazards.Tick();

            Assert.That(hazard.Transform!.position.x, Is.EqualTo(38f).Within(1e-4f), "4 m/s for half a second");
        }

        [Test]
        public void Drifting_HomesOnTheShipsBody_NotItsPivot()
        {
            _contact.BodyOffset = new Vector3(0f, -4f, 0f); // the deck sits below the pivot, as on the test ship
            _contact.Reach = 0f;
            var hazards = UseCase(fieldEnabled: false);
            var hazard = hazards.SpawnAt(new Vector3(0f, -4f, 40f), drifts: true)!;

            hazards.Tick();

            Assert.That(Vector3.Distance(hazard.Transform!.position, new Vector3(0f, -4f, 38f)), Is.LessThan(1e-4f), "straight at the body, level with it");
        }

        [Test]
        public void ResetForSession_ClearsTheSky()
        {
            var hazards = UseCase(fieldEnabled: false);
            var drifting = hazards.SpawnAt(new Vector3(40f, 0f, 0f), drifts: true)!;
            var target = hazards.SpawnAt(new Vector3(0f, 0f, 40f))!;

            hazards.ResetForSession();

            Assert.That(hazards.Alive, Is.Empty);
            Assert.That(_spawner.Despawned, Is.EquivalentTo(new[] { drifting, target }));
        }

        [Test]
        public void SetSessionActive_RunsTheFieldOnlyWhileActive_AndOnlyWhereTheConfigAllowsIt()
        {
            var hazards = UseCase(fieldEnabled: true);

            hazards.SetSessionActive(false);
            Assert.That(hazards.FieldEnabled, Is.False);
            hazards.SetSessionActive(true);
            Assert.That(hazards.FieldEnabled, Is.True);

            _config.FieldEnabled = false;
            hazards.SetSessionActive(true);
            Assert.That(hazards.FieldEnabled, Is.False);
        }

        [Test]
        public void FieldHazards_Drift()
        {
            var hazards = UseCase(fieldEnabled: true);
            hazards.Tick();
            var hazard = _spawner.Spawned[0];
            float before = Vector3.Distance(hazard.Transform!.position, Vector3.zero);

            hazards.Tick();

            Assert.That(Vector3.Distance(hazard.Transform!.position, Vector3.zero), Is.LessThan(before));
        }

        [Test]
        public void Contact_HurtsTheShipOnce_CountsTheHit_AndRemovesTheHazard()
        {
            var hazards = UseCase(fieldEnabled: false);
            var hazard = hazards.SpawnAt(new Vector3(6f, 0f, 0f), drifts: true)!;

            hazards.Tick(); // 6 -> 4 m: inside the 5 m reach
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
            var hazards = UseCase(fieldEnabled: false);
            var hazard = (FakeHazard)hazards.SpawnAt(new Vector3(1f, 0f, 0f), drifts: true)!;
            hazard.ShootDown();

            hazards.Tick();

            Assert.That(hazards.Hits, Is.Zero);
            Assert.That(ShipHealth, Is.EqualTo(1000f));
        }

        private SkyHazardUseCase UseCase(bool fieldEnabled)
        {
            _config.FieldEnabled = fieldEnabled;
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);
            return new SkyHazardUseCase(new FakeNetworkService(), _actors, _time, _events, _spawner, _field, _drift, _contact,
                abilities, _config, new System.Random(1));
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
