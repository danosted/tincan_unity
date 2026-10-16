#nullable enable
using System;

namespace TinCan.Features.Respawn
{
    /// <summary>Server: a player fell overboard and now waits for their choice.</summary>
    public readonly struct PlayerOverboardEvent
    {
        public readonly Guid PlayerId;

        public PlayerOverboardEvent(Guid playerId) => PlayerId = playerId;

        public override string ToString() => $"PlayerOverboard player={PlayerId}";
    }
}
