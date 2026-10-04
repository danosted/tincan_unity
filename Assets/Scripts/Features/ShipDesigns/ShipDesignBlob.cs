#nullable enable
using System;
using Unity.Netcode;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A design in its network form (<see cref="ShipDesignBinaryCodec"/>) with its hash, as one replicated value. Two
    /// blobs are equal when their hashes and lengths are, so writing the same design again sends nothing.
    /// </summary>
    public sealed class ShipDesignBlob : INetworkSerializable, IEquatable<ShipDesignBlob>
    {
        public ShipDesignBlob()
        {
        }

        public ShipDesignBlob(ulong hash, byte[] bytes)
        {
            Hash = hash;
            Bytes = bytes;
        }

        public ulong Hash;
        public byte[] Bytes = Array.Empty<byte>();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Hash);
            serializer.SerializeValue(ref Bytes);
        }

        public bool Equals(ShipDesignBlob? other) => other != null && Hash == other.Hash && Bytes.Length == other.Bytes.Length;

        public override bool Equals(object? obj) => Equals(obj as ShipDesignBlob);

        public override int GetHashCode() => Hash.GetHashCode();
    }
}
