#nullable enable
using VContainer;
using VContainer.Unity;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Possession;
using TinCan.Core.Ship;
using TinCan.Core.Interaction;
using TinCan.Core.Gas;
using TinCan.Network.Infrastructure;
using UnityEngine;
using Unity.Netcode;
using TinCan.Core.Infrastructure.Extensions;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Events;
using TinCan.Core.Infrastructure.Events;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.Ship.Fixtures;
namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Composition root: core services only (networking, time, registries, possession, abilities, input, the movement
    /// simulations and their scheduler, interaction core, spawning). Every feature registers through a FeatureInstaller
    /// asset; see .docs/FEATURE_INSTALLERS.md.
    /// </summary>
    public class ProjectLifetimeScope : LifetimeScope
    {
        [Header("Networking")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _airshipPrefab;

        [Header("APIs & Configs")]
        [SerializeField] private GameObject _possessionMediatorPrefab;

        [Header("Feature Composition")]
        [SerializeField] private FeatureProfile _featureProfile;

        private FeatureInstallerCatalog _features = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            // Composition: the core systems' installers always load; this scene's FeatureProfile adds its features.
            // Without a profile, every installer under Resources/Installers loads.
            // Adding a feature must not require editing this file; see .docs/FEATURE_INSTALLERS.md.
            _features = FeatureInstallerCatalog.LoadForScene(_featureProfile);
            builder.RegisterInstance(_features).AsSelf();
            builder.Register<ShipFixtureCatalog>(Lifetime.Singleton).As<IShipFixtureCatalog>();
            builder.Register<ShipFixtureSpawningUseCase>(Lifetime.Singleton).As<IInitializable>().As<ITickable>();

            // Register Events
            builder.Register<DebugLogEventObserver>(Lifetime.Singleton).As<IEventObserver>();
            builder.Register<EventPublisher>(Lifetime.Singleton).As<IEventPublisher>();

            // Register Domain logic (Plain C# classes)
            builder.Register<AirshipMovementProcessor>(Lifetime.Transient);
            builder.Register<HumanoidMovementProcessor>(Lifetime.Transient);

            // Register Application Use Cases

            // Register Networking
            builder.RegisterComponentInHierarchy<NetworkManager>().AsSelf();
            builder.Register<NetworkPlayerSpawner>(Lifetime.Singleton).As<INetworkPlayerSpawner>();
            builder.Register<NGONetworkService>(Lifetime.Singleton).As<INetworkService, IInitializable>();
            builder.Register<ProjectTimeService>(Lifetime.Singleton).AsSelf().As<ITimeService>();

            builder.Register<ActorRegistry>(Lifetime.Singleton).As<IActorRegistry>();
            builder.Register<InteractorRegistry>(Lifetime.Singleton).As<IInteractorRegistry>();
            builder.Register<Abilities.AbilityRegistry>(Lifetime.Singleton).As<IAbilityRegistry>();
            builder.Register<ActorOrchestrator>(Lifetime.Singleton).As<IActorOrchestrator>();
            builder.Register<NgoInteractionTargetResolver>(Lifetime.Singleton).As<IInteractionTargetResolver>();
            builder.Register<ActivateAbilityInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<InteractionHandlerRegistry>(Lifetime.Singleton).As<IInteractionHandlerRegistry>();

            // Register Possession Mediator Factory lazily
            builder.RegisterFactory<IPossessionNetworkMediator>((c) => () => FindAnyObjectByType<TinCan.Core.Possession.Infrastructure.PossessionNetworkMediator>(), Lifetime.Singleton);

            // Register Server Possession Manager
            builder.Register<ServerPossessionManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf().As<IPossessionAuthority>();
            builder.Register<PossessedViewCamera>(Lifetime.Singleton).As<ILocalViewCamera>();

            builder.Register<InteractionOrchestrator>(Lifetime.Singleton).As<IInteractionOrchestrator>();
            builder.Register<ModuleSpawningService>(Lifetime.Singleton).As<IModuleSpawningService>();

            builder.Register<PossessionUseCase>(Lifetime.Singleton)
                .AsSelf()
                .As<IInitializable>()
                .As<ITickable>().As<IPossessionState>();
            builder.Register<AbilitySystemUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>().As<ISimulationTickable>();
            builder.Register<ShipStateProvider>(Lifetime.Singleton).As<IShipState>();
            builder.Register<AirshipMovementUseCase>(Lifetime.Singleton).AsSelf().As<IAirshipCollisionResponse>();
            builder.Register<HumanoidMovementUseCase>(Lifetime.Singleton).AsSelf().As<IHumanoidRespawnService>();
            builder.Register<NetworkSimulationScheduler>(Lifetime.Singleton).As<IInitializable>();

            var installed = InstallerServiceCheck.Install(_features.Installers, builder);

            // Input itself (actions, contexts, the reader and the router) is the core InputFeatureInstaller.
            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<PlayerLookUseCase>();
                builder.Register<SwitchPossessionInputHandler>(Lifetime.Singleton).As<IInputCommandHandler>();
            });

            // Every feature's services must be buildable from what this scene loads; refuse to start otherwise.
            var missing = InstallerServiceCheck.FindMissing(installed, t => builder.Exists(t, includeInterfaceTypes: true, findParentScopes: true));
            if (missing.Count > 0)
            {
                throw new System.InvalidOperationException(
                    $"Feature services this scene cannot build ({(_featureProfile != null ? _featureProfile.name : "Resources/" + FeatureInstallerCatalog.ResourcesFolder)}). " +
                    "Add the installers that provide them to the profile, or resolve them optionally:\n  " + string.Join("\n  ", missing));
            }

            // Handle multi-instance actors in the scene hierarchy
            builder.RegisterBuildCallback(container =>
            {
                var orchestrator = container.Resolve<IActorOrchestrator>();
                var networkService = container.Resolve<INetworkService>();

                // Configure the network service with the prefab from the Inspector
                networkService.SetPlayerPrefab(_playerPrefab);

                // Register the Prefab Interceptor to ensure VContainer injection on all clients
                var networkManager = container.Resolve<NetworkManager>();
                var spawner = container.Resolve<INetworkPlayerSpawner>();

                container.AddNetworkedPrefab(
                    networkManager,
                    _playerPrefab,
                    configureInit: (instance, ownerClientId) =>
                    {
                        // Ensure consistent naming across network
                        instance.name = $"{_playerPrefab.name}_Client{ownerClientId}";

                    },
                    configureDestroy: null
                );
                container.AddNetworkedPrefab(
                    networkManager: networkManager,
                    prefab: _airshipPrefab,
                    onServerStarted: () =>
                    {
                        // Spawn the airship on server start
                        var airshipInstance = Instantiate(
                            _airshipPrefab,
                            _airshipPrefab.transform.position + Vector3.up * 40f,
                            _airshipPrefab.transform.rotation);
                        container.InjectGameObject(airshipInstance);
                        var netObj = airshipInstance.GetComponent<NetworkObject>();
                        netObj.Spawn();
                    });

                container.AddNetworkedPrefab(
                    networkManager: networkManager,
                    prefab: _possessionMediatorPrefab,
                    onServerStarted: () =>
                    {
                        var instance = Instantiate(_possessionMediatorPrefab);
                        container.InjectGameObject(instance);
                        var netObj = instance.GetComponent<NetworkObject>();
                        netObj.Spawn();
                        DontDestroyOnLoad(instance);

                        // Explicitly initialize the authoritative service when the mediator is ready
                        if (instance.TryGetComponent(out IPossessionNetworkMediator mediator))
                        {
                            var manager = container.Resolve<ServerPossessionManager>();
                            manager.Subscribe();
                        }
                    });

                // Feature-owned networked prefabs: registered with NGO at runtime (no DefaultNetworkPrefabs edits)
                // and with the DI interceptor so instances are injected on every peer.
                foreach (var prefab in _features.NetworkedPrefabs)
                {
                    if (!IsInStaticPrefabLists(networkManager, prefab) && !networkManager.NetworkConfig.Prefabs.Contains(prefab))
                    {
                        networkManager.AddNetworkPrefab(prefab);
                    }
                    container.AddNetworkedPrefab(networkManager, prefab);
                }

                // Views and other scene objects that opt into injection (e.g. UI overlays on this prefab).
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                {
                    if (behaviour is IInjectedView) container.Inject(behaviour);
                }

                foreach (var installer in _features.Installers)
                {
                    installer.OnContainerBuilt(container);
                }
            });

        }

        // NetworkConfig.Prefabs is only initialised when a session starts, so Contains() cannot see the list assets yet;
        // NGO's editor tooling may also have auto-added a prefab to DefaultNetworkPrefabs. Check the assets directly.
        private static bool IsInStaticPrefabLists(NetworkManager networkManager, GameObject prefab)
        {
            foreach (var list in networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists)
            {
                if (list == null) continue;
                foreach (var entry in list.PrefabList)
                {
                    if (entry != null && entry.Prefab == prefab) return true;
                }
            }
            return false;
        }

    }
}

