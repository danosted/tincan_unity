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
            public List<GameObject> Despawned { get; } = new();

            /// <summary>Each spawn returns a new stand-in object, so a despawn names which spawn it undid.</summary>
            public List<GameObject> Modules { get; } = new();

            public GameObject SpawnModule(GameObject prefab, Vector3 worldPosition, Quaternion worldRotation, IActor parentShip)
            {
                Calls.Add((prefab, worldPosition, worldRotation, parentShip));
                var module = new GameObject($"{prefab.name} #{Calls.Count}");
                Modules.Add(module);
                return module;
            }

            public void DespawnModule(GameObject module) => Despawned.Add(module);
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
            foreach (var module in _spawner.Modules) Object.DestroyImmediate(module);
            _ship.Destroy();
            _parts.Dispose();
            Object.DestroyImmediate(_config);
        }

        private ShipAssemblyUseCase Assembly(INetworkService? network = null) =>
            new(_actors, network ?? new FakeNetworkService(), _parts.Catalog, _config, _builder, _spawner, new FakeEventPublisher());

        /// <summary>The helm (part 10) on the test floor's middle and a beam (part 11) from x 3 m, then the extra parts (ids from 12).</summary>
        private static ShipDesign Ship(params (string Part, int X, int Z, byte Rot)[] extra) =>
            Design(new[] { (Helm, 0, 0, (byte)0), (Beam, 3, 0, (byte)0) }.Concat(extra).ToArray());

        private static ShipPartAnchor At(int x, int z) => ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, x * 10, z * 10);

        [Test]
        public void NoDesignYet_BuildsNothing()
        {
            var assembly = Assembly();

            assembly.Tick();

            Assert.That(_builder.Builds, Is.Zero);
            Assert.That(assembly.TryGetBuilt(_ship.Id, out _, out _), Is.False);
        }

        [Test]
        public void ADesign_BuildsItsParts_StandingOnTheirFloor_AndFitsTheVolume()
        {
            var assembly = Assembly();
            _state.Set(Ship((Block, 0, 2, 1)));

            assembly.Tick();

            Assert.That(_builder.Standing.Select(b => b.Visual), Is.EquivalentTo(new[] { _parts.BeamVisual, _parts.BlockVisual }));
            var block = _builder.Standing.Single(b => b.Visual == _parts.BlockVisual);
            Assert.That(block.Position, Is.EqualTo(new Vector3(0f, -0.5f, 2f)), "on the floor at -0.5 m");
            Assert.That(Quaternion.Angle(block.Rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
            Assert.That(_builder.Volume!.Value.min.x, Is.EqualTo(-10f).Within(1e-4f), "the floor's width");
            Assert.That(_builder.Volume!.Value.max.y, Is.EqualTo(1.5f).Within(1e-4f), "the helm's two blocks");
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
            // On the floor at -0.5 m, on a ship at (0, 40, 0).
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
            var first = Ship((Block, 0, 2, 0), (Block, 0, -2, 0));
            _state.Set(first);
            assembly.Tick();
            int builds = _builder.Builds;

            // Part 12 turns, part 13 goes, a new part 14 comes.
            var second = first.WithParts(new[]
            {
                first.Parts[0], first.Parts[1],
                new ShipPartPlacement(12, Block, At(0, 2), 2),
                new ShipPartPlacement(14, Block, At(-2, 2), 0),
            }, 15);
            _state.Set(second);
            assembly.Tick();

            Assert.That(_builder.Builds - builds, Is.EqualTo(2), "part 12 again and part 14");
            Assert.That(_builder.Removals, Is.EqualTo(2), "part 12 as it was and part 13");
            Assert.That(_builder.Standing.Count, Is.EqualTo(3));
            Assert.That(_spawner.Calls.Count, Is.EqualTo(1), "the helm stays");
            Assert.That(_spawner.Despawned, Is.Empty);
        }

        [Test]
        public void AFloorThatMoves_MovesThePartsOnIt()
        {
            var assembly = Assembly();
            var deck = ShipFrameTestDesigns.Deck();
            var withBlock = deck.WithParts(deck.Parts.Append(new ShipPartPlacement(11, Block, ShipPartAnchor.OnFace(9, -15, -15), 0)), 12);
            _state.Set(withBlock);
            assembly.Tick();
            int removals = _builder.Removals;

            // Corner 1 (-2, -2 m) rises 0.5 m: the floor under the block rises with it.
            var raised = withBlock.WithFrame(withBlock.Frame.With(nodes: withBlock.Frame.Nodes.Select(n => n.Id == 1 ? n.MovedTo(n.X, 5, n.Z) : n)),
                withBlock.NextPartInstanceId);
            _state.Set(raised);
            assembly.Tick();

            var block = _builder.Standing.Single(b => b.Visual == _parts.BlockVisual);
            Assert.That(block.Position.y, Is.GreaterThan(-0.5f + 0.1f), "it stands on the risen floor");
            Assert.That(_builder.Removals - removals, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void AnotherDesign_ReplacesTheHelm_WhereTheNewDesignHasIt()
        {
            // The shipyard's Launch onto a built ship: the old helm (part 10 in the middle) must not stay behind.
            var assembly = Assembly();
            _state.Set(Ship());
            assembly.Tick();
            var oldHelm = _spawner.Modules.Single();

            _state.Set(Design((Beam, 0, 0, 0), (Beam, 0, 2, 0), (Helm, 0, 4, 0)));
            assembly.Tick();

            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { oldHelm }));
            Assert.That(_spawner.Calls.Count, Is.EqualTo(2));
            // At z 4 m on the floor, on a ship at (0, 40, 0) turned 90° (local +Z is world +X).
            Assert.That(Vector3.Distance(_spawner.Calls[1].Position, new Vector3(4f, 39.5f, 0f)), Is.LessThan(0.001f));
            Assert.That(assembly.TryGetBuilt(_ship.Id, out _, out var count), Is.True);
            Assert.That(count, Is.EqualTo(3), "two beams and one helm");
        }

        [Test]
        public void AHelmThatMoves_IsRespawnedAtItsNewPose_AndOneThatGoes_IsDespawned()
        {
            var assembly = Assembly();
            var first = Ship();
            _state.Set(first);
            assembly.Tick();

            // Same instance id, another spot.
            _state.Set(first.WithParts(new[] { new ShipPartPlacement(10, Helm, At(-2, 0), 0), first.Parts[1] }, 12));
            assembly.Tick();
            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { _spawner.Modules[0] }));
            Assert.That(_spawner.Calls.Count, Is.EqualTo(2));

            // No helm at all: the moved one goes too.
            _state.Set(first.WithParts(new[] { first.Parts[1] }, 12));
            assembly.Tick();
            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { _spawner.Modules[0], _spawner.Modules[1] }));
            Assert.That(_spawner.Calls.Count, Is.EqualTo(2));
        }

        [Test]
        public void UnknownParts_AndUnanchoredParts_AreSkipped()
        {
            var assembly = Assembly();
            var design = Ship(("hull.from_the_future", 0, 2, 0));
            _state.Set(design.WithParts(design.Parts.Append(new ShipPartPlacement(13, Block, ShipPartAnchor.OnFace(77, 0, 0), 0)), 14));

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
            Assert.That(_builder.Meshes, Is.Empty);
            Assert.That(assembly.TryGetBuilt(_ship.Id, out _, out _), Is.False);
        }

        [Test]
        public void TheSocketsOfBuiltParts_AreTheShipsSockets()
        {
            var mountVisual = new GameObject("MountVisual");
            var marker = new GameObject("ShipSocket");
            marker.transform.SetParent(mountVisual.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            marker.AddComponent<TinCan.Core.Ship.Sockets.ShipSocket>();
            var mount = TinCan.Core.Ship.Parts.ShipPartDefinition.Create("hull.mount", false, mountVisual, null, Vector3.zero);
            try
            {
                var catalog = new ShipPartCatalog(_parts.Catalog.Parts.Append(mount));
                var assembly = new ShipAssemblyUseCase(_actors, new FakeNetworkService(), catalog, _config, new ShipHullBuilder(), _spawner,
                    new FakeEventPublisher());
                int version = assembly.Version;
                _state.Set(Ship(("hull.mount", -3, 0, 0)));

                assembly.Tick();

                var socket = assembly.SocketsOf(_ship.Id).Single();
                Assert.That(socket.Id, Is.EqualTo(new TinCan.Core.Ship.Sockets.ShipSocketId(12, 0)), "part #12, its first socket");
                Assert.That(socket.Mount.IsChildOf(_ship.Transform), Is.True);
                Assert.That(Vector3.Distance(socket.Mount.localPosition, new Vector3(0f, 0.5f, 0f)), Is.LessThan(0.001f));
                Assert.That(assembly.Version, Is.GreaterThan(version));
                Assert.That(assembly.SocketsOf(System.Guid.NewGuid()), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(mount);
                Object.DestroyImmediate(mountVisual);
            }
        }

        [Test]
        public void AFrame_BuildsPlanksAndBeams_AndOnlyAChangedStoryIsRebuilt()
        {
            var assembly = Assembly();
            var deck = ShipFrameTestDesigns.Deck();
            _state.Set(deck);
            assembly.Tick();

            Assert.That(_builder.Meshes.Select(m => m.Name), Is.EquivalentTo(new[] { "Planks0", "Beams0" }));
            Assert.That(assembly.BuiltFrameMeshes(_ship.Id), Is.EqualTo(2));
            Assert.That(_builder.Volume!.Value.min.x, Is.EqualTo(-2f).Within(1e-4f), "the volume covers the deck");
            Assert.That(_builder.Volume!.Value.min.y, Is.LessThan(-0.5f - 0.15f), "and its underside");

            // A new part, frame unchanged: no mesh is rebuilt.
            _state.Set(deck.WithParts(deck.Parts.Append(new ShipPartPlacement(11, Block, ShipPartAnchor.OnFace(9, 10, 10), 0)), 12));
            assembly.Tick();
            Assert.That(_builder.MeshBuilds, Is.EqualTo(2));

            // The deck goes open: the story is rebuilt, beams only.
            _state.Set(ShipFrameTestDesigns.Deck(ShipFrameFill.Open));
            assembly.Tick();
            Assert.That(_builder.Meshes.Select(m => m.Name), Is.EqualTo(new[] { "Beams0" }));

            // No frame at all: nothing generated stands.
            _state.Set(ShipDesign.Empty("Bare"));
            assembly.Tick();
            Assert.That(_builder.Meshes, Is.Empty);
            Assert.That(assembly.BuiltFrameMeshes(_ship.Id), Is.Zero);
        }

        [Test]
        public void Stairs_BuildSeenSteps_AndACollidingRamp()
        {
            var assembly = Assembly();
            var box = ShipFrameTestDesigns.FrameOnly(ShipFrameTestDesigns.StairWell());
            _state.Set(box);
            assembly.Tick();

            Assert.That(_builder.Meshes.Single(m => m.Name == "Steps0").Use, Is.EqualTo(ShipFrameMeshUse.Seen));
            Assert.That(_builder.Meshes.Single(m => m.Name == "Ramps0").Use, Is.EqualTo(ShipFrameMeshUse.Collider));
        }
    }
}
