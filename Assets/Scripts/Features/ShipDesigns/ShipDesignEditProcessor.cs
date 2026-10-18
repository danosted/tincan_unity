#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: applies one <see cref="ShipDesignEdit"/> to a design. A place or restore is refused when the part type is
    /// unknown, the yaw invalid, the anchor does not hold it (a missing element, the wrong kind for the part, a point off
    /// its plank floor), a block is beyond the limits or overlaps another part, or the design is full; removing a part that
    /// isn't there is refused too. Frame edits go to <see cref="ShipFrameEditProcessor"/>; one that takes a part's element
    /// away leaves the part unanchored (the validator reports it), so a builder can rework a ship in any order. Instance
    /// ids are never reused, and new parts skip the ids the frame uses.
    /// </summary>
    public sealed class ShipDesignEditProcessor
    {
        private readonly ShipFrameEditProcessor _frames = new();

        public ShipDesignEditResult Apply(ShipDesign design, ShipDesignEdit edit, IShipPartCatalog catalog, ShipDesignLimits limits) =>
            edit.Kind switch
            {
                ShipDesignEditKind.Place => Place(design, edit, catalog, limits),
                ShipDesignEditKind.Remove => Remove(design, edit.InstanceId),
                ShipDesignEditKind.Restore => Restore(design, edit, catalog, limits),
                ShipDesignEditKind.Frame when edit.FrameEdit != null => _frames.Apply(design, edit.FrameEdit, limits),
                _ => ShipDesignEditResult.Refused(design, $"Unknown edit {edit.Kind}."),
            };

        private static ShipDesignEditResult Place(ShipDesign design, ShipDesignEdit edit, IShipPartCatalog catalog, ShipDesignLimits limits)
        {
            int instanceId = Math.Max(design.NextPartInstanceId, design.IdFloor);
            var placement = new ShipPartPlacement(instanceId, edit.PartId, edit.Anchor, edit.Orientation);
            var refusal = RefusalToAdd(design, placement, catalog, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            var next = design.WithParts(design.Parts.Append(placement), instanceId + 1);
            return ShipDesignEditResult.Done(next, ShipDesignEdit.Remove(instanceId), instanceId);
        }

        private static ShipDesignEditResult Restore(ShipDesign design, ShipDesignEdit edit, IShipPartCatalog catalog, ShipDesignLimits limits)
        {
            var placement = edit.Placement;
            if (placement == null) return ShipDesignEditResult.Refused(design, "Nothing to restore.");
            if (design.TryGetPart(placement.InstanceId, out _))
            {
                return ShipDesignEditResult.Refused(design, $"Part #{placement.InstanceId} is already in the design.");
            }

            var refusal = RefusalToAdd(design, placement, catalog, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            var next = design.WithParts(design.Parts.Append(placement), Math.Max(design.NextPartInstanceId, placement.InstanceId + 1));
            return ShipDesignEditResult.Done(next, ShipDesignEdit.Remove(placement.InstanceId), placement.InstanceId);
        }

        private static ShipDesignEditResult Remove(ShipDesign design, int instanceId)
        {
            if (!design.TryGetPart(instanceId, out var removed))
            {
                return ShipDesignEditResult.Refused(design, $"There is no part #{instanceId}.");
            }

            var next = design.WithParts(design.Parts.Where(p => p.InstanceId != instanceId), design.NextPartInstanceId);
            return ShipDesignEditResult.Done(next, ShipDesignEdit.Restore(removed));
        }

        private static string? RefusalToAdd(ShipDesign design, ShipPartPlacement placement, IShipPartCatalog catalog, ShipDesignLimits limits)
        {
            if (design.Parts.Count >= limits.MaxParts) return $"The design is full ({limits.MaxParts} parts).";
            if (!catalog.TryGet(placement.PartId, out var part)) return $"No loaded part is called \"{placement.PartId}\".";
            if (!ShipPartOrientation.IsValid(placement.Orientation)) return $"There is no yaw {placement.Orientation} (0–3).";
            if (part.Attach != placement.Anchor.Kind) return $"{part.DisplayName} goes on a {part.Attach.ToString().ToLowerInvariant()}, not {placement.Anchor}.";

            var shape = ShipFrameShape.From(design.Frame);
            if (!ShipPartPose.TryGetAnchorPoint(placement.Anchor, shape, out var point))
            {
                return placement.Anchor.Kind == ShipPartAttach.Node
                    ? $"There is no node #{placement.Anchor.Element}."
                    : $"Face #{placement.Anchor.Element} is not a plank floor at ({placement.Anchor.X}, {placement.Anchor.Z}).";
            }

            var taken = ShipPartFootprints.Occupancy(design, catalog, shape);
            foreach (var block in ShipPartPose.Blocks(placement, part, point))
            {
                if (!limits.Contains(ShipFrameUnits.FromMetres(block.x), ShipFrameUnits.FromMetres(block.y), ShipFrameUnits.FromMetres(block.z)))
                {
                    return $"The part reaches more than {limits.MaxExtent} m from the origin.";
                }

                if (taken.TryGetOverlap(block, placement.InstanceId, out var other)) return $"Part #{other} is in the way.";
            }

            return null;
        }
    }
}
