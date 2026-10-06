#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Server: what is mounted in ships' sockets. Players reach it through E on a socket and the fitting menu; a crafting
    /// system later, and scenarios now, call it directly.
    /// </summary>
    public interface IShipFittings
    {
        bool Mount(Guid shipId, ShipSocketId socket, string fittingId, out string? error);

        bool Unmount(Guid shipId, ShipSocketId socket);

        IReadOnlyList<MountedFitting> MountedOn(Guid shipId);
    }
}
