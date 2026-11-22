#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// One island as the layout places it. <see cref="Top"/> is the middle of its grassy top; the rock hangs
    /// <see cref="Depth"/> metres below it and spreads <see cref="Radius"/> metres around it (the edge wanders a little).
    /// A landmass (<see cref="IsLandmass"/>) is a much larger main island, hilly, with several clearings and caves.
    /// </summary>
    public readonly struct SkyIslandSpec
    {
        public readonly SkyIslandId Id;
        public readonly Vector3 Top;
        public readonly float Radius;
        public readonly float Depth;
        public readonly uint ShapeSeed;
        public readonly bool IsLandmass;

        public SkyIslandSpec(SkyIslandId id, Vector3 top, float radius, float depth, uint shapeSeed, bool isLandmass = false)
        {
            Id = id;
            Top = top;
            Radius = radius;
            Depth = depth;
            ShapeSeed = shapeSeed;
            IsLandmass = isLandmass;
        }

        public bool IsSatellite => Id.Index > 0;

        public override string ToString() => $"{(IsLandmass ? "landmass " : "")}{Id} at {Top} r{Radius:0} d{Depth:0}";
    }
}
