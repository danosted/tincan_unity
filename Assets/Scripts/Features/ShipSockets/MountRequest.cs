#nullable enable
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>A player asking the server to mount a fitting in a socket (it validates everything).</summary>
    public readonly struct MountRequest
    {
        public MountRequest(ulong clientId, ShipSocketId socket, string fittingId)
        {
            ClientId = clientId;
            Socket = socket;
            FittingId = fittingId;
        }

        public ulong ClientId { get; }
        public ShipSocketId Socket { get; }
        public string FittingId { get; }
    }
}
