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

namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Composition root for the project using VContainer.
    /// This defines which services are available for injection.
    /// </summary>
    public class ProjectLifetimeScope : LifetimeScope
    {
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
            builder.Register<NGONetworkService>(Lifetime.Singleton).As<INetworkService>();
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

                // Find and inject all "Complete" Humanoid characters
                foreach (var character in Object.FindObjectsByType<ThirdPersonHumanoidView>(FindObjectsInactive.Exclude))
                {
                    // Recommended: Inject into the entire GameObject hierarchy
                    container.InjectGameObject(character.gameObject);

                    // Register the character facade with the global actor system
                    registry.Register(character);
                }

                // Find and inject all standalone Network Mediators
                foreach (var mediator in Object.FindObjectsByType<HumanoidCharacterNetworkMediator>(FindObjectsInactive.Exclude))
                {
                    container.InjectGameObject(mediator.gameObject);
                    registry.Register(mediator);
                }
            });
        }
    }
}
