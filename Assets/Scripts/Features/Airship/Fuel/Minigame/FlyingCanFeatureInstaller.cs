#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Flying jerry cans and the handheld net. Contributes everything the minigame needs through sockets: the net
    /// item (and its held visual, with the swing), the net-rack ship fixture (the same prefab is the fuel.net_rack socket
    /// fitting for designed ships), and the can prefab, which is registered with the network at runtime from here.
    /// Depends on the fuel feature (jerry-can supply, carry state), hence a later Order.
    /// </summary>
    [CreateAssetMenu(fileName = "FlyingCanFeatureInstaller", menuName = "TinCan/Features/Flying Can Feature Installer")]
    public class FlyingCanFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ItemDefinition>,
        FeatureInstaller.IExtension<ShipFixtureDefinition>, FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [SerializeField] private FlyingCanConfig? _config;
        [Tooltip("The items this feature owns (the catching net).")]
        [SerializeField] private List<ItemDefinition> _items = new();
        [Tooltip("Networked net-rack prefab and where it sits on the ship.")]
        [SerializeField] private ShipFixtureDefinition? _netRackFixture;
        [Tooltip("Socket fittings: the net rack (fuel.net_rack).")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override int Order => 10;

        public override void Install(IContainerBuilder builder)
        {
            var config = _config;
            if (config == null)
            {
                config = CreateInstance<FlyingCanConfig>();
                config.Enabled = false;
                Debug.LogWarning($"[{name}] No FlyingCanConfig assigned; flying cans disabled.", this);
            }

            builder.RegisterInstance(config);
            builder.Register<FlyingCanWaveProcessor>(Lifetime.Transient);
            builder.Register<FlyingCanSpawningService>(Lifetime.Singleton).As<IFlyingCanSpawner>();
            builder.Register<FlyingCanUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<CatchProcessor>(Lifetime.Transient);
            builder.Register<NetCatchUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<TakeNetInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get
            {
                if (_config != null && _config.CanPrefab != null) yield return _config.CanPrefab;
                if (_netRackFixture != null && _netRackFixture.Prefab != null) yield return _netRackFixture.Prefab;
                foreach (var fitting in _fittings)
                {
                    if (fitting != null && fitting.Prefab != null) yield return fitting.Prefab;
                }
            }
        }

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get
            {
                if (_netRackFixture != null) yield return _netRackFixture;
            }
        }
    }
}
