#nullable enable
using System;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// One point of a ship's route: where to fly (with its altitude) and, when it stands for a point of interest, that
    /// POI's id (0 for a plain waypoint). A POI waypoint follows its POI when the POI moves (a new voyage destination).
    /// </summary>
    public struct RouteWaypoint : IEquatable<RouteWaypoint>, INetworkSerializeByMemcpy
    {
        public Vector3 Position;
        public int PoiId;

        public RouteWaypoint(Vector3 position, int poiId = 0)
        {
            Position = position;
            PoiId = poiId;
        }

        public bool IsPoi => PoiId != 0;

        public bool Equals(RouteWaypoint other) => Position == other.Position && PoiId == other.PoiId;
        public override bool Equals(object? obj) => obj is RouteWaypoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Position, PoiId);
        public override string ToString() => IsPoi ? $"POI #{PoiId} at {Position}" : $"waypoint at {Position}";
    }
}
