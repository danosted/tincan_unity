using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Application;
using TinCan.Features.FreeCamera;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Possession;
using TinCan.Features.Airship;
using TinCan.Network.Infrastructure;
using UnityEngine;
using Unity.Netcode;
using System.Linq;
using TinCan.Features.Environment;

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

            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<FreeCameraMovementUseCase>();
                entryPoints.Add<HumanoidMovementUseCase>();
                entryPoints.Add<HumanoidLookUseCase>();
                entryPoints.Add<AirshipMovementUseCase>();
                entryPoints.Add<PossessionUseCase>();
                entryPoints.Add<UnityInputService>().As<IInputService>();
            });

            // Handle multi-instance actors in the scene hierarchy
            builder.RegisterBuildCallback(container =>
            {
                var registry = container.Resolve<IActorRegistry>();
                var networkService = container.Resolve<INetworkService>();

                // Configure the network service with the prefab from the Inspector
                networkService.SetPlayerPrefab(_playerPrefab);

                // Register the Prefab Interceptor to ensure VContainer injection on all clients
                var networkManager = container.Resolve<NetworkManager>();
                if (networkManager != null && _playerPrefab != null)
                {
                    var interceptor = new NetworkPrefabInterceptor(container, _playerPrefab);
                    networkManager.PrefabHandler.AddHandler(_playerPrefab, interceptor);
                    Debug.Log($"[ProjectLifetimeScope] Registered NetworkPrefabInterceptor for {_playerPrefab.name}");
                }

                // Inject into environmental components
                foreach (var oscillator in FindObjectsByType<SimpleOscillator>(FindObjectsInactive.Exclude))
                {
                    container.InjectGameObject(oscillator.gameObject);
                }

                // Find and inject all "Complete" NetworkMediator actors (e.g. FreeCamera) to ensure they have their Registry reference
                foreach (var character in FindObjectsByType<NetworkMediator>(FindObjectsInactive.Exclude))
                {
                    // Injection is required for the Mediator to get its Registry reference
                    container.InjectGameObject(character.gameObject);
                }

            });

        }
    }
}
