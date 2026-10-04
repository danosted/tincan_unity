#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: everything wrong with a design, as a list the shipyard can show. The server runs it on every design before
    /// it builds or replicates one. Rules: ids unique, positive and below the next id; orientations valid; every cell
    /// within the limits; no two parts in one cell; exactly one core part; every part joined to the core through
    /// face-adjacent cells; lift at least mass; some thrust. Unknown parts are reported but take no part in the geometry.
    /// </summary>
    public sealed class ShipDesignValidator
    {
        private static readonly ShipGridCell[] Neighbours =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
        };

        public ShipDesignValidation Validate(ShipDesign design, IShipPartCatalog catalog, ShipDesignLimits limits)
        {
            var problems = new List<ShipDesignProblem>();

            if (design.Name.Length > limits.MaxNameLength)
            {
                problems.Add(new(ShipDesignProblemKind.NameTooLong, 0, $"The name is longer than {limits.MaxNameLength} characters."));
            }

            if (design.Parts.Count > limits.MaxParts)
            {
                problems.Add(new(ShipDesignProblemKind.TooManyParts, 0, $"{design.Parts.Count} parts; at most {limits.MaxParts}."));
            }

            var occupancy = new Dictionary<ShipGridCell, int>();
            var known = new List<(ShipPartPlacement Placement, ShipPartDefinition Part)>();
            var seenIds = new HashSet<int>();

            foreach (var placement in design.Parts)
            {
                CheckIdentity(design, placement, seenIds, problems);

                if (!ShipPartOrientation.IsValid(placement.Orientation))
                {
                    problems.Add(new(ShipDesignProblemKind.InvalidOrientation, placement.InstanceId,
                        $"{placement} has no orientation {placement.Orientation} (0–{ShipPartOrientation.Count - 1})."));
                }

                if (!catalog.TryGet(placement.PartId, out var part))
                {
                    problems.Add(new(ShipDesignProblemKind.UnknownPart, placement.InstanceId,
                        $"{placement}: no loaded part is called \"{placement.PartId}\"; it is kept but not built."));
                    continue;
                }

                known.Add((placement, part));
                Occupy(placement, part, limits, occupancy, problems);
            }

            CheckCoreAndConnection(known, occupancy, problems);
            CheckFlight(known, problems);
            return new ShipDesignValidation(problems);
        }

        private static void CheckFlight(List<(ShipPartPlacement Placement, ShipPartDefinition Part)> known, List<ShipDesignProblem> problems)
        {
            float mass = 0f, lift = 0f, thrust = 0f;
            foreach (var (_, part) in known)
            {
                mass += part.Stats.Mass;
                lift += part.Stats.Lift;
                thrust += part.Stats.Thrust;
            }

            if (lift < mass) problems.Add(new(ShipDesignProblemKind.TooHeavy, 0, $"Lift {lift:0} cannot hold mass {mass:0}: add balloons or lose weight."));
            if (thrust <= 0f) problems.Add(new(ShipDesignProblemKind.NoThrust, 0, "Nothing drives the ship: add an engine."));
        }

        private static void CheckIdentity(ShipDesign design, ShipPartPlacement placement, HashSet<int> seenIds, List<ShipDesignProblem> problems)
        {
            if (placement.InstanceId < 1 || placement.InstanceId >= design.NextPartInstanceId)
            {
                problems.Add(new(ShipDesignProblemKind.InvalidInstanceId, placement.InstanceId,
                    $"{placement}: ids run from 1 to below the design's next id ({design.NextPartInstanceId})."));
            }

            if (!seenIds.Add(placement.InstanceId))
            {
                problems.Add(new(ShipDesignProblemKind.DuplicateInstanceId, placement.InstanceId,
                    $"Two parts have the id {placement.InstanceId}."));
            }
        }

        private static void Occupy(ShipPartPlacement placement, ShipPartDefinition part, ShipDesignLimits limits,
            Dictionary<ShipGridCell, int> occupancy, List<ShipDesignProblem> problems)
        {
            foreach (var cell in ShipPartFootprints.CellsOf(placement, part))
            {
                if (!limits.Contains(cell))
                {
                    problems.Add(new(ShipDesignProblemKind.OutOfBounds, placement.InstanceId,
                        $"{placement} reaches {cell}, more than {limits.MaxExtent} cells from the origin."));
                    return;
                }

                if (occupancy.TryGetValue(cell, out var other))
                {
                    problems.Add(new(ShipDesignProblemKind.Overlap, placement.InstanceId,
                        $"{placement} overlaps part #{other} at {cell}."));
                    continue;
                }

                occupancy.Add(cell, placement.InstanceId);
            }
        }

        private static void CheckCoreAndConnection(List<(ShipPartPlacement Placement, ShipPartDefinition Part)> known,
            Dictionary<ShipGridCell, int> occupancy, List<ShipDesignProblem> problems)
        {
            ShipPartPlacement? core = null;
            foreach (var (placement, part) in known)
            {
                if (!part.IsCore) continue;
                if (core == null)
                {
                    core = placement;
                    continue;
                }

                problems.Add(new(ShipDesignProblemKind.ExtraCore, placement.InstanceId,
                    $"{placement} is a second core part; a ship has exactly one (#{core.InstanceId})."));
            }

            if (core == null)
            {
                problems.Add(new(ShipDesignProblemKind.MissingCore, 0, "The design has no core part (the helm)."));
                return;
            }

            var reached = Reach(core.InstanceId, occupancy);
            foreach (var (placement, _) in known)
            {
                if (reached.Contains(placement.InstanceId)) continue;
                problems.Add(new(ShipDesignProblemKind.Disconnected, placement.InstanceId,
                    $"{placement} is not joined to the core."));
            }
        }

        /// <summary>The parts joined to the start part through face-adjacent occupied cells.</summary>
        private static HashSet<int> Reach(int start, Dictionary<ShipGridCell, int> occupancy)
        {
            var reached = new HashSet<int> { start };
            var visited = new HashSet<ShipGridCell>();
            var frontier = new Queue<ShipGridCell>();
            foreach (var pair in occupancy)
            {
                if (pair.Value != start) continue;
                visited.Add(pair.Key);
                frontier.Enqueue(pair.Key);
            }

            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                foreach (var step in Neighbours)
                {
                    var next = cell + step;
                    if (!occupancy.TryGetValue(next, out var owner) || !visited.Add(next)) continue;
                    reached.Add(owner);
                    frontier.Enqueue(next);
                }
            }

            return reached;
        }
    }
}
