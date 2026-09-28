#nullable enable
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.FreeCamera
{
    /// <summary>The spectator free camera: moves and turns the possessed free camera from local input, per frame.</summary>
    [CreateAssetMenu(fileName = "FreeCameraFeatureInstaller", menuName = "TinCan/Features/Free Camera Feature Installer")]
    public class FreeCameraFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<FreeCameraMovementProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraRotationProcessor>(Lifetime.Transient);
            builder.Register<FreeCameraMovementUseCase>(Lifetime.Singleton).As<ITickable>();
        }
    }
}
