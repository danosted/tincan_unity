#nullable enable
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>Where the cursor points on the grid: the cell a new part goes in, and the cell of the part it is on.</summary>
    public readonly struct ShipyardAim
    {
        public ShipyardAim(ShipGridCell placeCell, ShipGridCell? partCell)
        {
            IsValid = true;
            PlaceCell = placeCell;
            PartCell = partCell;
        }

        public bool IsValid { get; }
        public ShipGridCell PlaceCell { get; }

        /// <summary>The cell of the part under the cursor (what Remove takes), or null over empty grid.</summary>
        public ShipGridCell? PartCell { get; }

        public static ShipyardAim None => default;

        public override string ToString() => IsValid ? $"place {PlaceCell}, on {PartCell?.ToString() ?? "the grid"}" : "nowhere";
    }
}
