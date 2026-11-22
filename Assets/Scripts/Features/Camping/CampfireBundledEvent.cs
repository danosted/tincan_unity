#nullable enable
using System;

namespace TinCan.Features.Camping
{
    /// <summary>A player tied the log they held into a campfire bundle.</summary>
    public readonly struct CampfireBundledEvent
    {
        public readonly Guid PlayerId;

        public CampfireBundledEvent(Guid playerId) => PlayerId = playerId;

        public override string ToString() => $"Player {PlayerId} tied a campfire bundle";
    }
}
