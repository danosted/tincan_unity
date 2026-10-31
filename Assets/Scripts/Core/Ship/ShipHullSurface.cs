#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>One planked face of a ship's frame.</summary>
    public readonly struct ShipHullSurface
    {
        public ShipHullSurface(int faceId, int story, bool isWall, Vector3 localPoint, Vector3 localNormal, float area)
        {
            FaceId = faceId;
            Story = story;
            IsWall = isWall;
            LocalPoint = localPoint;
            LocalNormal = localNormal;
            Area = area;
        }

        /// <summary>Stable in the design, so a spot picked on it stays put across rebuilds.</summary>
        public int FaceId { get; }

        /// <summary>The story the face stands on (its lowest node's).</summary>
        public int Story { get; }

        public bool IsWall { get; }

        /// <summary>A point inside the face (the centre of its largest triangle), ship-local.</summary>
        public Vector3 LocalPoint { get; }

        /// <summary>Which way the face looks (up for a floor), ship-local.</summary>
        public Vector3 LocalNormal { get; }

        public float Area { get; }

        public override string ToString() => $"{(IsWall ? "wall" : "floor")} face #{FaceId} on story {Story}";
    }
}
