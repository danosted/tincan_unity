#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>The grid cells a placed part fills: its footprint, rotated, moved to its cell.</summary>
    public static class ShipPartFootprints
    {
        /// <summary>Which part fills each cell, for the known parts of a design (a later part wins an overlap).</summary>
        public static Dictionary<ShipGridCell, int> Occupancy(ShipDesign design, IShipPartCatalog catalog)
        {
            var taken = new Dictionary<ShipGridCell, int>();
            foreach (var placement in design.Parts)
            {
                if (!catalog.TryGet(placement.PartId, out var part)) continue;
                foreach (var cell in CellsOf(placement, part)) taken[cell] = placement.InstanceId;
            }

            return taken;
        }

        public static IEnumerable<ShipGridCell> CellsOf(ShipPartPlacement placement, ShipPartDefinition part) =>
            CellsOf(placement.Cell, placement.Orientation, part);

        public static IEnumerable<ShipGridCell> CellsOf(ShipGridCell origin, byte orientation, ShipPartDefinition part)
        {
            foreach (var offset in part.Footprint)
            {
                yield return origin + ShipPartOrientation.Rotate(offset, orientation);
            }
        }
    }
}
