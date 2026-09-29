#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas.Cues;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Records every burst cue the ability system dispatches, with its context.</summary>
    public sealed class RecordingCueDispatcher : IGameplayCueDispatcher
    {
        public List<(GameplayTag Cue, IAbilityControllerBase Target, GameplayEffectContext Context)> Executed { get; } = new();

        public void Execute(GameplayTag cue, IAbilityControllerBase target, GameplayEffectContext context) => Executed.Add((cue, target, context));
    }
}
