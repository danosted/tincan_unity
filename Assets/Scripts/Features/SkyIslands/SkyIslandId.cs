#nullable enable
using System;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Which island this is: the layout seed, its cell and its index in the cell (0 the main island, then satellites).
    /// The same on every peer, so a rebuild keeps the islands a new layout still wants.
    /// </summary>
    public readonly struct SkyIslandId : IEquatable<SkyIslandId>
    {
        public readonly int Seed;
        public readonly int CellX;
        public readonly int CellZ;
        public readonly int Index;

        public SkyIslandId(int seed, int cellX, int cellZ, int index)
        {
            Seed = seed;
            CellX = cellX;
            CellZ = cellZ;
            Index = index;
        }

        public bool Equals(SkyIslandId other) =>
            Seed == other.Seed && CellX == other.CellX && CellZ == other.CellZ && Index == other.Index;

        public override bool Equals(object? obj) => obj is SkyIslandId other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Seed, CellX, CellZ, Index);

        public override string ToString() => $"{CellX},{CellZ}#{Index}";
    }
}
