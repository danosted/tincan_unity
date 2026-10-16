#nullable enable
using System;

namespace TinCan.Features.Respawn
{
    /// <summary>Server: a leashed player reached the leash point (climbed or winched) and stands on deck, keeping what they carry.</summary>
    public readonly struct PlayerRescuedEvent
    {
        public readonly Guid PlayerId;
        public readonly Guid PointId;

        public PlayerRescuedEvent(Guid playerId, Guid pointId)
        {
            PlayerId = playerId;
            PointId = pointId;
        }

        public override string ToString() => $"PlayerRescued player={PlayerId} point={PointId}";
    }
}
