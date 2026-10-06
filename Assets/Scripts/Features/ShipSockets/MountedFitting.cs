#nullable enable
using System;
using TinCan.Core.Ship.Sockets;
using Unity.Collections;
using Unity.Netcode;

namespace TinCan.Features.ShipSockets
{
    /// <summary>One fitting in one socket, as replicated: the socket, the fitting's id and the mounted object's network id.</summary>
    public struct MountedFitting : INetworkSerializeByMemcpy, IEquatable<MountedFitting>
    {
        public int PartInstanceId;
        public int SocketIndex;
        public FixedString64Bytes FittingId;
        public ulong FixtureNetworkObjectId;

        public MountedFitting(ShipSocketId socket, string fittingId, ulong fixtureNetworkObjectId)
        {
            PartInstanceId = socket.PartInstanceId;
            SocketIndex = socket.Index;
            FittingId = new FixedString64Bytes(fittingId);
            FixtureNetworkObjectId = fixtureNetworkObjectId;
        }

        public ShipSocketId Socket => new(PartInstanceId, SocketIndex);

        public bool Equals(MountedFitting other) =>
            PartInstanceId == other.PartInstanceId && SocketIndex == other.SocketIndex && FittingId.Equals(other.FittingId)
            && FixtureNetworkObjectId == other.FixtureNetworkObjectId;

        public override bool Equals(object? obj) => obj is MountedFitting other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(PartInstanceId, SocketIndex, FixtureNetworkObjectId);

        public override string ToString() => $"{FittingId} in {Socket}";
    }
}
