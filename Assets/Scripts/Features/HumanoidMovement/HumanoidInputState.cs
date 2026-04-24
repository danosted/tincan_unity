using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Data structure representing the input state of a humanoid character.
    /// Synchronized over the network to allow remote clients to simulate movement.
    /// </summary>
    public struct HumanoidInputState : INetworkSerializable
    {
        public Vector3 MovementDirection;
        public bool IsJumping;
        public bool IsSprinting;
        public Quaternion LookRotation;

        /// <summary>
        /// A 64-bit mask where each bit represents a specific GameplayInput that is currently active.
        /// </summary>
        public ulong ActiveInputMask;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MovementDirection);
            serializer.SerializeValue(ref IsJumping);
            serializer.SerializeValue(ref IsSprinting);
            serializer.SerializeValue(ref LookRotation);
            serializer.SerializeValue(ref ActiveInputMask);
        }
    }
}
