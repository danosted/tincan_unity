#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// One island as the layout places it. <see cref="Top"/> is the middle of its grassy top; the rock hangs
    /// <see cref="Depth"/> metres below it and spreads <see cref="Radius"/> metres around it (the edge wanders a little).
    /// </summary>
    public readonly struct SkyIslandSpec
    {
        public readonly SkyIslandId Id;
        public readonly Vector3 Top;
        public readonly float Radius;
        public readonly float Depth;
        public readonly uint ShapeSeed;

        public SkyIslandSpec(SkyIslandId id, Vector3 top, float radius, float depth, uint shapeSeed)
        {
            Id = id;
            Top = top;
            Radius = radius;
            Depth = depth;
            ShapeSeed = shapeSeed;
        }

        public bool IsSatellite => Id.Index > 0;

        public override string ToString() => $"{Id} at {Top} r{Radius:0} d{Depth:0}";
    }
}
