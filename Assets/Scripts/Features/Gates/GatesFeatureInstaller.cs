#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Gates
{
    /// <summary>
    /// Gates: places in the sky the ship passes through, each doing what its <see cref="GateDefinition"/> says, once.
    /// This feature lays none itself; a feature that uses gates (Boons) spawns them through <see cref="IGates"/> and
    /// lists its gate prefabs as networked prefabs. Plan: .docs/plans/hazards-and-boons.md (Gates).
    /// </summary>
    [CreateAssetMenu(fileName = "GatesFeatureInstaller", menuName = "TinCan/Features/Gates Feature Installer")]
    public class GatesFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<PhysicsShipOverlapQuery>(Lifetime.Singleton).As<IShipOverlapQuery>();
            builder.Register<GateSpawningService>(Lifetime.Singleton).As<IGateSpawner>();
            builder.Register<GateUseCase>(Lifetime.Singleton).As<IGates>().As<ISimulationTickable>().As<ISessionParticipant>();
        }
    }
}
