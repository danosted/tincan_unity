#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: everything wrong with a design, as a list the shipyard can show. The server runs it on every design before
    /// it builds or replicates one. Rules: ids (of parts and frame elements alike) unique, positive and below the next id;
    /// orientations valid; every cell within the limits; no two parts in one cell; the frame's own rules
    /// (<see cref="ShipFrameValidator"/>); exactly one core part; every part and every piece of frame joined to the core,
    /// part to part through face-adjacent cells, through frame edges, and part to frame where a part rests on planks or
    /// meets a beam (<see cref="ShipFrameContact"/>); lift at least mass (the frame weighs too); some thrust. Unknown parts
    /// are reported but take no part in the geometry.
    /// </summary>
    public sealed class ShipDesignValidator
    {
        private static readonly ShipGridCell[] Neighbours =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
        };

        public ShipDesignValidation Validate(ShipDesign design, IShipPartCatalog catalog, ShipDesignLimits limits, ShipFrameTuning? frameTuning = null)
        {
            var tuning = frameTuning ?? ShipFrameTuning.Default;
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
                CheckIdentity(design, placement.InstanceId, placement.ToString(), seenIds, problems);

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

            var frame = design.Frame;
            foreach (var node in frame.Nodes) CheckIdentity(design, node.Id, node.ToString(), seenIds, problems);
            foreach (var edge in frame.Edges) CheckIdentity(design, edge.Id, edge.ToString(), seenIds, problems);
            foreach (var face in frame.Faces) CheckIdentity(design, face.Id, face.ToString(), seenIds, problems);

            var shape = ShipFrameShape.From(frame);
            ShipFrameValidator.Validate(shape, tuning, limits, problems);

            CheckCoreAndConnection(known, occupancy, shape, tuning, problems);
            CheckFlight(known, shape, tuning, problems);
            return new ShipDesignValidation(problems);
        }

        private static void CheckFlight(List<(ShipPartPlacement Placement, ShipPartDefinition Part)> known, ShipFrameShape shape, ShipFrameTuning tuning,
            List<ShipDesignProblem> problems)
        {
            var (plankArea, beamLength) = shape.Totals();
            float mass = plankArea * tuning.PlankMassPerSquareMetre + beamLength * tuning.BeamMassPerMetre;
            float lift = 0f, thrust = 0f;
            foreach (var (_, part) in known)
            {
                mass += part.Stats.Mass;
                lift += part.Stats.Lift;
                thrust += part.Stats.Thrust;
            }

            if (lift < mass) problems.Add(new(ShipDesignProblemKind.TooHeavy, 0, $"Lift {lift:0} cannot hold mass {mass:0}: add balloons or lose weight."));
            if (thrust <= 0f) problems.Add(new(ShipDesignProblemKind.NoThrust, 0, "Nothing drives the ship: add an engine."));
        }

        private static void CheckIdentity(ShipDesign design, int id, string what, HashSet<int> seenIds, List<ShipDesignProblem> problems)
        {
            if (id < 1 || id >= design.NextPartInstanceId)
            {
                problems.Add(new(ShipDesignProblemKind.InvalidInstanceId, id,
                    $"{what}: ids run from 1 to below the design's next id ({design.NextPartInstanceId})."));
            }

            if (!seenIds.Add(id))
            {
                problems.Add(new(ShipDesignProblemKind.DuplicateInstanceId, id, $"Two parts or frame elements have the id {id}."));
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
            Dictionary<ShipGridCell, int> occupancy, ShipFrameShape shape, ShipFrameTuning tuning, List<ShipDesignProblem> problems)
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

            var joined = Join(occupancy, shape, tuning);
            int root = joined.Find(core.InstanceId);
            foreach (var (placement, _) in known)
            {
                if (joined.Find(placement.InstanceId) == root) continue;
                problems.Add(new(ShipDesignProblemKind.Disconnected, placement.InstanceId,
                    $"{placement} is not joined to the core."));
            }

            // One problem per loose piece of frame, named by its lowest node.
            var reported = new HashSet<int>();
            foreach (var node in shape.Frame.Nodes)
            {
                int piece = joined.Find(node.Id);
                if (piece == root || !reported.Add(piece)) continue;
                problems.Add(new(ShipDesignProblemKind.Disconnected, node.Id, $"The frame around {node} is not joined to the core."));
            }
        }

        /// <summary>Which parts and frame nodes hang together: union-find over part instance ids and node ids.</summary>
        private static UnionFind Join(Dictionary<ShipGridCell, int> occupancy, ShipFrameShape shape, ShipFrameTuning tuning)
        {
            var joined = new UnionFind();
            foreach (var (cell, owner) in occupancy)
            {
                joined.Find(owner);
                foreach (var step in Neighbours)
                {
                    if (occupancy.TryGetValue(cell + step, out var other)) joined.Union(owner, other);
                }
            }

            var frame = shape.Frame;
            foreach (var node in frame.Nodes) joined.Find(node.Id);
            foreach (var edge in frame.Edges)
            {
                if (frame.TryGetNode(edge.A, out _) && frame.TryGetNode(edge.B, out _)) joined.Union(edge.A, edge.B);
            }

            if (frame.Nodes.Count == 0) return joined;
            foreach (var (cell, owner) in occupancy)
            {
                foreach (int node in ShipFrameContact.TouchedNodes(shape, cell, tuning)) joined.Union(owner, node);
            }

            return joined;
        }

        private sealed class UnionFind
        {
            private readonly Dictionary<int, int> _parent = new();

            public int Find(int id)
            {
                if (!_parent.TryGetValue(id, out int parent))
                {
                    _parent[id] = id;
                    return id;
                }

                if (parent == id) return id;
                int root = Find(parent);
                _parent[id] = root;
                return root;
            }

            public void Union(int a, int b)
            {
                int rootA = Find(a), rootB = Find(b);
                if (rootA == rootB) return;
                if (rootA < rootB) _parent[rootB] = rootA;
                else _parent[rootA] = rootB;
            }
        }
    }
}
