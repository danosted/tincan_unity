using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.FreeCamera;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Possession;
using TinCan.Features.Airship;
using TinCan.Features.Interaction;
using TinCan.Network.Infrastructure;
using UnityEngine;
using Unity.Netcode;
using TinCan.Core.Infrastructure.Extensions;
using TinCan.Features.Possession.Infrastructure;

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

        [Header("APIs")]
        [SerializeField] private GameObject _possessionApiPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            // Register Domain logic (Plain C# classes)
            builder.Register<AirshipMovementProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraMovementProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraRotationProcessor>(Lifetime.Transient);
            builder.Register<HumanoidMovementProcessor>(Lifetime.Transient);

            // Register Application Use Cases

            // Register Networking
            builder.RegisterComponentInHierarchy<NetworkManager>().AsSelf();
            builder.Register<NetworkPlayerSpawner>(Lifetime.Singleton).As<INetworkPlayerSpawner>();
            builder.Register<NGONetworkService>(Lifetime.Singleton).As<INetworkService, IInitializable>();
            builder.Register<ProjectTimeService>(Lifetime.Singleton).As<ITimeService>();

            builder.Register<ActorRegistry>(Lifetime.Singleton).As<IActorRegistry>();
            builder.Register<InteractorRegistry>(Lifetime.Singleton).As<IInteractorRegistry>();
            builder.Register<ActorOrchestrator>(Lifetime.Singleton).As<IActorOrchestrator>();

            // Register Possession API Factory lazily
            builder.RegisterFactory<IPossessionApi>((c) => () => FindAnyObjectByType<Features.Possession.Infrastructure.PossessionApi>(), Lifetime.Singleton);

            builder.Register<VehicleBoardingUseCase>(Lifetime.Singleton).As<IVehicleBoardingUseCase>();
            builder.Register<InteractionOrchestrator>(Lifetime.Singleton).As<IInteractionOrchestrator>();
            builder.Register<PossessionUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>(); ;

            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<FreeCameraMovementUseCase>();
                entryPoints.Add<HumanoidMovementUseCase>();
                entryPoints.Add<HumanoidLookUseCase>();
                entryPoints.Add<AirshipMovementUseCase>();
                entryPoints.Add<PossessionInputController>();
                entryPoints.Add<InteractivityUseCase>();
                entryPoints.Add<UnityInputService>().As<IInputService>();
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

                        // On clients (and host), notify if this is our local player
                        // This triggers possession logic for the networked object
                        if (ownerClientId == networkManager.LocalClientId)
                        {
                            Debug.Log($"[ProjectLifetimeScope] Local player object {instance.name} detected. Notifying spawner.");
                            spawner.NotifyPlayerSpawned(instance, ownerClientId, true);
                        }
                    },
                    configureDestroy: null
                );
                container.AddNetworkedPrefab(
                    networkManager: networkManager,
                    prefab: _airshipPrefab,
                    onServerStarted: () =>
                    {
                        // Spawn the airship on server start
                        var airshipInstance = Instantiate(_airshipPrefab);
                        container.InjectGameObject(airshipInstance);
                        var netObj = airshipInstance.GetComponent<NetworkObject>();
                        netObj.Spawn();
                    });

                container.AddNetworkedPrefab(
                    networkManager: networkManager,
                    prefab: _possessionApiPrefab,
                    onServerStarted: () =>
                    {
                        var instance = Instantiate(_possessionApiPrefab);
                        container.InjectGameObject(instance);
                        var netObj = instance.GetComponent<NetworkObject>();
                        netObj.Spawn();
                        DontDestroyOnLoad(instance);
                    });

                // Find and inject all "Complete" NetworkMediator actors (e.g. FreeCamera) to ensure they have their Registry reference
                foreach (var character in FindObjectsByType<NetworkMediator>(FindObjectsInactive.Exclude))
                {
                    // Injection handles dependency resolution
                    container.InjectGameObject(character.gameObject);
                }

            });

        }

    }
}

