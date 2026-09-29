#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas.Cues;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Records every burst cue played on this peer.</summary>
    public sealed class RecordingCuePlayer : IGameplayCuePlayer
    {
        public List<(GameplayTag Cue, IAbilityControllerBase Target)> Played { get; } = new();

        public void Play(GameplayTag cue, IAbilityControllerBase target) => Played.Add((cue, target));
    }
}
