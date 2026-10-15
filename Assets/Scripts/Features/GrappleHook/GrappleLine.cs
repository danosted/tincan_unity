#nullable enable
using System;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// One replicated line: the holder's player object, and the hook point in the local space of the networked object it
    /// caught on (a ship), or in world space when it caught on something not networked (sky islands are built on every
    /// peer at the same place).
    /// </summary>
    public struct GrappleLine : INetworkSerializable, IEquatable<GrappleLine>
    {
        public NetworkObjectReference Player;
        public NetworkObjectReference Frame;
        public bool HasFrame;
        public Vector3 Point;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Player);
            serializer.SerializeValue(ref HasFrame);
            if (HasFrame) serializer.SerializeValue(ref Frame);
            serializer.SerializeValue(ref Point);
        }

        public bool Equals(GrappleLine other) =>
            Player.Equals(other.Player) && HasFrame == other.HasFrame && (!HasFrame || Frame.Equals(other.Frame)) && Point == other.Point;

        public override bool Equals(object? obj) => obj is GrappleLine other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Player, HasFrame, Point);
    }
}
