#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// One change to a design. The shipyard changes designs only through edits, which gives undo (every applied edit
    /// returns its inverse) and is what co-op building will later send to the server to validate and apply.
    /// </summary>
    public sealed class ShipDesignEdit
    {
        private ShipDesignEdit(ShipDesignEditKind kind, string partId, ShipPartAnchor anchor, byte orientation, int instanceId,
            ShipPartPlacement? placement, ShipFrameEdit? frameEdit = null)
        {
            Kind = kind;
            PartId = partId;
            Anchor = anchor;
            Orientation = orientation;
            InstanceId = instanceId;
            Placement = placement;
            FrameEdit = frameEdit;
        }

        public ShipDesignEditKind Kind { get; }
        public string PartId { get; }
        public ShipPartAnchor Anchor { get; }
        public byte Orientation { get; }
        public int InstanceId { get; }

        /// <summary>The part a <see cref="ShipDesignEditKind.Restore"/> puts back.</summary>
        public ShipPartPlacement? Placement { get; }

        /// <summary>The frame change a <see cref="ShipDesignEditKind.Frame"/> makes.</summary>
        public ShipFrameEdit? FrameEdit { get; }

        public static ShipDesignEdit Place(string partId, ShipPartAnchor anchor, byte orientation) =>
            new(ShipDesignEditKind.Place, partId, anchor, orientation, 0, null);

        public static ShipDesignEdit Remove(int instanceId) =>
            new(ShipDesignEditKind.Remove, string.Empty, default, 0, instanceId, null);

        public static ShipDesignEdit Restore(ShipPartPlacement placement) =>
            new(ShipDesignEditKind.Restore, placement.PartId, placement.Anchor, placement.Orientation, placement.InstanceId, placement);

        public static ShipDesignEdit Frame(ShipFrameEdit edit) => new(ShipDesignEditKind.Frame, string.Empty, default, 0, 0, null, edit);

        public override string ToString() => Kind switch
        {
            ShipDesignEditKind.Place => $"Place {PartId} {Anchor} turned {Orientation}",
            ShipDesignEditKind.Remove => $"Remove #{InstanceId}",
            ShipDesignEditKind.Frame => $"Frame: {FrameEdit}",
            _ => $"Restore {Placement}",
        };
    }
}
