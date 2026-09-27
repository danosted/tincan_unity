#nullable enable
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Core.Domain.Cues
{
    /// <summary>
    /// Presentation for one gameplay cue (a <c>Cue.*</c> tag). Runs on every peer and never changes gameplay state.
    /// <list type="bullet">
    /// <item><see cref="OnExecute"/>: a burst (an Instant effect's cue, or a direct dispatch). One-shot; late joiners miss it.</item>
    /// <item><see cref="OnActive"/> / <see cref="OnRemoved"/>: the cue tag appeared on / left the target (a Duration or
    /// Infinite effect's cue). Late joiners get <see cref="OnActive"/> for cues already active. A despawning target gets
    /// no <see cref="OnRemoved"/>: teardown is not a gameplay moment.</item>
    /// </list>
    /// Implemented by feature-contributed notify assets and by components in the target's own hierarchy.
    /// </summary>
    public interface IGameplayCueHandler
    {
        GameplayTag? Cue { get; }

        void OnExecute(in GameplayCueEvent cueEvent);
        void OnActive(in GameplayCueEvent cueEvent);
        void OnRemoved(in GameplayCueEvent cueEvent);
    }
}
