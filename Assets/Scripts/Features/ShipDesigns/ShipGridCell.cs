#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A cell on a ship's grid, in whole cells from the ship's origin. Cell (0,0,0) is centred on the ship root; one cell
    /// is <see cref="ShipGrid.CellSize"/> metres. Integers keep designs exact and the same on every peer.
    /// </summary>
    public readonly struct ShipGridCell : IEquatable<ShipGridCell>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ShipGridCell(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static ShipGridCell From(Vector3Int cell) => new(cell.x, cell.y, cell.z);

        public Vector3Int ToVector3Int() => new(X, Y, Z);

        public static ShipGridCell operator +(ShipGridCell a, ShipGridCell b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static bool operator ==(ShipGridCell a, ShipGridCell b) => a.Equals(b);

        public static bool operator !=(ShipGridCell a, ShipGridCell b) => !a.Equals(b);

        public bool Equals(ShipGridCell other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object? obj) => obj is ShipGridCell other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
