#nullable enable
using System;

namespace TinCan.Features.Respawn
{
    /// <summary>Server: a player started or stopped a leash point's winch.</summary>
    public readonly struct WinchToggledEvent
    {
        public readonly Guid RequesterId;
        public readonly Guid PointId;
        public readonly bool Reeling;

        public WinchToggledEvent(Guid requesterId, Guid pointId, bool reeling)
        {
            RequesterId = requesterId;
            PointId = pointId;
            Reeling = reeling;
        }

        public override string ToString() => $"WinchToggled requester={RequesterId} point={PointId} reeling={Reeling}";
    }
}
