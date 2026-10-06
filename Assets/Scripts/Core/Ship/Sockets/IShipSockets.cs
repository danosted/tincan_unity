#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Ship.Sockets
{
    /// <summary>
    /// The sockets each ship's parts provide, on this peer. Implemented by whatever builds ships from parts (ship designs);
    /// read by the systems that mount things in them. <see cref="Version"/> changes whenever a ship's sockets change.
    /// </summary>
    public interface IShipSockets
    {
        int Version { get; }

        IReadOnlyList<ShipSocketInfo> SocketsOf(Guid shipId);
    }
}
