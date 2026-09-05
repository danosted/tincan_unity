#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Airship.Fixtures;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers runtime furnishing of airships: every fixture spawned exactly once per ship, at the ship-relative pose,
    /// server only, and again for a ship that (re)appears later.
    /// </summary>
    public class ShipFixtureSpawningUseCaseTests
    {
        private sealed class RecordingModuleSpawner : IModuleSpawningService
        {
            public List<(GameObject Prefab, Vector3 Position, Quaternion Rotation, IActor Ship)> Calls { get; } = new();
            public void SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip) =>
                Calls.Add((prefab, worldPosition, worldRotation, parentShip));
        }

        private sealed class ListCatalog : IShipFixtureCatalog
        {
            public List<ShipFixtureDefinition> Items { get; } = new();
            public IReadOnlyList<ShipFixtureDefinition> Fixtures => Items;
        }

        private FakeActorRegistry _registry = null!;
        private RecordingModuleSpawner _spawner = null!;
        private ListCatalog _catalog = null!;
        private ShipFixtureSpawningUseCase _useCase = null!;
        private FakeAirshipView _airship = null!;
        private GameObject _prefab = null!;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _registry = new FakeActorRegistry();
            _spawner = new RecordingModuleSpawner();
            _catalog = new ListCatalog();
            _useCase = new ShipFixtureSpawningUseCase(new FakeNetworkService(), _registry, _spawner, _catalog, new FakeEventPublisher());
            _useCase.Initialize();
            _prefab = new GameObject("FixturePrefab");
            _catalog.Items.Add(Fixture(_prefab, new Vector3(1f, 0f, 21f), Vector3.zero));
            _airship = new FakeAirshipView("Airship");
            _airship.Transform.SetPositionAndRotation(new Vector3(0f, 40f, 0f), Quaternion.Euler(0f, 90f, 0f));
            _registry.Register(_airship);
        }

        [TearDown]
        public void TearDown()
        {
            _useCase.Dispose();
            _airship.Destroy();
            Object.DestroyImmediate(_prefab);
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Tick_SpawnsEachFixtureOncePerShip_AtShipLocalPose()
        {
            _useCase.Tick();
            _useCase.Tick();

            Assert.That(_spawner.Calls.Count, Is.EqualTo(1));
            var call = _spawner.Calls[0];
            Assert.That(call.Prefab, Is.SameAs(_prefab));
            Assert.That(call.Ship, Is.SameAs(_airship));
            // local (1, 0, 21) rotated 90 deg about Y -> world offset (21, 0, -1) from (0, 40, 0)
            Assert.That(call.Position.x, Is.EqualTo(21f).Within(0.001f));
            Assert.That(call.Position.y, Is.EqualTo(40f).Within(0.001f));
            Assert.That(call.Position.z, Is.EqualTo(-1f).Within(0.001f));
        }

        [Test]
        public void Tick_NotServer_DoesNothing()
        {
            var client = new ShipFixtureSpawningUseCase(new ClientNetwork(), _registry, _spawner, _catalog, new FakeEventPublisher());

            client.Tick();

            Assert.That(_spawner.Calls, Is.Empty);
        }

        [Test]
        public void Tick_SkipsFixturesWithoutPrefab_AndShipsThatAreNotSimulating()
        {
            _catalog.Items.Add(Fixture(null, Vector3.zero, Vector3.zero));
            _airship.IsSimulating = false;
            _useCase.Tick();
            Assert.That(_spawner.Calls, Is.Empty);

            _airship.IsSimulating = true;
            _useCase.Tick();
            Assert.That(_spawner.Calls.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_ShipUnregisteredAndReregistered_IsFurnishedAgain()
        {
            _useCase.Tick();
            _registry.Unregister(_airship);
            _registry.Register(_airship);

            _useCase.Tick();

            Assert.That(_spawner.Calls.Count, Is.EqualTo(2));
        }

        private ShipFixtureDefinition Fixture(GameObject? prefab, Vector3 position, Vector3 euler)
        {
            var definition = ScriptableObject.CreateInstance<ShipFixtureDefinition>();
            definition.Prefab = prefab;
            definition.LocalPosition = position;
            definition.LocalEulerAngles = euler;
            _assets.Add(definition);
            return definition;
        }

        private sealed class ClientNetwork : INetworkService
        {
            public NetworkState State => NetworkState.Client;
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
