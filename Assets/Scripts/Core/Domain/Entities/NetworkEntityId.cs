#nullable enable
using System;
using Unity.Netcode;

namespace TinCan.Core.Domain.Entities
{
    /// <summary>
    /// A networked entity's stable identity: a GUID the server assigns (or restores from a saved world) and replicates,
    /// so it is the same on every peer and across a respawn from saved state. Network-serializable as two ulongs.
    /// </summary>
    public struct NetworkEntityId : INetworkSerializable, IEquatable<NetworkEntityId>
    {
        private ulong _high;
        private ulong _low;

        public NetworkEntityId(Guid value)
        {
            var bytes = value.ToByteArray();
            _high = BitConverter.ToUInt64(bytes, 0);
            _low = BitConverter.ToUInt64(bytes, 8);
        }

        public Guid Value
        {
            get
            {
                var bytes = new byte[16];
                BitConverter.GetBytes(_high).CopyTo(bytes, 0);
                BitConverter.GetBytes(_low).CopyTo(bytes, 8);
                return new Guid(bytes);
            }
        }

        public bool IsEmpty => _high == 0 && _low == 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _high);
            serializer.SerializeValue(ref _low);
        }

        public bool Equals(NetworkEntityId other) => _high == other._high && _low == other._low;
        public override bool Equals(object? obj) => obj is NetworkEntityId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_high, _low);
        public override string ToString() => Value.ToString();
    }
}
