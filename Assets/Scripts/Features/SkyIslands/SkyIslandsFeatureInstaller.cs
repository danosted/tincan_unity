#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Sky islands: floating rock around the ship, built on every peer from the session's layout seed (a new layout every
    /// voyage, through <see cref="ISessionLayout"/>), so nothing is replicated. The server pushes the ship out of rock
    /// and hurts it (<see cref="SkyIslandImpactUseCase"/>).
    /// Plan: .docs/plans/sky-islands.md.
    /// </summary>
    [CreateAssetMenu(fileName = "SkyIslandsFeatureInstaller", menuName = "TinCan/Features/Sky Islands Feature Installer")]
    public class SkyIslandsFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private SkyIslandConfig? _config;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No SkyIslandConfig assigned; there are no sky islands.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<SkyIslandLayoutProcessor>(Lifetime.Transient);
            builder.Register<SkyIslandMeshProcessor>(Lifetime.Transient);
            builder.Register<SkyIslandBuilder>(Lifetime.Singleton).As<ISkyIslandBuilder>();
            builder.Register<SkyIslandStreamingUseCase>(Lifetime.Singleton).As<ISkyIslands>().As<ITickable>();
            builder.Register<PhysicsSkyIslandContactQuery>(Lifetime.Singleton).As<ISkyIslandContactQuery>();
            builder.Register<SkyIslandImpactUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
            builder.Register<PhysicsSkyIslandObstacleQuery>(Lifetime.Singleton).As<IWorldObstacleQuery>();
        }
    }
}
