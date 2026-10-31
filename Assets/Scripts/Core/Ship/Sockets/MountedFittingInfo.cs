#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship.Sockets
{
    /// <summary>A fitting in a socket, and the socket's mount marker (the fitting stands on it).</summary>
    public readonly struct MountedFittingInfo
    {
        public MountedFittingInfo(ShipSocketId socket, string fittingId, Transform mount)
        {
            Socket = socket;
            FittingId = fittingId;
            Mount = mount;
        }

        public ShipSocketId Socket { get; }
        public string FittingId { get; }
        public Transform Mount { get; }

        public override string ToString() => $"{FittingId} in {Socket}";
    }
}
