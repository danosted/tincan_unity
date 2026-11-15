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
    /// A rift is a hole open along a line: <see cref="Reach"/> runs from its centre to one end (zero for a round hole).
    /// </summary>
    public struct CloudHole : INetworkSerializable, IEquatable<CloudHole>
    {
        /// <summary>Close time of a hole that stays open.</summary>
        public const float Never = float.MaxValue;

        public Vector2 Centre;
        public Vector2 Drift;
        public float Radius;
        /// <summary>From the centre to one end of a rift, in metres; zero for a round hole.</summary>
        public Vector2 Reach;
        /// <summary>How much a rift's width wanders along it, 0 (straight edges) .. 1.</summary>
        public float Ragged;
        public float OpenedAt;
        public float OpenSeconds;
        public float ClosesAt;
        public float CloseSeconds;

        public CloudHole(Vector2 centre, float radius, float openedAt, float openSeconds, float closesAt = Never,
            float closeSeconds = 0f, Vector2 drift = default, Vector2 reach = default, float ragged = 0f)
        {
            Centre = centre;
            Drift = drift;
            Radius = radius;
            Reach = reach;
            Ragged = ragged;
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
            serializer.SerializeValue(ref Reach);
            serializer.SerializeValue(ref Ragged);
            serializer.SerializeValue(ref OpenedAt);
            serializer.SerializeValue(ref OpenSeconds);
            serializer.SerializeValue(ref ClosesAt);
            serializer.SerializeValue(ref CloseSeconds);
        }

        public bool Equals(CloudHole other) =>
            Centre == other.Centre && Drift == other.Drift && Radius.Equals(other.Radius) && Reach == other.Reach && Ragged.Equals(other.Ragged) &&
            OpenedAt.Equals(other.OpenedAt) && OpenSeconds.Equals(other.OpenSeconds) &&
            ClosesAt.Equals(other.ClosesAt) && CloseSeconds.Equals(other.CloseSeconds);

        public override bool Equals(object? obj) => obj is CloudHole other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Centre, Drift, Radius, Reach, OpenedAt, OpenSeconds, ClosesAt, HashCode.Combine(CloseSeconds, Ragged));
    }
}
