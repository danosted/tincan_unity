#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.Airship.Damage;
using UnityEngine;
using TinCan.Tests.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Features.Airship.Damage.Tests
{
    /// <summary>ShipDamagePointSpawnUseCase: damage points on a designed ship's floors and beside its fittings.</summary>
    public class ShipDamagePointSpawnUseCaseTests
    {
        /// <summary>Spawns a FakeShipDamagePoint under the ship (as the real service parents a module to its ship).</summary>
        private sealed class PointSpawner : IModuleSpawningService
        {
            public List<GameObject> Spawned { get; } = new();
            public List<GameObject> Despawned { get; } = new();

            public GameObject SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip)
            {
                var point = new GameObject($"Point #{Spawned.Count}");
                point.transform.SetParent(((IAirshipView)parentShip).Transform, true);
                point.transform.SetPositionAndRotation(worldPosition, worldRotation);
                point.AddComponent<FakeShipDamagePoint>();
                Spawned.Add(point);
                return point;
            }

            public void DespawnModule(GameObject module)
            {
                Despawned.Add(module);
                Object.DestroyImmediate(module);
            }
        }

        private sealed class Surfaces : IShipHullSurfaces
        {
            public Dictionary<Guid, List<ShipHullSurface>> Ships { get; } = new();
            public int Version { get; set; } = 1;

            public IReadOnlyList<ShipHullSurface> SurfacesOf(Guid shipId) =>
                Ships.TryGetValue(shipId, out var surfaces) ? surfaces : (IReadOnlyList<ShipHullSurface>)Array.Empty<ShipHullSurface>();
        }

        private sealed class Fittings : IMountedFittings
        {
            public List<MountedFittingInfo> Mounted { get; } = new();
            public IReadOnlyList<MountedFittingInfo> FittingsOn(Guid shipId) => Mounted;
        }

        private readonly List<Object> _objects = new();
        private readonly List<FakeAirshipView> _ships = new();
        private FakeActorRegistry _actors = null!;
        private PointSpawner _spawner = null!;
        private Surfaces _surfaces = null!;
        private Fittings _fittings = null!;
        private ShipDamageConfig _config = null!;
        private FakeAirshipView _ship = null!;
        private ShipDamagePointSpawnUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _spawner = new PointSpawner();
            _surfaces = new Surfaces();
            _fittings = new Fittings();
            _config = Track(ScriptableObject.CreateInstance<ShipDamageConfig>());
            _config.DesignedShipPointPrefab = Track(new GameObject("PointPrefab"));
            _config.DesignedShipPoints = 2;
            _config.FittingPointHeight = 1f;
            _ship = Ship("Designed");
            _surfaces.Ships[_ship.Id] = new List<ShipHullSurface>
            {
                new(1, 0, false, new Vector3(0f, 0f, 2f), Vector3.up, 10f),
                new(2, 1, false, new Vector3(0f, 3f, -2f), Vector3.up, 10f),
                new(3, 0, true, Vector3.zero, Vector3.right, 10f),
            };
            _useCase = Create(new FakeSessionNetworkService { IsServer = true, IsClient = true });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ship in _ships) ship.Destroy();
            _ships.Clear();
            foreach (var instance in _objects) if (instance != null) Object.DestroyImmediate(instance);
            _objects.Clear();
        }

        [Test]
        public void DesignedShip_GetsHullPointsOnItsFloors_TopStoryFirst_IndexedFromZero()
        {
            _useCase.Tick();

            var points = Points();
            Assert.That(points.Count, Is.EqualTo(2));
            Assert.That(points.Select(p => p.Index), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(points[0].Transform!.localPosition.y, Is.EqualTo(3.05f).Within(0.001f), "story 1 first, lifted off the planks");
            Assert.That(points.All(p => p.Socket == null), Is.True);

            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(2), "spawned once");
        }

        [Test]
        public void EveryMountedFitting_GetsAPointAboveIt_AfterTheHullSpots_AndLosesItWhenUnmounted()
        {
            var cannon = new ShipSocketId(20, 0);
            var rack = new ShipSocketId(10, 1);
            _fittings.Mounted.Add(new MountedFittingInfo(cannon, "weapon.cannon", Mount(new Vector3(1f, 0f, 0f))));
            _fittings.Mounted.Add(new MountedFittingInfo(rack, "storage.tool_rack", Mount(new Vector3(-1f, 0f, 0f))));

            _useCase.Tick();

            var byIndex = Points().ToDictionary(p => p.Index);
            Assert.That(byIndex.Count, Is.EqualTo(4));
            Assert.That(byIndex[2].Socket, Is.EqualTo(rack), "fittings follow the hull spots, in socket order");
            Assert.That(byIndex[3].Socket, Is.EqualTo(cannon));
            Assert.That(byIndex[3].Transform!.localPosition, Is.EqualTo(new Vector3(1f, 1f, 0f)));

            _fittings.Mounted.RemoveAt(1);
            _useCase.Tick();

            Assert.That(_spawner.Despawned.Count, Is.EqualTo(1));
            Assert.That(Points().Count, Is.EqualTo(3));
            Assert.That(Points().Single(p => p.Socket != null).Index, Is.EqualTo(2), "indexes close up");
        }

        [Test]
        public void Rebuild_RepicksHullSpots_OnlyWhenTheyMoved()
        {
            _useCase.Tick();
            _surfaces.Version++;
            _useCase.Tick();
            Assert.That(_spawner.Despawned, Is.Empty, "same spots: kept");

            _surfaces.Ships[_ship.Id][0] = new ShipHullSurface(1, 0, false, new Vector3(5f, 0f, 2f), Vector3.up, 10f);
            _surfaces.Version++;
            _useCase.Tick();
            Assert.That(_spawner.Despawned.Count, Is.EqualTo(2));
            Assert.That(Points().Count, Is.EqualTo(2));
        }

        [Test]
        public void ShipWithFixedPoints_IsLeftAlone()
        {
            FakeShipDamage.AttachPoints(_ship.GameObject, 1, Track(ScriptableObject.CreateInstance<HealthAttribute>()),
                Track(ScriptableObject.CreateInstance<MaxHealthAttribute>()));

            _useCase.Tick();
            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void ShipNotBuiltFromADesign_OrOnAClient_GetsNothing()
        {
            _surfaces.Ships.Clear();
            _useCase.Tick();
            Assert.That(_spawner.Spawned, Is.Empty, "no surfaces: not a designed ship");

            _surfaces.Ships[_ship.Id] = new List<ShipHullSurface> { new(1, 0, false, Vector3.zero, Vector3.up, 1f) };
            Create(new FakeSessionNetworkService { IsServer = false, IsClient = true }).Tick();
            Assert.That(_spawner.Spawned, Is.Empty, "server only");
        }

        private ShipDamagePointSpawnUseCase Create(INetworkService network) => new(network, _actors, _spawner,
            new ShipDamageSpotProcessor(), _config, new FakeEventPublisher(), new IShipHullSurfaces[] { _surfaces }, new IMountedFittings[] { _fittings });

        private IReadOnlyList<IShipDamagePoint> Points() => ShipDamageLocator.FindPoints(_ship);

        private FakeAirshipView Ship(string name)
        {
            var ship = new FakeAirshipView(name);
            _ships.Add(ship);
            _actors.Register(ship);
            return ship;
        }

        private Transform Mount(Vector3 local)
        {
            var mount = new GameObject("Mount").transform;
            mount.SetParent(_ship.Transform, false);
            mount.localPosition = local;
            return mount;
        }

        private T Track<T>(T instance) where T : Object
        {
            _objects.Add(instance);
            return instance;
        }
    }
}
