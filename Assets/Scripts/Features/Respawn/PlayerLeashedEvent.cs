#nullable enable
using System;

namespace TinCan.Features.Respawn
{
    /// <summary>Server: a player overboard took the leash and now hangs on a ship's leash point.</summary>
    public readonly struct PlayerLeashedEvent
    {
        public readonly Guid PlayerId;
        public readonly Guid PointId;

        public PlayerLeashedEvent(Guid playerId, Guid pointId)
        {
            PlayerId = playerId;
            PointId = pointId;
        }

        public override string ToString() => $"PlayerLeashed player={PlayerId} point={PointId}";
    }
}
