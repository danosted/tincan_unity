#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: applies one <see cref="ShipDesignEdit"/> to a design. A place or restore is refused when the part type is
    /// unknown, the orientation invalid, a cell beyond the limits or already taken, or the design full; removing a part
    /// that isn't there is refused too. Disconnecting parts is allowed (the validator reports it), so a builder can
    /// rework a ship in any order. Instance ids are never reused.
    /// </summary>
    public sealed class ShipDesignEditProcessor
    {
        public ShipDesignEditResult Apply(ShipDesign design, ShipDesignEdit edit, IShipPartCatalog catalog, ShipDesignLimits limits) =>
            edit.Kind switch
            {
                ShipDesignEditKind.Place => Place(design, edit, catalog, limits),
                ShipDesignEditKind.Remove => Remove(design, edit.InstanceId),
                ShipDesignEditKind.Restore => Restore(design, edit, catalog, limits),
                _ => ShipDesignEditResult.Refused(design, $"Unknown edit {edit.Kind}."),
            };

        private static ShipDesignEditResult Place(ShipDesign design, ShipDesignEdit edit, IShipPartCatalog catalog, ShipDesignLimits limits)
        {
            int instanceId = Math.Max(design.NextPartInstanceId, design.Parts.Count == 0 ? 1 : design.Parts.Max(p => p.InstanceId) + 1);
            var placement = new ShipPartPlacement(instanceId, edit.PartId, edit.Cell, edit.Orientation);
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
            if (!ShipPartOrientation.IsValid(placement.Orientation)) return $"There is no orientation {placement.Orientation}.";

            var cells = ShipPartFootprints.CellsOf(placement, part).ToList();
            var outside = cells.Where(c => !limits.Contains(c)).Select(c => (ShipGridCell?)c).FirstOrDefault();
            if (outside != null) return $"{outside} is more than {limits.MaxExtent} cells from the origin.";

            var taken = ShipPartFootprints.Occupancy(design, catalog);
            foreach (var cell in cells)
            {
                if (taken.TryGetValue(cell, out var other)) return $"{cell} is taken by part #{other}.";
            }

            return null;
        }
    }
}
