#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// A stretch of sky where the hazard field is thick: a level circle and the band of altitude it spans. A ship below,
    /// above or beside it is outside.
    /// </summary>
    public readonly struct HazardZone
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float Bottom;
        public readonly float Top;

        public HazardZone(Vector3 center, float radius, float bottom, float top)
        {
            Center = center;
            Radius = radius;
            Bottom = bottom;
            Top = top;
        }

        public bool Contains(Vector3 point)
        {
            float dx = point.x - Center.x, dz = point.z - Center.z;
            return dx * dx + dz * dz <= Radius * Radius && point.y >= Bottom && point.y <= Top;
        }

        public override string ToString() => $"zone at {Center} r{Radius:0} {Bottom:0}..{Top:0} m";
    }
}
