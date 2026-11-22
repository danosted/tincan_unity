#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Core.Ship.Sockets;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Camping: gather firewood (from deadwood, such as an island tree's canopy), tie a log into a campfire bundle, set
    /// the bundle down as a campfire where it fits (a ghost shows where) and keep it burning with logs; set a tent roll
    /// down as a pitched tent, and pack it up again. The ship's hearth burns the same way, and its basket hands out tent
    /// rolls. Brings the items (firewood, the bundle, the tent roll), the placed campfire and tent, and the hearth
    /// fitting for a ship's socket. Plans: .docs/plans/peaceful-mode.md, .docs/plans/island-life.md.
    /// </summary>
    [CreateAssetMenu(fileName = "CampingFeatureInstaller", menuName = "TinCan/Features/Camping Feature Installer")]
    public class CampingFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ItemDefinition>,
        FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [Tooltip("The camping tunables, with the placed campfire and tent prefabs and their previews.")]
        [SerializeField] private CampingConfig? _config;
        [Tooltip("The items this feature owns (firewood, the campfire bundle, the tent roll).")]
        [SerializeField] private List<ItemDefinition> _items = new();
        [Tooltip("Socket fittings: the hearth.")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<GatherFirewoodInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<StokeFireInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<PitchTentInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();

            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No CampingConfig assigned; nothing can be bundled or placed.", this);
                return;
            }

            var config = _config;
            builder.RegisterInstance(config);
            builder.Register<PhysicsCampPlacementQuery>(Lifetime.Singleton).As<ICampPlacementQuery>();
            builder.Register<CampPlacementFinder>(Lifetime.Singleton);
            builder.Register<ICampSpawner>(
                resolver => new CampSpawningService(resolver.Resolve<NetworkManager>(), resolver, config), Lifetime.Singleton);
            builder.Register<CampPlacementUseCase>(Lifetime.Singleton).AsSelf().As<IHumanoidMotionContributor>().As<ISimulationTickable>();
            builder.Register<CampPlacementPreviewPresenter>(Lifetime.Singleton).AsSelf().As<ITickable>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_config != null && _config.CampfirePrefab != null) yield return _config.CampfirePrefab;
                if (_config != null && _config.TentPrefab != null) yield return _config.TentPrefab;
                foreach (var fitting in _fittings)
                {
                    if (fitting != null && fitting.Prefab != null) yield return fitting.Prefab;
                }
            }
        }

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;
    }
}
