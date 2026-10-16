#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Input;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: the shipyard's panels for a mode. Parts mode lists every part, grouped by category (catalog order within
    /// one), the selected one marked. The controls table has the keys on the left and what they do in this mode on the
    /// right; the keys come from the bindings (<c>describe</c>), so a rebound key shows as rebound. Plan:
    /// .docs/plans/ship-frames.md (S2).
    /// </summary>
    public sealed class ShipyardPanelPresenter
    {
        public ShipyardPanelModel Build(ShipyardMode mode, IReadOnlyList<ShipPartDefinition> parts, string? selectedPartId, bool deleteMode,
            ShipyardInputContext controls, InputActionId? cancel, Func<InputActionId?, string> describe)
        {
            var tiles = mode == ShipyardMode.Parts
                ? parts.Select((p, i) => (Part: p, Index: i))
                    .OrderBy(p => CategoryOrder(parts, p.Part.Category)).ThenBy(p => p.Index)
                    .Select(p => new ShipyardPartTile(p.Part.PartId, p.Part.DisplayName, p.Part.Category, p.Part.PartId == selectedPartId))
                    .ToList()
                : new List<ShipyardPartTile>();

            string Keys(params InputActionId?[] actions) => string.Join(" / ", actions.Select(describe));

            var rows = new List<ShipyardControlRow>();
            if (mode == ShipyardMode.Parts)
            {
                rows.Add(new(Keys(controls.Place), deleteMode ? "Delete the highlighted part" : "Place the selected part"));
                rows.Add(new(Keys(controls.Rotate), "Turn the part"));
                rows.Add(new(Keys(controls.LevelUp, controls.LevelDown), "Working level up / down"));
            }
            else
            {
                rows.Add(new(Keys(controls.Place), deleteMode ? "Delete the node, edge or face" : "Pick, add or join a node; split an edge"));
                rows.Add(new($"{Keys(controls.Place)} (drag)", "Move a node; drop it on another to merge"));
                rows.Add(new(Keys(controls.Fill), "Plank or open a face"));
                rows.Add(new(Keys(controls.Extrude), "Extrude: push a wall out, grow a deck edge, raise a story on a floor"));
                rows.Add(new($"{Keys(controls.Height)} + {Keys(controls.Place)} (drag)", "Raise / lower a node"));
                rows.Add(new(Keys(controls.LevelUp, controls.LevelDown), "Working story up / down"));
                rows.Add(new($"{Keys(controls.LookHold)} (click)", "Deselect the node"));
            }

            rows.Add(new(Keys(controls.DeleteMode), deleteMode ? "Back to building" : "Delete mode"));
            rows.Add(new(Keys(controls.Mode), mode == ShipyardMode.Parts ? "Draw the frame" : "Place parts"));
            rows.Add(new(Keys(controls.Undo, controls.Redo), "Undo / redo"));
            rows.Add(new($"{Keys(controls.LookHold)} (hold)", "Look around"));
            rows.Add(new(Keys(controls.Fly), "Fly"));
            rows.Add(new(Keys(controls.Rise), "Down / up"));
            rows.Add(new(Keys(controls.Fast), "Faster"));
            rows.Add(new(Keys(controls.Zoom), "Push the camera"));
            if (cancel != null) rows.Add(new(Keys(cancel), "Menu"));

            return new ShipyardPanelModel(mode, tiles, rows);
        }

        /// <summary>Categories in the order the catalog first names them.</summary>
        private static int CategoryOrder(IReadOnlyList<ShipPartDefinition> parts, string category)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Category == category) return i;
            }

            return parts.Count;
        }
    }
}
