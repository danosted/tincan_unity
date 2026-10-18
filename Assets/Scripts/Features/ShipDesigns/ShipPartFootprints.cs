#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>The blocks the known, anchored parts of a design fill.</summary>
    public static class ShipPartFootprints
    {
        public static ShipPartBlocks Occupancy(ShipDesign design, IShipPartCatalog catalog, ShipFrameShape shape)
        {
            var taken = new ShipPartBlocks();
            foreach (var placement in design.Parts)
            {
                if (!catalog.TryGet(placement.PartId, out var part) || !ShipPartPose.TryGetAnchorPoint(placement.Anchor, shape, out var point)) continue;
                foreach (var centre in ShipPartPose.Blocks(placement, part, point)) taken.Add(centre, placement.InstanceId);
            }

            return taken;
        }
    }
}
