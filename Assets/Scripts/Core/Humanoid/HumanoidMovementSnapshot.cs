#nullable enable
using System;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Humanoid
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
        /// <summary>The server's smoothed input queue depth for this player, in tenths (see <see cref="InputLeadProcessor"/>).</summary>
        public byte QueueDepthTenths;

        public static byte ToTenths(float depth) => (byte)Math.Clamp((int)Math.Round(depth * 10f), 0, 255);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref TeleportEpoch);
            serializer.SerializeValue(ref HasPlatform);
            if (HasPlatform) serializer.SerializeValue(ref Platform);
            serializer.SerializeValue(ref LocalPosition);
            serializer.SerializeValue(ref LocalHorizontalVelocity);
            serializer.SerializeValue(ref VerticalVelocity);
            serializer.SerializeValue(ref QueueDepthTenths);
        }
    }
}
