#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The ship grid's geometry. The cell size is a game constant, not part of a design: a file with another cell size
    /// would be a new format version. A cell's walkable surface is its top face.
    /// </summary>
    public static class ShipGrid
    {
        public const float CellSize = 1f;

        /// <summary>The ship-local centre of a cell.</summary>
        public static Vector3 CellCentre(ShipGridCell cell) => new Vector3(cell.X, cell.Y, cell.Z) * CellSize;
    }
}
