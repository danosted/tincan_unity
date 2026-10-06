#nullable enable
using System;
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>Server: a fitting was mounted in a ship's socket.</summary>
    public readonly struct ShipFittingMountedEvent
    {
        public readonly Guid ShipId;
        public readonly ShipSocketId Socket;
        public readonly string FittingId;

        public ShipFittingMountedEvent(Guid shipId, ShipSocketId socket, string fittingId)
        {
            ShipId = shipId;
            Socket = socket;
            FittingId = fittingId;
        }

        public override string ToString() => $"{FittingId} mounted in {Socket} on ship {ShipId}";
    }
}
