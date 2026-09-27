#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>Plays a burst cue on this peer, now: every handler of the cue gets <c>OnExecute</c>.</summary>
    public interface IGameplayCuePlayer
    {
        void Play(GameplayTag cue, IAbilityControllerBase target);
    }
}
