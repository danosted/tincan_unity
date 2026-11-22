#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// Island details: trees in groves, boulders and bushes on the sky islands (SkyIslands), clear of their clearings and
    /// caves. Boulders and bushes are scenery every peer builds for itself; trees are networked, spawned by the server,
    /// and give firewood through Camping's deadwood (their prefabs carry a <c>DeadwoodNetworkMediator</c>, so load
    /// Camping for them to be gathered from). Plan: .docs/plans/island-life.md.
    /// </summary>
    [CreateAssetMenu(fileName = "IslandDetailsFeatureInstaller", menuName = "TinCan/Features/Island Details Feature Installer")]
    public class IslandDetailsFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private IslandDetailsConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No IslandDetailsConfig assigned; islands stay bare.", this);
                return;
            }

            var config = _config;
            builder.RegisterInstance(config);
            builder.Register<IslandDetailPlacementProcessor>(Lifetime.Transient);
            builder.Register<IslandSceneryBuilder>(Lifetime.Singleton).As<IIslandSceneryBuilder>();
            builder.Register<IslandSceneryUseCase>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<IIslandTreeSpawner>(
                resolver => new NetworkIslandTreeSpawner(resolver.Resolve<NetworkManager>(), resolver, config.TreePrefabs),
                Lifetime.Singleton);
            builder.Register<IslandTreesUseCase>(Lifetime.Singleton).AsSelf().As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_config == null) yield break;
                foreach (var tree in _config.TreePrefabs)
                {
                    if (tree != null) yield return tree;
                }
            }
        }
    }
}
