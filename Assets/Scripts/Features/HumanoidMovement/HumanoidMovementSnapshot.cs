using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Server-authoritative state used to reconcile an owning client's predicted humanoid movement.
    /// </summary>
    public struct HumanoidMovementSnapshot : INetworkSerializable
    {
        public uint LastProcessedInputSequence;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 HorizontalVelocity;
        public float VerticalVelocity;
        public ulong PreviousInputMask;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref LastProcessedInputSequence);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref HorizontalVelocity);
            serializer.SerializeValue(ref VerticalVelocity);
            serializer.SerializeValue(ref PreviousInputMask);
        }
    }
}
