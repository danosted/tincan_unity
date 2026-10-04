#nullable enable
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>Where the cursor points: the cell on the working level a new part would go in, and the part under it.</summary>
    public readonly struct ShipyardAim
    {
        public ShipyardAim(ShipGridCell placeCell, ShipGridCell? partCell)
        {
            IsValid = true;
            PlaceCell = placeCell;
            PartCell = partCell;
        }

        private ShipyardAim(ShipGridCell? partCell)
        {
            IsValid = false;
            PlaceCell = default;
            PartCell = partCell;
        }

        /// <summary>True when the cursor is over the working level's floor.</summary>
        public bool IsValid { get; }
        public ShipGridCell PlaceCell { get; }

        /// <summary>The cell of the part under the cursor (what Remove takes), or null over empty space.</summary>
        public ShipGridCell? PartCell { get; }

        public static ShipyardAim None => default;

        public static ShipyardAim Nowhere(ShipGridCell? partCell) => new(partCell);

        public override string ToString() =>
            $"{(IsValid ? $"place {PlaceCell}" : "place nowhere")}, on {PartCell?.ToString() ?? "nothing"}";
    }
}
