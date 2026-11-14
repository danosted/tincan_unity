#nullable enable
using System;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// A hole in the cloud sea, as the server opened it: where it starts, how it drifts, how big it gets, and when it
    /// opens and closes on the shared clock (<c>ITimeService.Time</c>). Replicated once; every peer works out where it is
    /// and how open it is at any moment (<see cref="CloudHoleProcessor"/>), so nothing is sent per frame.
    /// </summary>
    public struct CloudHole : INetworkSerializable, IEquatable<CloudHole>
    {
        /// <summary>Close time of a hole that stays open.</summary>
        public const float Never = float.MaxValue;

        public Vector2 Centre;
        public Vector2 Drift;
        public float Radius;
        public float OpenedAt;
        public float OpenSeconds;
        public float ClosesAt;
        public float CloseSeconds;

        public CloudHole(Vector2 centre, float radius, float openedAt, float openSeconds, float closesAt = Never,
            float closeSeconds = 0f, Vector2 drift = default)
        {
            Centre = centre;
            Drift = drift;
            Radius = radius;
            OpenedAt = openedAt;
            OpenSeconds = openSeconds;
            ClosesAt = closesAt;
            CloseSeconds = closeSeconds;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Centre);
            serializer.SerializeValue(ref Drift);
            serializer.SerializeValue(ref Radius);
            serializer.SerializeValue(ref OpenedAt);
            serializer.SerializeValue(ref OpenSeconds);
            serializer.SerializeValue(ref ClosesAt);
            serializer.SerializeValue(ref CloseSeconds);
        }

        public bool Equals(CloudHole other) =>
            Centre == other.Centre && Drift == other.Drift && Radius.Equals(other.Radius) &&
            OpenedAt.Equals(other.OpenedAt) && OpenSeconds.Equals(other.OpenSeconds) &&
            ClosesAt.Equals(other.ClosesAt) && CloseSeconds.Equals(other.CloseSeconds);

        public override bool Equals(object? obj) => obj is CloudHole other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Centre, Drift, Radius, OpenedAt, OpenSeconds, ClosesAt, CloseSeconds);
    }
}
