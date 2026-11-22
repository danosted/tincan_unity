#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.SkyIslands;
using VContainer.Unity;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// Application Layer, server: the trees and crystal veins of every main island and landmass that stands. Each is a
    /// network object (it holds firewood or crystals every peer sees taken), spawned where the placement puts it (<see cref="IslandDetailPlacementProcessor"/>),
    /// a few per check so a landmass streaming in costs no spike, and despawned when its island streams out. A tree's
    /// stock lasts as long as its island stands: it is not saved yet (plan .docs/plans/peaceful-mode.md, the world save).
    /// </summary>
    public class IslandTreesUseCase : ITickable
    {
        private readonly INetworkService _network;
        private readonly ITimeService _time;
        private readonly ISkyIslands _islands;
        private readonly IIslandTreeSpawner _spawner;
        private readonly IslandDetailPlacementProcessor _placement;
        private readonly SkyIslandSiteProcessor _siteProcessor;
        private readonly SkyIslandConfig _islandConfig;
        private readonly IslandDetailsConfig _config;

        private readonly Dictionary<SkyIslandId, Grove> _groves = new();
        private readonly HashSet<SkyIslandId> _standing = new();
        private readonly List<SkyIslandId> _leaving = new();
        private readonly SkyIslandSites _sites = new();
        private readonly List<IslandDetail> _details = new();
        private float _nextCheck;

        public IslandTreesUseCase(INetworkService network, ITimeService time, ISkyIslands islands, IIslandTreeSpawner spawner,
            IslandDetailPlacementProcessor placement, SkyIslandSiteProcessor siteProcessor, SkyIslandConfig islandConfig,
            IslandDetailsConfig config)
        {
            _network = network;
            _time = time;
            _islands = islands;
            _spawner = spawner;
            _placement = placement;
            _siteProcessor = siteProcessor;
            _islandConfig = islandConfig;
            _config = config;
        }

        /// <summary>The islands with trees planted, or waiting to be.</summary>
        public IReadOnlyCollection<SkyIslandId> Planted => _groves.Keys;

        /// <summary>Trees spawned on this island so far (empty when it has none).</summary>
        public IReadOnlyList<ulong> TreesOn(SkyIslandId island) =>
            _groves.TryGetValue(island, out var grove) ? grove.Trees : System.Array.Empty<ulong>();

        public void Tick()
        {
            if (!_network.IsServer)
            {
                // A session ended: NGO took the trees with it.
                _groves.Clear();
                return;
            }

            if (_time.Time < _nextCheck) return;
            _nextCheck = _time.Time + _config.CheckIntervalSeconds;

            _standing.Clear();
            foreach (var island in _islands.Standing)
            {
                if (island.IsSatellite) continue;
                _standing.Add(island.Id);
                if (!_groves.ContainsKey(island.Id)) _groves[island.Id] = Plan(island);
            }

            _leaving.Clear();
            foreach (var id in _groves.Keys)
            {
                if (!_standing.Contains(id)) _leaving.Add(id);
            }
            foreach (var id in _leaving)
            {
                foreach (ulong tree in _groves[id].Trees) _spawner.Despawn(tree);
                _groves.Remove(id);
            }

            int budget = System.Math.Max(1, _config.MaxTreeSpawnsPerCheck);
            foreach (var grove in _groves.Values)
            {
                while (budget > 0 && grove.Waiting.Count > 0)
                {
                    var tree = grove.Waiting.Dequeue();
                    budget--;
                    if (_spawner.Spawn(tree.Kind, grove.Top + tree.Position, tree.Rotation, tree.Scale, tree.Variant) is { } id) grove.Trees.Add(id);
                }
                if (budget <= 0) break;
            }
        }

        private Grove Plan(in SkyIslandSpec island)
        {
            _siteProcessor.Sites(island, _islandConfig.SiteRules, _sites);
            _placement.Place(island, _sites, _config.Rules(_islandConfig.ClearingRadius), _details);
            var grove = new Grove(island.Top);
            foreach (var detail in _details)
            {
                if (detail.Kind is IslandDetailKind.Tree or IslandDetailKind.Vein) grove.Waiting.Enqueue(detail);
            }
            return grove;
        }

        private sealed class Grove
        {
            public readonly UnityEngine.Vector3 Top;
            public readonly Queue<IslandDetail> Waiting = new();
            public readonly List<ulong> Trees = new();

            public Grove(UnityEngine.Vector3 top) => Top = top;
        }
    }
}
