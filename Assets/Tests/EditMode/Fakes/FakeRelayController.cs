#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities.Cues;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>An ability controller with a network side: ownership and the cues relayed to clients.</summary>
    public sealed class FakeRelayController : FakeAbilityController, IGameplayCueRelay
    {
        public bool IsOwnedLocally { get; set; }
        public List<(GameplayTag Cue, bool ExcludeOwner)> Relayed { get; } = new();

        public void RelayCue(GameplayTag cue, bool excludeOwner) => Relayed.Add((cue, excludeOwner));
    }
}
