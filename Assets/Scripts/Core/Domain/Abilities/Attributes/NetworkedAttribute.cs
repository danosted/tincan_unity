using System;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Domain.Abilities.Attributes
{
    /// <summary>
    /// Flat structure for synchronizing an attribute's state over the network.
    /// Avoids C# Dictionary allocations and supports Netcode for GameObjects.
    /// </summary>
    [Serializable]
    public struct NetworkedAttribute : INetworkSerializable, IEquatable<NetworkedAttribute>
    {
        public int AttributeHash;
        public AttributeValue Value;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref AttributeHash);
            serializer.SerializeValue(ref Value.BaseValue);
            serializer.SerializeValue(ref Value.CurrentValue);
        }

        public bool Equals(NetworkedAttribute other)
        {
            return AttributeHash == other.AttributeHash &&
                   Mathf.Approximately(Value.BaseValue, other.Value.BaseValue) &&
                   Mathf.Approximately(Value.CurrentValue, other.Value.CurrentValue);
        }
    }
}
