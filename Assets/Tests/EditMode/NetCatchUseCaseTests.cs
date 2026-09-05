#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers the server-side catch: one catch per swing, only while the swinging tag is present, nearest can within
    /// reach, supply increment on the ship, and event publication.
    /// </summary>
    public class NetCatchUseCaseTests
    {
        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private FakeActorRegistry _registry = null!;
        private FakeFlyingCanSpawner _spawner = null!;
        private FlyingCanConfig _config = null!;
        private GameplayTag _swingTag = null!;
        private RecordingPublisher _events = null!;
        private NetCatchUseCase _useCase = null!;
        private FakeAirshipView _airship = null!;
        private FakeJerryCanSupplyBehaviour _supply = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeNetHumanoidView _player = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new FakeActorRegistry();
            _spawner = new FakeFlyingCanSpawner(_registry);
            _config = ScriptableObject.CreateInstance<FlyingCanConfig>();
            _swingTag = ScriptableObject.CreateInstance<GameplayTag>();
            _config.SwingingTag = _swingTag;
            _config.NetReach = 2f;
            _config.NetHeight = 1f;
            _config.CatchRadius = 1.5f;
            _events = new RecordingPublisher();
            _useCase = new NetCatchUseCase(new FakeNetworkService(), _registry, _spawner, new CatchProcessor(), _config, _events);

            _airship = new FakeAirshipView("Airship");
            _supply = FakeJerryCanSupplyBehaviour.AttachTo(_airship.GameObject, 1);
            _registry.Register(_airship);

            _movement = new FakeHumanoidMovementView("Player");
            _movement.Transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // facing +Z
            _player = new FakeNetHumanoidView(_movement);
            _registry.Register(_player);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner.DestroyAll();
            _movement.Destroy();
            _airship.Destroy();
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_swingTag);
        }

        [Test]
        public void Tick_SwingingNearACan_CatchesItOnce()
        {
            var inReach = _spawner.Spawn(new Vector3(0f, 1f, 2.5f), Vector3.zero)!; // net head is at (0,1,2)
            _spawner.Spawn(new Vector3(0f, 1f, 20f), Vector3.zero);
            _player.AddTag(_swingTag);

            _useCase.Tick();
            _useCase.Tick(); // same swing, must not catch the second (far) can either way

            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { inReach }));
            Assert.That(_supply.Count, Is.EqualTo(2));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<JerryCanCaughtEvent>());
        }

        [Test]
        public void Tick_NotSwinging_NeverCatches()
        {
            _spawner.Spawn(new Vector3(0f, 1f, 2f), Vector3.zero);

            _useCase.Tick();

            Assert.That(_spawner.Despawned, Is.Empty);
            Assert.That(_supply.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_NewSwingAfterTagDrops_CanCatchAgain()
        {
            _spawner.Spawn(new Vector3(0f, 1f, 2f), Vector3.zero);
            _player.AddTag(_swingTag);
            _useCase.Tick();
            Assert.That(_spawner.Despawned.Count, Is.EqualTo(1));

            _player.RemoveTag(_swingTag);
            _useCase.Tick();
            _spawner.Spawn(new Vector3(0f, 1f, 2f), Vector3.zero);
            _player.AddTag(_swingTag);
            _useCase.Tick();

            Assert.That(_spawner.Despawned.Count, Is.EqualTo(2));
            Assert.That(_supply.Count, Is.EqualTo(3));
        }

        [Test]
        public void Tick_CanOutOfReach_NothingHappens()
        {
            _spawner.Spawn(new Vector3(4f, 1f, 2f), Vector3.zero);
            _player.AddTag(_swingTag);

            _useCase.Tick();

            Assert.That(_spawner.Despawned, Is.Empty);
        }

        [Test]
        public void Tick_NoSwingTagConfigured_IsANoOp()
        {
            _config.SwingingTag = null;
            _spawner.Spawn(new Vector3(0f, 1f, 2f), Vector3.zero);
            _player.AddTag(_swingTag);

            Assert.DoesNotThrow(() => _useCase.Tick());
            Assert.That(_spawner.Despawned, Is.Empty);
        }
    }
}
