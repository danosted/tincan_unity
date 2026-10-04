#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// One change to a design. The shipyard changes designs only through edits, which gives undo (every applied edit
    /// returns its inverse) and is what co-op building will later send to the server to validate and apply.
    /// </summary>
    public sealed class ShipDesignEdit
    {
        private ShipDesignEdit(ShipDesignEditKind kind, string partId, ShipGridCell cell, byte orientation, int instanceId,
            ShipPartPlacement? placement)
        {
            Kind = kind;
            PartId = partId;
            Cell = cell;
            Orientation = orientation;
            InstanceId = instanceId;
            Placement = placement;
        }

        public ShipDesignEditKind Kind { get; }
        public string PartId { get; }
        public ShipGridCell Cell { get; }
        public byte Orientation { get; }
        public int InstanceId { get; }

        /// <summary>The part a <see cref="ShipDesignEditKind.Restore"/> puts back.</summary>
        public ShipPartPlacement? Placement { get; }

        public static ShipDesignEdit Place(string partId, ShipGridCell cell, byte orientation) =>
            new(ShipDesignEditKind.Place, partId, cell, orientation, 0, null);

        public static ShipDesignEdit Remove(int instanceId) =>
            new(ShipDesignEditKind.Remove, string.Empty, default, 0, instanceId, null);

        public static ShipDesignEdit Restore(ShipPartPlacement placement) =>
            new(ShipDesignEditKind.Restore, placement.PartId, placement.Cell, placement.Orientation, placement.InstanceId, placement);

        public override string ToString() => Kind switch
        {
            ShipDesignEditKind.Place => $"Place {PartId} at {Cell} orientation {Orientation}",
            ShipDesignEditKind.Remove => $"Remove #{InstanceId}",
            _ => $"Restore {Placement}",
        };
    }
}
