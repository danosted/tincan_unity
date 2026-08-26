using Unity.Netcode;
using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Server-authoritative description of the reference frame used by a player.
    /// </summary>
    public struct PlayerAttachmentState : INetworkSerializable
    {
        public bool IsAttached;
        public NetworkObjectReference Platform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public uint LastProcessedInputSequence;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsAttached);
            serializer.SerializeValue(ref Platform);
            serializer.SerializeValue(ref LocalPosition);
            serializer.SerializeValue(ref LocalRotation);
            serializer.SerializeValue(ref LastProcessedInputSequence);
        }
    }
}