using VContainer;
using VContainer.Unity;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.FreeCamera;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Possession;
using TinCan.Features.Airship;
using TinCan.Features.CloudBoundary;
using TinCan.Features.GasChallenge;
using TinCan.Features.Interaction;
using TinCan.Features.Abilities;
using TinCan.Features.Events;
using TinCan.Network.Infrastructure;
using UnityEngine;
using Unity.Netcode;
using TinCan.Core.Infrastructure.Extensions;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Events;
using TinCan.Core.Infrastructure.Events;
using TinCan.Core.Domain.Abilities.Tags;
using Assets.Scripts.Features.Airship;
using TinCan.Features.UI;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Features.Carry;
using TinCan.Features.UI.Commands;
using TinCan.UI;
namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Composition root for the project using VContainer.
    /// This defines which services are available for injection.
    /// </summary>
    public class ProjectLifetimeScope : LifetimeScope
    {
        [Header("Networking")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _airshipPrefab;

        [Header("APIs & Configs")]
        [SerializeField] private GameObject _possessionMediatorPrefab;
        [SerializeField] private GameObject _buildPlacementMediatorPrefab;
        [SerializeField] private TinCan.Core.Domain.Abilities.InputBindingConfig _inputBindingConfig;
        [SerializeField] private CloudBoundaryConfig _cloudBoundaryConfig;
        [SerializeField] private CloudVisualProfile _cloudVisualProfile;

        [Header("Abilities")]
        [SerializeField] private TinCan.Core.Domain.Abilities.Tags.GameplayTag _buildingTag;

        [Header("Building Modules")]
        [SerializeField] private List<GameObject> _buildablePrefabs = new();

        [Header("Airship Components")]
        [SerializeField] private GameplayTag _doorInteractionTag;

        [Header("UI")]
        [SerializeField] private MenuDefinition _mainMenu;

        [Header("Minigame")]
        [SerializeField] private FlyingCanConfig _flyingCanConfig;
        protected override void Configure(IContainerBuilder builder)
        {
            // Register Configs
            builder.RegisterInstance(_inputBindingConfig);
            builder.RegisterInstance(_cloudBoundaryConfig);
            builder.RegisterInstance(_cloudVisualProfile);

            // Register Events
            builder.Register<DebugLogEventObserver>(Lifetime.Singleton).As<IEventObserver>();
            builder.Register<EventPublisher>(Lifetime.Singleton).As<IEventPublisher>();

            // Register Domain logic (Plain C# classes)
            builder.Register<AirshipMovementProcessor>(Lifetime.Transient);
            builder.Register<CloudBoundaryProcessor>(Lifetime.Singleton);
            builder.Register<CloudSurfaceQuery>(Lifetime.Singleton).As<ICloudSurfaceQuery>();
            builder.Register<NoOpCloudBoundaryExpiryHandler>(Lifetime.Singleton).As<ICloudBoundaryExpiryHandler>();
            builder.Register<FreeCameraMovementProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraRotationProcessor>(Lifetime.Transient);
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
            builder.Register<PossessionInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<ActivateAbilityInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<InteractionHandlerRegistry>(Lifetime.Singleton).As<IInteractionHandlerRegistry>();

            // Register Possession Mediator Factory lazily
            builder.RegisterFactory<IPossessionNetworkMediator>((c) => () => FindAnyObjectByType<Features.Possession.Infrastructure.PossessionNetworkMediator>(), Lifetime.Singleton);

            // Register Server Possession Manager
            builder.Register<ServerPossessionManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf().As<IPossessionAuthority>();

            // builder.Register<VehicleBoardingUseCase>(Lifetime.Singleton).As<IVehicleBoardingUseCase>();
            builder.Register<InteractionOrchestrator>(Lifetime.Singleton).As<IInteractionOrchestrator>();
            builder.Register<ModuleSpawningService>(Lifetime.Singleton).As<IModuleSpawningService>();
            builder.Register<ModulePlacementUseCase>(Lifetime.Singleton).As<IModulePlacementUseCase>();

            // VContainer cannot inject parameters into classes resolved via EntryPoints using standard Register.
            // For BuildModeUseCase, we register it as an EntryPoint and pass the parameter directly to the EntryPoint builder.
            // Registration is handled inside UseEntryPoints below.

            builder.Register<PossessionUseCase>(Lifetime.Singleton)
                .AsSelf()
                .As<IInitializable>()
                .As<ITickable>().As<IPossessionState>();
            builder.Register<AbilitySystemUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>().As<ITickable>();
            builder.Register<ShipStateProvider>(Lifetime.Singleton).As<IShipState>();
            builder.Register<AirshipMovementUseCase>(Lifetime.Singleton);
            builder.Register<CloudBoundaryUseCase>(Lifetime.Singleton);
            builder.Register<GasChallengeUseCase>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<HumanoidMovementUseCase>(Lifetime.Singleton).AsSelf().As<IHumanoidRespawnService>();
            builder.Register<NetworkSimulationScheduler>(Lifetime.Singleton).As<IInitializable>();
            builder.RegisterComponentInHierarchy<CloudEnvironmentView>();

            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<FreeCameraMovementUseCase>();
                entryPoints.Add<PlayerLookUseCase>();
                entryPoints.Add<VehicleBoardingUseCase>().As<IVehicleBoardingUseCase>();
                entryPoints.Add<PossessionInputController>();
                entryPoints.Add<InteractivityUseCase>();
                builder.Register<ScriptedInput>(Lifetime.Singleton).AsSelf().As<IScriptedInput>().As<ILateTickable>();
                builder.Register<InputGate>(Lifetime.Singleton);
                entryPoints.Add<UnityInputService>().As<IInputService>();
                entryPoints.Add<EventOrchestratorUseCase>().As<IEventOrchestrator>();
                entryPoints.Add<BuildModeUseCase>().WithParameter("buildingTag", _buildingTag);
            });

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
                container.AddNetworkedPrefab(
                    networkManager: networkManager,
                    prefab: _buildPlacementMediatorPrefab,
                    onServerStarted: () =>
                    {
                        var instance = Instantiate(_buildPlacementMediatorPrefab);
                        container.InjectGameObject(instance);
                        var netObj = instance.GetComponent<NetworkObject>();
                        netObj.Spawn();
                        DontDestroyOnLoad(instance);
                    });

                // Register all buildable module prefabs
                foreach (var modulePrefab in _buildablePrefabs)
                {
                    if (modulePrefab == null) continue;
                    container.AddNetworkedPrefab(networkManager, modulePrefab);
                }

                // Find and inject all "Complete" NetworkMediator actors (e.g. FreeCamera) to ensure they have their Registry reference
                foreach (var character in FindObjectsByType<NetworkMediator>(FindObjectsInactive.Exclude))
                {
                    // Injection handles dependency resolution
                    container.InjectGameObject(character.gameObject);
                }

            });


            // Airship components (Consider moving this to a separate LifetimeScope for the Airship feature)
            builder.Register<DoorInteractionHandler>(Lifetime.Singleton).WithParameter("handlerTag", _doorInteractionTag).As<IInteractionHandler>();

            ConfigureUi(builder);
            ConfigureFuel(builder);
            ConfigureFlyingCans(builder);
        }

        // Flying jerry cans (slice 3). Ticked from NetworkSimulationScheduler; disabled config when none is assigned.
        private void ConfigureFlyingCans(IContainerBuilder builder)
        {
            var config = _flyingCanConfig;
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FlyingCanConfig>();
                config.Enabled = false;
                Debug.LogWarning("[ProjectLifetimeScope] No FlyingCanConfig assigned; flying cans disabled.");
            }

            builder.RegisterInstance(config);
            builder.Register<FlyingCanWaveProcessor>(Lifetime.Transient);
            builder.Register<FlyingCanMotionProcessor>(Lifetime.Transient);
            builder.Register<FlyingCanSpawningService>(Lifetime.Singleton).As<IFlyingCanSpawner>();
            builder.Register<FlyingCanUseCase>(Lifetime.Singleton);
            builder.Register<CatchProcessor>(Lifetime.Transient);
            builder.Register<NetCatchUseCase>(Lifetime.Singleton);
            builder.Register<TakeNetInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();

            builder.RegisterBuildCallback(container =>
            {
                if (config.CanPrefab == null) return;
                container.AddNetworkedPrefab(container.Resolve<NetworkManager>(), config.CanPrefab);
            });
        }

        // Airship fuel loop (ship tasks prototype, slice 1). Ticked from NetworkSimulationScheduler.
        private void ConfigureFuel(IContainerBuilder builder)
        {
            builder.Register<FuelConsumptionProcessor>(Lifetime.Transient);
            builder.Register<FuelConsumptionUseCase>(Lifetime.Singleton);
            builder.Register<PourFuelInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<TakeJerryCanInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<FuelHudPresenter>(Lifetime.Singleton).As<ITickable>();
        }

        // Headless menu/HUD framework (ship tasks prototype, slice 0). See .docs/UI_FRAMEWORK.md.
        // Menus are MenuDefinition assets; commands are plain IMenuCommand classes; views are optional children of this prefab.
        private void ConfigureUi(IContainerBuilder builder)
        {
            builder.Register<MenuCommandRegistry>(Lifetime.Singleton).As<IMenuCommandRegistry>();
            builder.Register<MenuUseCase>(Lifetime.Singleton).As<IMenuSystem>();
            builder.Register<CommandLineSessionBootstrap>(Lifetime.Singleton).As<IStartable>();
            builder.Register<HudUseCase>(Lifetime.Singleton).As<IHudValues>();
            builder.Register<StartHostMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<JoinGameMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<QuitMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();

            if (_mainMenu != null)
            {
                builder.RegisterInstance(_mainMenu);
                builder.Register<MainMenuBootstrap>(Lifetime.Singleton).As<IInitializable>().As<ITickable>();
            }
            else
            {
                Debug.LogWarning("[ProjectLifetimeScope] No main MenuDefinition assigned; the start menu will not appear.");
            }

            // Views are optional: inject whichever overlay components exist in the scene (usually children of this prefab).
            builder.RegisterBuildCallback(container =>
            {
                foreach (var view in FindObjectsByType<MenuOverlayView>(FindObjectsInactive.Include))
                {
                    container.Inject(view);
                }
                foreach (var view in FindObjectsByType<HudOverlayView>(FindObjectsInactive.Include))
                {
                    container.Inject(view);
                }
            });
        }

    }
}

