#nullable enable
using System;

namespace TinCan.Core.Ship.Sockets
{
    /// <summary>
    /// A socket on a built ship: its part's instance id (stable in the design) and its index among that part's sockets.
    /// The same on every peer, since every peer builds the same parts.
    /// </summary>
    public readonly struct ShipSocketId : IEquatable<ShipSocketId>
    {
        public ShipSocketId(int partInstanceId, int index)
        {
            PartInstanceId = partInstanceId;
            Index = index;
        }

        public int PartInstanceId { get; }
        public int Index { get; }

        public bool Equals(ShipSocketId other) => PartInstanceId == other.PartInstanceId && Index == other.Index;

        public override bool Equals(object? obj) => obj is ShipSocketId other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(PartInstanceId, Index);

        public override string ToString() => $"socket {Index} of part #{PartInstanceId}";
    }
}
