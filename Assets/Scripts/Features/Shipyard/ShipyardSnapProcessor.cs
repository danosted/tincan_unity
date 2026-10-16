#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: where a part may go so it is built onto the ship, never floating. A spot is good when the part's cells are free
    /// and at least one of them touches the ship: shares a face with a part already there, or rests on a plank floor of
    /// the frame or meets one of its beams (<see cref="ShipFrameContact"/>, the validator's rule). The aimed cell wins if it
    /// is good; otherwise the nearest good cell on the same level within <see cref="Radius"/> cells, so the ghost snaps to
    /// the ship's edge. The first part of a design with neither parts nor frame may go anywhere.
    /// </summary>
    public sealed class ShipyardSnapProcessor
    {
        public const int Radius = 2;

        private static readonly ShipGridCell[] Faces =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
        };

        public bool TrySnap(ShipDesign design, IShipPartCatalog catalog, ShipPartDefinition part, byte orientation,
            ShipGridCell aimed, out ShipGridCell cell, ShipFrameTuning? frameTuning = null)
        {
            var taken = ShipPartFootprints.Occupancy(design, catalog);
            var shape = ShipFrameShape.From(design.Frame);
            if (taken.Count == 0 && design.Frame.Nodes.Count == 0)
            {
                cell = aimed;
                return true;
            }

            var tuning = frameTuning ?? ShipFrameTuning.Default;
            foreach (var candidate in Around(aimed))
            {
                if (!Attaches(taken, shape, ShipPartFootprints.CellsOf(candidate, orientation, part).ToList(), tuning)) continue;
                cell = candidate;
                return true;
            }

            cell = aimed;
            return false;
        }

        /// <summary>The cells free, and at least one sharing a face with a taken cell, resting on the frame's planks or meeting a beam.</summary>
        public static bool Attaches(IReadOnlyDictionary<ShipGridCell, int> taken, ShipFrameShape frame, IReadOnlyCollection<ShipGridCell> cells,
            ShipFrameTuning tuning)
        {
            if (cells.Any(taken.ContainsKey)) return false;
            if (cells.Any(c => Faces.Any(face => taken.ContainsKey(c + face)))) return true;
            return frame.Frame.Nodes.Count > 0 && cells.Any(c => ShipFrameContact.TouchedNodes(frame, c, tuning).Any());
        }

        /// <summary>The aimed cell, then the cells on its level within the radius, nearest first (ties in a fixed order).</summary>
        private static IEnumerable<ShipGridCell> Around(ShipGridCell aimed)
        {
            var cells = new List<(int Distance, int X, int Z)>();
            for (int x = -Radius; x <= Radius; x++)
            for (int z = -Radius; z <= Radius; z++)
                cells.Add((x * x + z * z, x, z));

            return cells.OrderBy(c => c.Distance).ThenBy(c => c.X).ThenBy(c => c.Z)
                .Select(c => new ShipGridCell(aimed.X + c.X, aimed.Y, aimed.Z + c.Z));
        }
    }
}
