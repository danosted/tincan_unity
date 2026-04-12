using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Application;
using TinCan.Features.FreeCamera;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Possession;
using TinCan.Features.ThirdPersonCharacter;
using TinCan.Network.Infrastructure;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

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
            builder.Register<AirshipEngine>(Lifetime.Transient);
            builder.Register<FreeCameraMovementProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraRotationProcessor>(Lifetime.Transient);
            builder.Register<HumanoidMovementProcessor>(Lifetime.Transient);

            // Register Application Use Cases
            builder.Register<DriveAirshipUseCase>(Lifetime.Singleton);

            // Register Networking
            builder.RegisterComponentInHierarchy<NetworkManager>().AsSelf();
            builder.Register<NetworkPlayerSpawner>(Lifetime.Singleton).As<INetworkPlayerSpawner>();
            builder.Register<NGONetworkService>(Lifetime.Singleton).As<INetworkService, IInitializable>();

            builder.Register<ActorRegistry>(Lifetime.Singleton).As<IActorRegistry>();

            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<FreeCameraMovementUseCase>();
                entryPoints.Add<HumanoidMovementUseCase>();
                entryPoints.Add<HumanoidLookUseCase>();
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

                // Find and inject all "Complete" Humanoid characters
                foreach (var character in FindObjectsByType<ThirdPersonHumanoidView>(FindObjectsInactive.Exclude))
                {
                    // Recommended: Inject into the entire GameObject hierarchy
                    container.InjectGameObject(character.gameObject);

                    // Register the character facade with the global actor system
                    registry.Register(character);
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
