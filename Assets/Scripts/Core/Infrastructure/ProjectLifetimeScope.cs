using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Application;
using TinCan.Features.FreeCamera;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Possession;
using TinCan.Network.Infrastructure;

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

            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<FreeCameraMovementUseCase>();
                entryPoints.Add<HumanoidMovementUseCase>();
                entryPoints.Add<HumanoidLookUseCase>();
                entryPoints.Add<PossessionUseCase>();
                entryPoints.Add<UnityInputService>().As<IInputService>();

            });

            builder.UseComponents((components) =>
            {
                components.AddInHierarchy<FreeCameraTransformView>().AsImplementedInterfaces();
                components.AddInHierarchy<HumanoidControllerView>().AsImplementedInterfaces();
                components.AddInHierarchy<ThirdPersonLookView>().AsImplementedInterfaces();
            });
        }
    }
}
