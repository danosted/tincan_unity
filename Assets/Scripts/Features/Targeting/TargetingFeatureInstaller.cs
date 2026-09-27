#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Targeting;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Targeting
{
    /// <summary>
    /// Targeting: the registry of everything that can be aimed at (filled by ActorOrchestrator) and the query service
    /// features and abilities use. Foundational and cross-cutting, so it runs early and belongs in Profile_Base.
    /// See .docs/plans/targeting-subsystem.md and the Targeting pillar in ARCHITECTURE.md.
    /// </summary>
    [CreateAssetMenu(fileName = "TargetingFeatureInstaller", menuName = "TinCan/Features/Targeting Feature Installer")]
    public class TargetingFeatureInstaller : FeatureInstaller
    {
        public override int Order => -18;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<TargetableRegistry>(Lifetime.Singleton).As<ITargetableRegistry>();
            builder.Register<TargetingProcessor>(Lifetime.Transient);
            builder.Register<TargetingUseCase>(Lifetime.Singleton).As<ITargetingService>();
        }
    }
}
