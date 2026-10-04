#nullable enable
using System.Linq;
using TinCan.Core.Ship.Parts;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>Where a placed part's prefab goes in ship space, and the space a design fills.</summary>
    public static class ShipPartPose
    {
        public static Quaternion LocalRotation(ShipPartPlacement placement) => ShipPartOrientation.ToRotation(placement.Orientation);

        public static Vector3 LocalPosition(ShipPartPlacement placement, ShipPartDefinition part) =>
            ShipGrid.CellCentre(placement.Cell) + LocalRotation(placement) * part.PivotOffset;

        /// <summary>The ship-local box around every known part's cells, or false for a design with none.</summary>
        public static bool TryGetBounds(ShipDesign design, IShipPartCatalog catalog, out Bounds bounds)
        {
            var cells = design.Parts
                .SelectMany(p => catalog.TryGet(p.PartId, out var part) ? ShipPartFootprints.CellsOf(p, part) : Enumerable.Empty<ShipGridCell>())
                .ToList();
            bounds = default;
            if (cells.Count == 0) return false;

            var half = Vector3.one * (ShipGrid.CellSize * 0.5f);
            var min = new Vector3(cells.Min(c => c.X), cells.Min(c => c.Y), cells.Min(c => c.Z)) * ShipGrid.CellSize - half;
            var max = new Vector3(cells.Max(c => c.X), cells.Max(c => c.Y), cells.Max(c => c.Z)) * ShipGrid.CellSize + half;
            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }
    }
}
