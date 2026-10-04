#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipAssemblyUseCase"/>: every peer builds a ship from its replicated design; the server spawns the
    /// functional parts once; design changes rebuild only what changed.
    /// </summary>
    public class ShipAssemblyUseCaseTests
    {
        private sealed class RecordingModuleSpawner : IModuleSpawningService
        {
            public List<(GameObject Prefab, Vector3 Position, Quaternion Rotation, IActor Ship)> Calls { get; } = new();

            public void SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip) =>
                Calls.Add((prefab, worldPosition, worldRotation, parentShip));
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
            public void SetListenEndpoint(string listenAddress, ushort port) { }
            public void StartHost() { }
            public void StartServer() { }
            public void StartClient() { }
            public void Shutdown() { }
        }

        private ShipDesignTestParts _parts = null!;
        private ShipDesignsConfig _config = null!;
        private FakeActorRegistry _actors = null!;
        private FakeShipHullBuilder _builder = null!;
        private RecordingModuleSpawner _spawner = null!;
        private FakeAirshipView _ship = null!;
        private FakeShipDesignState _state = null!;

        [SetUp]
        public void SetUp()
        {
            _parts = new ShipDesignTestParts();
            _config = ScriptableObject.CreateInstance<ShipDesignsConfig>();
            _actors = new FakeActorRegistry();
            _builder = new FakeShipHullBuilder();
            _spawner = new RecordingModuleSpawner();
            _ship = new FakeAirshipView("Ship");
            _ship.Transform.SetPositionAndRotation(new Vector3(0f, 40f, 0f), Quaternion.Euler(0f, 90f, 0f));
            _state = new FakeShipDesignState(_ship);
            _actors.Register(_ship);
            _actors.Register(_state);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            _parts.Dispose();
            Object.DestroyImmediate(_config);
        }

        private ShipAssemblyUseCase Assembly(INetworkService? network = null) =>
            new(_actors, network ?? new FakeNetworkService(), _parts.Catalog, _config, _builder, _spawner, new FakeEventPublisher());

        private static ShipDesign Ship(params (string Part, int X, int Y, int Z, byte Rot)[] extra) =>
            Design(new[] { (Helm, 0, 0, 0, (byte)0), (Beam, -1, -1, 0, (byte)0) }.Concat(extra).ToArray());

        [Test]
        public void NoDesignYet_BuildsNothing()
        {
            var assembly = Assembly();

            assembly.Tick();

            Assert.That(_builder.Builds, Is.Zero);
            Assert.That(assembly.TryGetBuilt(_ship.Id, out _, out _), Is.False);
        }

        [Test]
        public void ADesign_BuildsItsStructure_AtTheirCells_AndFitsTheVolume()
        {
            var assembly = Assembly();
            _state.Set(Ship((Block, 0, -1, 1, 1)));

            assembly.Tick();

            Assert.That(_builder.Standing.Select(b => b.Visual), Is.EquivalentTo(new[] { _parts.BeamVisual, _parts.BlockVisual }));
            var block = _builder.Standing.Single(b => b.Visual == _parts.BlockVisual);
            Assert.That(block.Position, Is.EqualTo(new Vector3(0f, -1f, 1f)));
            Assert.That(Quaternion.Angle(block.Rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
            Assert.That(_builder.Volume!.Value.min, Is.EqualTo(new Vector3(-1.5f, -1.5f, -0.5f)));
            Assert.That(_builder.Volume!.Value.max, Is.EqualTo(new Vector3(1.5f, 1.5f, 1.5f)));
            Assert.That(assembly.TryGetBuilt(_ship.Id, out var hash, out var count), Is.True);
            Assert.That((hash, count), Is.EqualTo((_state.DesignHash, 3)));
        }

        [Test]
        public void TheServer_SpawnsTheHelm_OnceAtItsDesignPose()
        {
            var assembly = Assembly();
            _state.Set(Ship());

            assembly.Tick();
            assembly.Tick();

            var helm = _spawner.Calls.Single();
            Assert.That(helm.Prefab, Is.SameAs(_parts.HelmPrefab));
            Assert.That(helm.Ship, Is.SameAs(_ship));
            // Cell (0,0,0) with the pivot half a cell down, on a ship at (0, 40, 0).
            Assert.That(Vector3.Distance(helm.Position, new Vector3(0f, 39.5f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void AClient_BuildsTheStructure_ButSpawnsNothing()
        {
            var assembly = Assembly(new ClientNetwork());
            _state.Set(Ship());

            assembly.Tick();

            Assert.That(_builder.Builds, Is.EqualTo(1));
            Assert.That(_spawner.Calls, Is.Empty);
        }

        [Test]
        public void AChangedDesign_RebuildsOnlyWhatChanged()
        {
            var assembly = Assembly();
            var first = Ship((Block, 0, -1, 1, 0), (Block, 0, -1, -1, 0));
            _state.Set(first);
            assembly.Tick();
            int builds = _builder.Builds;

            // Part 3 turns, part 4 goes, a new part 5 comes.
            var second = first.WithParts(new[]
            {
                first.Parts[0], first.Parts[1],
                new ShipPartPlacement(3, Block, new ShipGridCell(0, -1, 1), 2),
                new ShipPartPlacement(5, Block, new ShipGridCell(2, -1, 1), 0),
            }, 6);
            _state.Set(second);
            assembly.Tick();

            Assert.That(_builder.Builds - builds, Is.EqualTo(2), "part 3 again and part 5");
            Assert.That(_builder.Removals, Is.EqualTo(2), "part 3 as it was and part 4");
            Assert.That(_builder.Standing.Count, Is.EqualTo(3));
            Assert.That(_spawner.Calls.Count, Is.EqualTo(1), "the helm stays");
        }

        [Test]
        public void UnknownParts_AreSkipped()
        {
            var assembly = Assembly();
            _state.Set(Ship(("hull.from_the_future", 0, -1, 1, 0)));

            assembly.Tick();

            Assert.That(_builder.Builds, Is.EqualTo(1), "the beam only");
        }

        [Test]
        public void AnUnreadableDesign_BuildsNothing()
        {
            var assembly = Assembly();
            _state.ServerSetDesign(42, new byte[] { 1, 2, 3 });

            assembly.Tick();

            Assert.That(_builder.Builds, Is.Zero);
        }

        [Test]
        public void AShipThatGoes_HasItsHullRemoved()
        {
            var assembly = Assembly();
            _state.Set(Ship());
            assembly.Tick();

            _actors.Unregister(_state);
            assembly.Tick();

            Assert.That(_builder.Standing, Is.Empty);
            Assert.That(assembly.TryGetBuilt(_ship.Id, out _, out _), Is.False);
        }
    }
}
