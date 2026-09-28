#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The cloud deck as a boundary: ships and players that sink into it are pushed out or respawned, on the server's
    /// simulation tick (<see cref="SimulationPhase.BeforeHumanoid"/>). Reads the deck from the scene's
    /// <see cref="CloudEnvironmentView"/>, so the scene needs one (the test-range scenes have it).
    /// </summary>
    [CreateAssetMenu(fileName = "CloudBoundaryFeatureInstaller", menuName = "TinCan/Features/Cloud Boundary Feature Installer")]
    public class CloudBoundaryFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private CloudBoundaryConfig _config = null!;
        [SerializeField] private CloudVisualProfile _visualProfile = null!;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.RegisterInstance(_visualProfile);
            builder.RegisterComponentInHierarchy<CloudEnvironmentView>();
            builder.Register<CloudBoundaryProcessor>(Lifetime.Singleton);
            builder.Register<CloudSurfaceQuery>(Lifetime.Singleton).As<ICloudSurfaceQuery>();
            builder.Register<NoOpCloudBoundaryExpiryHandler>(Lifetime.Singleton).As<ICloudBoundaryExpiryHandler>();
            builder.Register<CloudBoundaryUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
        }
    }
}
