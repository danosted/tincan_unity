#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship.Sockets
{
    /// <summary>A socket a ship has right now: its id and its marker (the mount pose, on this peer).</summary>
    public readonly struct ShipSocketInfo
    {
        public ShipSocketInfo(ShipSocketId id, Transform mount)
        {
            Id = id;
            Mount = mount;
        }

        public ShipSocketId Id { get; }
        public Transform Mount { get; }
    }
}
