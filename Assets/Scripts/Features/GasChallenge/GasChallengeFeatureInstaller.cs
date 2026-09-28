#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Gas pockets in the scene detonate when an airship flies into them (server, simulation tick).</summary>
    [CreateAssetMenu(fileName = "GasChallengeFeatureInstaller", menuName = "TinCan/Features/Gas Challenge Feature Installer")]
    public class GasChallengeFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<PhysicsGasPocketQuery>(Lifetime.Singleton).As<IGasPocketQuery>();
            builder.Register<GasChallengeUseCase>(Lifetime.Singleton).As<ISimulationTickable>();
        }
    }
}
