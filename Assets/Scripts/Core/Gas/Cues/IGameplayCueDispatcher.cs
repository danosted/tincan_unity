#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// Fires a burst cue on a target so every peer plays it once (<see cref="GameplayCueDispatchProcessor"/>). Instant
    /// effects call it for their cues; a feature may call it directly for a moment that is not an effect.
    /// </summary>
    public interface IGameplayCueDispatcher
    {
        void Execute(GameplayTag cue, IAbilityControllerBase target, GameplayEffectContext context);
    }
}
