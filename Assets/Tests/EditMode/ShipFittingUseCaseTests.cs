#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipSockets;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipFittingUseCase"/> (server: mounting, refusals, fittings that lose their socket), the catalog and the
    /// fixture filter.
    /// </summary>
    public class ShipFittingUseCaseTests
    {
        private const string Cannon = "weapon.cannon";

        private sealed class RecordingSpawner : IModuleSpawningService
        {
            public List<(GameObject Prefab, Vector3 Position, GameObject Instance)> Spawned { get; } = new();
            public List<GameObject> Despawned { get; } = new();

            public GameObject SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip)
            {
                var instance = new GameObject("Fixture");
                Spawned.Add((prefab, worldPosition, instance));
                return instance;
            }

            public void DespawnModule(GameObject module)
            {
                Despawned.Add(module);
                Object.DestroyImmediate(module);
            }
        }

        private FakeActorRegistry _actors = null!;
        private FakeAirshipView _ship = null!;
        private FakeShipFittingState _state = null!;
        private FakeShipSocketList _sockets = null!;
        private RecordingSpawner _spawner = null!;
        private ShipSocketsConfig _config = null!;
        private GameObject _cannonPrefab = null!;
        private ShipFittingDefinition _cannon = null!;
        private ShipFittingUseCase _fittings = null!;
        private ShipSocketInfo _bow;
        private readonly List<Object> _objects = new();

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _ship = new FakeAirshipView("Ship");
            _state = new FakeShipFittingState(_ship);
            _actors.Register(_ship);
            _actors.Register(_state);
            _sockets = new FakeShipSocketList();
            _bow = FakeShipSockets.Socket(_ship.Transform, 7, new Vector3(1f, -0.5f, 3f));
            _sockets.Ships[_ship.Id] = new List<ShipSocketInfo> { _bow };
            _spawner = new RecordingSpawner();
            _config = Keep(ScriptableObject.CreateInstance<ShipSocketsConfig>());
            _cannonPrefab = Keep(new GameObject("CannonStation"));
            _cannon = Keep(ShipFittingDefinition.Create(Cannon, _cannonPrefab, "Cannon station"));
            _fittings = new ShipFittingUseCase(_actors, new FakeNetworkService(), _sockets, new ShipFittingCatalog(new[] { _cannon }),
                _spawner, _config, new FakeEventPublisher());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var spawned in _spawner.Spawned) Object.DestroyImmediate(spawned.Instance);
            _ship.Destroy();
            foreach (var o in _objects) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        private T Keep<T>(T o) where T : Object
        {
            _objects.Add(o);
            return o;
        }

        private FakeFittingPlayer Player(Vector3 position, ulong clientId)
        {
            var player = FakeShipSockets.Player(position, clientId);
            Keep(player.gameObject);
            _actors.Register(player);
            return player;
        }

        [Test]
        public void ARequestFromAPlayerNearby_MountsTheFitting_OnTheSocket_AndReplicatesIt()
        {
            Player(new Vector3(1f, 0f, 1.5f), 7);
            _state.Requests.Add(new MountRequest(7, _bow.Id, Cannon));

            _fittings.Tick();

            var spawned = _spawner.Spawned.Single();
            Assert.That(spawned.Prefab, Is.SameAs(_cannonPrefab));
            Assert.That(Vector3.Distance(spawned.Position, _bow.Mount.position), Is.LessThan(0.001f));
            Assert.That(_state.Mounted.Single().Socket, Is.EqualTo(_bow.Id));
            Assert.That(_state.Mounted.Single().FittingId.ToString(), Is.EqualTo(Cannon));
        }

        [Test]
        public void Requests_AreRefused_TooFar_Taken_UnknownFitting_UnknownSocket()
        {
            Player(new Vector3(30f, 0f, 0f), 1);
            Player(new Vector3(1f, 0f, 2f), 2);
            _state.Requests.Add(new MountRequest(1, _bow.Id, Cannon));
            _state.Requests.Add(new MountRequest(2, _bow.Id, "weapon.unknown"));
            _state.Requests.Add(new MountRequest(2, new ShipSocketId(99, 0), Cannon));
            _fittings.Tick();
            Assert.That(_spawner.Spawned, Is.Empty, "too far, unknown fitting, unknown socket");

            _state.Requests.Add(new MountRequest(2, _bow.Id, Cannon));
            _state.Requests.Add(new MountRequest(2, _bow.Id, Cannon));
            _fittings.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(1), "the second asks for a taken socket");
        }

        [Test]
        public void WhenItsPartGoes_TheFittingIsDespawned()
        {
            Assert.That(_fittings.Mount(_ship.Id, _bow.Id, Cannon, out var error), Is.True, error);
            var fixture = _spawner.Spawned.Single().Instance;

            _sockets.Ships[_ship.Id].Clear();
            _fittings.Tick();

            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { fixture }));
            Assert.That(_state.Mounted, Is.Empty);
            Assert.That(_fittings.MountedOn(_ship.Id), Is.Empty);
        }

        [Test]
        public void Unmount_FreesTheSocket()
        {
            _fittings.Mount(_ship.Id, _bow.Id, Cannon, out _);

            Assert.That(_fittings.Unmount(_ship.Id, _bow.Id), Is.True);

            Assert.That(_state.Mounted, Is.Empty);
            Assert.That(_fittings.Mount(_ship.Id, _bow.Id, Cannon, out var error), Is.True, error);
            Assert.That(_fittings.Unmount(_ship.Id, new ShipSocketId(99, 0)), Is.False);
        }

        [Test]
        public void TheCatalog_LeavesOutFittingsWithoutAPrefabOrWithABadId()
        {
            var noPrefab = Keep(ShipFittingDefinition.Create("storage.empty", null));
            var badId = Keep(ShipFittingDefinition.Create("Bad Id!", _cannonPrefab));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("storage.empty needs a prefab"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Bad Id! needs a prefab"));

            var catalog = new ShipFittingCatalog(new[] { _cannon, noPrefab, badId, _cannon });

            Assert.That(catalog.Fittings, Is.EqualTo(new[] { _cannon }));
        }

        [Test]
        public void TheFixtureFilter_KeepsAFittingsFixedPoseFixtureOffTheShip()
        {
            var filter = new ShipSocketsFixtureFilter(new ShipFittingCatalog(new[] { _cannon }));
            var cannonFixture = Keep(ScriptableObject.CreateInstance<ShipFixtureDefinition>());
            cannonFixture.Prefab = _cannonPrefab;
            var otherFixture = Keep(ScriptableObject.CreateInstance<ShipFixtureDefinition>());
            otherFixture.Prefab = Keep(new GameObject("Other"));

            Assert.That(filter.ShouldSpawn(_ship, cannonFixture), Is.False);
            Assert.That(filter.ShouldSpawn(_ship, otherFixture), Is.True);
        }
    }
}
