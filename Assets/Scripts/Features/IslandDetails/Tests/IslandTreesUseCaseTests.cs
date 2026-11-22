#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Features.IslandDetails.Tests.Fakes;
using TinCan.Features.SkyIslands;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.IslandDetails.Tests
{
    /// <summary><see cref="IslandTreesUseCase"/>: the server plants each main island's trees, a few per check, and fells them with it.</summary>
    public class IslandTreesUseCaseTests
    {
        private sealed class ClientNetwork : INetworkService
        {
            public NetworkState State => NetworkState.Offline;
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

        private FakeSkyIslands _islands = null!;
        private FakeIslandTreeSpawner _spawner = null!;
        private FakeTimeService _time = null!;
        private IslandDetailsConfig _config = null!;
        private SkyIslandConfig _islandConfig = null!;

        [SetUp]
        public void SetUp()
        {
            _islands = new FakeSkyIslands();
            _spawner = new FakeIslandTreeSpawner();
            _time = new FakeTimeService();
            _config = IslandDetailsTestKit.Config();
            _config.MaxTreeSpawnsPerCheck = 1000;
            _islandConfig = ScriptableObject.CreateInstance<SkyIslandConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_islandConfig);
        }

        private IslandTreesUseCase Create(INetworkService? network = null) =>
            new(network ?? new FakeNetworkService(), _time, _islands, _spawner, new IslandDetailPlacementProcessor(),
                new SkyIslandSiteProcessor(), _islandConfig, _config);

        [Test]
        public void AMainIsland_GetsItsTrees_WhereThePlacementPutsThem()
        {
            var island = IslandDetailsTestKit.Main(4);
            _islands.List.Add(island);

            Create().Tick();

            var expected = Expected(island);
            Assert.That(expected, Is.Not.Empty);
            Assert.That(_spawner.Spawned.Values.Select(t => t.Position), Is.EqualTo(expected.Select(d => island.Top + d.Position)));
            Assert.That(_spawner.Spawned.Values.Select(t => t.Variant), Is.EqualTo(expected.Select(d => d.Variant)));
            Assert.That(_spawner.Spawned.Values.Select(t => t.Scale), Is.EqualTo(expected.Select(d => d.Scale)));
        }

        [Test]
        public void ASatellite_GetsNoTrees()
        {
            _islands.List.Add(IslandDetailsTestKit.Satellite(2));
            var trees = Create();

            trees.Tick();

            Assert.That(_spawner.Spawned, Is.Empty);
            Assert.That(trees.Planted, Is.Empty);
        }

        [Test]
        public void TreesAreSpawned_AFewPerCheck()
        {
            _config.MaxTreeSpawnsPerCheck = 3;
            var island = IslandDetailsTestKit.Main(4);
            _islands.List.Add(island);
            int all = Expected(island).Count;
            var trees = Create();

            trees.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(3));

            trees.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(3), "waits for the next check");

            for (int check = 1; check <= all; check++)
            {
                _time.Time = check * (_config.CheckIntervalSeconds + 0.01f);
                trees.Tick();
            }
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(all));
            Assert.That(trees.TreesOn(island.Id), Has.Count.EqualTo(all));
        }

        [Test]
        public void AnIslandThatStreamsOut_LosesItsTrees()
        {
            var island = IslandDetailsTestKit.Main(4);
            _islands.List.Add(island);
            var trees = Create();
            trees.Tick();
            var planted = _spawner.Spawned.Keys.ToList();

            _islands.List.Clear();
            _time.Time = 1f;
            trees.Tick();

            Assert.That(_spawner.Despawned, Is.EquivalentTo(planted));
            Assert.That(trees.Planted, Is.Empty);
        }

        [Test]
        public void AClient_PlantsNothing()
        {
            _islands.List.Add(IslandDetailsTestKit.Main(4));

            Create(new ClientNetwork()).Tick();

            Assert.That(_spawner.Spawned, Is.Empty);
        }

        private List<IslandDetail> Expected(in SkyIslandSpec island)
        {
            var sites = new SkyIslandSites();
            new SkyIslandSiteProcessor().Sites(island, _islandConfig.SiteRules, sites);
            var details = new List<IslandDetail>();
            new IslandDetailPlacementProcessor().Place(island, sites, _config.Rules(_islandConfig.ClearingRadius), details);
            return details.Where(d => d.Kind == IslandDetailKind.Tree).ToList();
        }
    }
}
