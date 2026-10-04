#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: where a part may go so it is built onto the ship, never floating. A spot is good when the part's cells are free
    /// and at least one of them touches a part already there (shares a face). The aimed cell wins if it is good; otherwise
    /// the nearest good cell on the same level within <see cref="Radius"/> cells, so the ghost snaps to the ship's edge.
    /// The first part of an empty design may go anywhere.
    /// </summary>
    public sealed class ShipyardSnapProcessor
    {
        public const int Radius = 2;

        private static readonly ShipGridCell[] Faces =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
        };

        public bool TrySnap(ShipDesign design, IShipPartCatalog catalog, ShipPartDefinition part, byte orientation,
            ShipGridCell aimed, out ShipGridCell cell)
        {
            var taken = ShipPartFootprints.Occupancy(design, catalog);
            if (taken.Count == 0)
            {
                cell = aimed;
                return true;
            }

            foreach (var candidate in Around(aimed))
            {
                if (!Attaches(taken, ShipPartFootprints.CellsOf(candidate, orientation, part).ToList())) continue;
                cell = candidate;
                return true;
            }

            cell = aimed;
            return false;
        }

        /// <summary>The cells free, and at least one sharing a face with a taken cell.</summary>
        public static bool Attaches(IReadOnlyDictionary<ShipGridCell, int> taken, IReadOnlyCollection<ShipGridCell> cells)
        {
            if (cells.Any(taken.ContainsKey)) return false;
            return cells.Any(c => Faces.Any(face => taken.ContainsKey(c + face)));
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
