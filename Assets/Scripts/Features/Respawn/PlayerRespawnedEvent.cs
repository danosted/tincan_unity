#nullable enable
using System;

namespace TinCan.Features.Respawn
{
    /// <summary>Server: a player respawned aboard a ship, after the rule for their cause.</summary>
    public readonly struct PlayerRespawnedEvent
    {
        public readonly Guid PlayerId;
        public readonly RespawnCause Cause;
        public readonly string LostItem;

        public PlayerRespawnedEvent(Guid playerId, RespawnCause cause, string lostItem)
        {
            PlayerId = playerId;
            Cause = cause;
            LostItem = lostItem;
        }

        public override string ToString() => $"PlayerRespawned player={PlayerId} cause={Cause} lost={LostItem}";
    }
}
