#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Features.SkyIslands;
using VContainer.Unity;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// Application Layer, every peer, every frame: the boulders and bushes of every island standing on this peer. Each
    /// peer builds its own from the same placement (<see cref="IslandDetailPlacementProcessor"/>), so nothing is replicated.
    /// An island that streams out loses its scenery at once; new islands get theirs a few per frame.
    /// </summary>
    public class IslandSceneryUseCase : ITickable, IDisposable
    {
        private readonly ISkyIslands _islands;
        private readonly IIslandSceneryBuilder _builder;
        private readonly IslandDetailPlacementProcessor _placement;
        private readonly SkyIslandSiteProcessor _siteProcessor;
        private readonly SkyIslandConfig _islandConfig;
        private readonly IslandDetailsConfig _config;

        private readonly Dictionary<SkyIslandId, object> _built = new();
        private readonly HashSet<SkyIslandId> _standing = new();
        private readonly List<SkyIslandId> _leaving = new();
        private readonly SkyIslandSites _sites = new();
        private readonly List<IslandDetail> _details = new();

        public IslandSceneryUseCase(ISkyIslands islands, IIslandSceneryBuilder builder, IslandDetailPlacementProcessor placement,
            SkyIslandSiteProcessor siteProcessor, SkyIslandConfig islandConfig, IslandDetailsConfig config)
        {
            _islands = islands;
            _builder = builder;
            _placement = placement;
            _siteProcessor = siteProcessor;
            _islandConfig = islandConfig;
            _config = config;
        }

        /// <summary>The islands whose scenery stands on this peer.</summary>
        public IReadOnlyCollection<SkyIslandId> Built => _built.Keys;

        public void Tick()
        {
            _standing.Clear();
            foreach (var island in _islands.Standing) _standing.Add(island.Id);

            _leaving.Clear();
            foreach (var id in _built.Keys)
            {
                if (!_standing.Contains(id)) _leaving.Add(id);
            }
            foreach (var id in _leaving)
            {
                _builder.Remove(_built[id]);
                _built.Remove(id);
            }

            int budget = Math.Max(1, _config.MaxSceneryBuildsPerFrame);
            foreach (var island in _islands.Standing)
            {
                if (budget <= 0) break;
                if (_built.ContainsKey(island.Id)) continue;

                _siteProcessor.Sites(island, _islandConfig.SiteRules, _sites);
                _placement.Place(island, _sites, _config.Rules(_islandConfig.ClearingRadius), _details);
                _built[island.Id] = _builder.Build(island, _details);
                budget--;
            }
        }

        public void Dispose()
        {
            _built.Clear();
            _builder.Clear();
        }
    }
}
