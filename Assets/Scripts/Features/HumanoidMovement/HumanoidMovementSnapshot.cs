#nullable enable
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Wire format of <see cref="HumanoidAuthoritativeState"/>: the server's movement state after it simulated the
    /// input <see cref="Sequence"/>, sent to the owning client every tick. Pose and velocity are in the local space of
    /// <see cref="Platform"/> when <see cref="HasPlatform"/>, else in world space, because the client sees the ship
    /// at a different (interpolated) pose than the server.
    /// </summary>
    public struct HumanoidMovementSnapshot : INetworkSerializable
    {
        public uint Sequence;
        public byte TeleportEpoch;
        public bool HasPlatform;
        public NetworkObjectReference Platform;
        public Vector3 LocalPosition;
        public Vector3 LocalHorizontalVelocity;
        public float VerticalVelocity;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref TeleportEpoch);
            serializer.SerializeValue(ref HasPlatform);
            if (HasPlatform) serializer.SerializeValue(ref Platform);
            serializer.SerializeValue(ref LocalPosition);
            serializer.SerializeValue(ref LocalHorizontalVelocity);
            serializer.SerializeValue(ref VerticalVelocity);
        }
    }
}
