#nullable enable
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// Gameplay cues: presentation (sound, VFX, HUD) for gameplay moments, identified by <c>Cue.*</c> tags. Effects
    /// declare cues (<see cref="GameplayEffectDefinition.Cues"/>); features contribute what a cue does as
    /// <see cref="GameplayCueNotify"/> assets or as <c>IGameplayCueHandler</c> components on the target. Without this
    /// installer the ability system resolves no dispatcher and cues stay silent, with no errors.
    /// See .docs/plans/gameplay-cues.md.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayCuesFeatureInstaller", menuName = "TinCan/Features/Gameplay Cues Installer")]
    public class GameplayCuesFeatureInstaller : FeatureInstaller
    {
        // Right after the tag registry: other features' effects fire cues from their first tick.
        public override int Order => -19;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<GameplayCueCatalog>(Lifetime.Singleton);
            builder.Register<GameplayCueStateTracker>(Lifetime.Singleton);
            builder.Register<GameplayCueDispatchProcessor>(Lifetime.Singleton);
            builder.Register<GameplayCueUseCase>(Lifetime.Singleton)
                .As<IGameplayCuePlayer>().As<IGameplayCueFeed>().As<ITickable>().As<IInitializable>();
            builder.Register<GameplayCueDispatcher>(Lifetime.Singleton).As<IGameplayCueDispatcher>();
        }
    }
}
