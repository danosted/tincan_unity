#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Features.Abilities;
using TinCan.Features.Airship;
using TinCan.Features.Airship.Fuel;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers the server-side fuel loop: drain gating, boost, tank discovery on a child object, and the stall toggle
    /// (which must be idempotent because the toggleable ability flips on every activation).
    /// </summary>
    public class FuelConsumptionUseCaseTests
    {
        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private FakeNetworkService _network = null!;
        private FakeActorRegistry _registry = null!;
        private FakeTimeService _time = null!;
        private RecordingPublisher _events = null!;
        private FuelConsumptionUseCase _useCase = null!;
        private FuelConfig _config = null!;
        private GameplayTag _boostTag = null!;
        private GameplayTag _stalledTag = null!;
        private AbilityDefinition _stallAbility = null!;
        private FakeAbilityController _controller = null!;
        private FakeAirshipView _airship = null!;
        private FakeFuelTankBehaviour _tank = null!;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _network = new FakeNetworkService();
            _registry = new FakeActorRegistry();
            _time = new FakeTimeService { DeltaTime = 0.5f };
            _events = new RecordingPublisher();
            _useCase = new FuelConsumptionUseCase(_network, _registry, _time, _events, new FuelConsumptionProcessor());

            _boostTag = Create<GameplayTag>();
            _stalledTag = Create<GameplayTag>();
            _stallAbility = Create<AbilityDefinition>();
            _config = Create<FuelConfig>();
            _config.DrainPerSecondAtFullThrottle = 2f;
            _config.BoostMultiplier = 2f;
            _config.BoostActiveTag = _boostTag;
            _config.StalledTag = _stalledTag;
            _config.StallAbility = _stallAbility;

            _controller = new FakeAbilityController();
            _controller.GrantsTagWhileActive(_stallAbility, _stalledTag);

            _airship = new FakeAirshipView("Airship", _controller);
            _tank = FakeFuelTankBehaviour.AttachTo(_airship.GameObject, _config);
            _registry.Register(_airship);
        }

        [TearDown]
        public void TearDown()
        {
            _airship.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Tick_NotServer_DoesNothing()
        {
            var clientNetwork = new ClientNetworkService();
            var useCase = new FuelConsumptionUseCase(clientNetwork, _registry, _time, _events, new FuelConsumptionProcessor());
            Drive(1f);

            useCase.Tick();

            Assert.That(_tank.Inner.TotalConsumed, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_NoPossessor_DoesNotDrain()
        {
            _airship.PossessorId = null;
            _airship.InputState = new AirshipInputState { Throttle = 1f };

            _useCase.Tick();

            Assert.That(_tank.Inner.TotalConsumed, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_PossessedButIdle_DoesNotDrain()
        {
            Drive(0f);

            _useCase.Tick();

            Assert.That(_tank.Inner.TotalConsumed, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_Driven_DrainsRateTimesDeltaTime()
        {
            Drive(1f);

            _useCase.Tick();

            Assert.That(_tank.Inner.TotalConsumed, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(_tank.Level, Is.EqualTo(99f).Within(0.0001f));
        }

        [Test]
        public void Tick_DrivenWhileBoostTagPresent_DrainsDouble()
        {
            Drive(1f);
            _controller.AddTag(_boostTag);

            _useCase.Tick();

            Assert.That(_tank.Inner.TotalConsumed, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void Tick_FindsTankOnChildObject_AndIgnoresShipsWithoutOne()
        {
            var bareShip = new FakeAirshipView("NoTank", new FakeAbilityController());
            _registry.Register(bareShip);
            Drive(1f);

            Assert.DoesNotThrow(() => _useCase.Tick());
            Assert.That(_tank.Inner.TotalConsumed, Is.GreaterThan(0f));

            bareShip.Destroy();
        }

        [Test]
        public void Tick_TankRunsDry_ActivatesStallExactlyOnce()
        {
            Drive(1f);
            _tank.Inner.Level = 0.5f;

            _useCase.Tick(); // burns 1 -> empty -> stall
            _useCase.Tick(); // still empty -> no re-activation (that would toggle it off)
            _useCase.Tick();

            Assert.That(_controller.Granted, Does.Contain(_stallAbility));
            Assert.That(_controller.Activations.Count, Is.EqualTo(1));
            Assert.That(_controller.HasTag(_stalledTag), Is.True);
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<FuelEmptyEvent>());
        }

        [Test]
        public void Tick_RefuelledAfterStall_TogglesStallOffOnce()
        {
            Drive(1f);
            _tank.Inner.Level = 0f;
            _useCase.Tick();
            Assert.That(_controller.HasTag(_stalledTag), Is.True);

            _tank.Inner.Refill(25f);
            _airship.InputState = new AirshipInputState { Throttle = 0f };
            _useCase.Tick();
            _useCase.Tick();

            Assert.That(_controller.HasTag(_stalledTag), Is.False);
            Assert.That(_controller.Activations.Count, Is.EqualTo(2));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<FuelRestoredEvent>());
        }

        [Test]
        public void Tick_StallDisabledInConfig_NeverActivates()
        {
            _config.StallWhenEmpty = false;
            Drive(1f);
            _tank.Inner.Level = 0f;

            _useCase.Tick();

            Assert.That(_controller.Activations, Is.Empty);
            Assert.That(_controller.HasTag(_stalledTag), Is.False);
        }

        [Test]
        public void Tick_ShipNotSimulating_IsSkipped()
        {
            _airship.IsSimulating = false;
            Drive(1f);

            _useCase.Tick();

            Assert.That(_tank.Inner.TotalConsumed, Is.EqualTo(0f));
        }

        private void Drive(float throttle)
        {
            _airship.PossessorId = 1;
            _airship.InputState = new AirshipInputState { Throttle = throttle };
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        private sealed class ClientNetworkService : TinCan.Core.Domain.Networking.INetworkService
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
    }
}
