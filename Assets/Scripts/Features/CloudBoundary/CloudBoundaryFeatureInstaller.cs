#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Atmosphere;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The cloud deck as a boundary: a ship that sinks too deep for too long is lost with its crew (the voyage ends on
    /// it, <see cref="IShipLossSource"/>), and a player who falls overboard is respawned, on the server's
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
            builder.RegisterComponentInHierarchy<CloudEnvironmentView>().AsSelf().As<IViewSubmersion>();
            builder.Register<CloudBoundaryProcessor>(Lifetime.Singleton);
            builder.Register<CloudSurfaceQuery>(Lifetime.Singleton).As<ICloudSurfaceQuery>();
            builder.Register<CloudBoundaryUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>()
                .As<IShipLossSource>().As<IShipRecovery>();
        }
    }
}
