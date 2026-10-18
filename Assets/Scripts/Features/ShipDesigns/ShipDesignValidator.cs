#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship.Parts;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: everything wrong with a design, as a list the shipyard can show. The server runs it on every design before
    /// it builds or replicates one. Rules: ids (of parts and frame elements alike) unique, positive and below the next id;
    /// yaws valid; every part held by its anchor (its element there and of the part's kind, a face part's point on its
    /// plank floor); part blocks within the limits and not overlapping; the frame's own rules
    /// (<see cref="ShipFrameValidator"/>); exactly one core part and at most one cairn; the frame in one piece with the
    /// core's anchor, so every part on it is joined too; lift at least mass (the frame weighs too; the spirit lifts when
    /// there is a cairn); some thrust. Unknown parts are reported but take no part in the geometry. Plan:
    /// .docs/plans/frame-only-ships.md.
    /// </summary>
    public sealed class ShipDesignValidator
    {
        public ShipDesignValidation Validate(ShipDesign design, IShipPartCatalog catalog, ShipDesignLimits limits, ShipFrameTuning? frameTuning = null,
            ShipStatsTuning? flight = null)
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

            var seenIds = new HashSet<int>();
            var frame = design.Frame;
            foreach (var placement in design.Parts) CheckIdentity(design, placement.InstanceId, placement.ToString(), seenIds, problems);
            foreach (var node in frame.Nodes) CheckIdentity(design, node.Id, node.ToString(), seenIds, problems);
            foreach (var edge in frame.Edges) CheckIdentity(design, edge.Id, edge.ToString(), seenIds, problems);
            foreach (var face in frame.Faces) CheckIdentity(design, face.Id, face.ToString(), seenIds, problems);
            foreach (var stair in frame.Stairs) CheckIdentity(design, stair.Id, stair.ToString(), seenIds, problems);

            var shape = ShipFrameShape.From(frame);
            ShipFrameValidator.Validate(shape, tuning, limits, problems);

            var anchored = CheckParts(design, catalog, shape, limits, problems);
            CheckCoreAndConnection(anchored, shape, problems);
            CheckFlight(design, catalog, tuning, flight ?? ShipStatsTuning.Default, problems);
            return new ShipDesignValidation(problems);
        }

        /// <summary>Each part's yaw, anchor, blocks and overlaps; returns the known parts their anchors hold.</summary>
        private static List<(ShipPartPlacement Placement, ShipPartDefinition Part)> CheckParts(ShipDesign design, IShipPartCatalog catalog,
            ShipFrameShape shape, ShipDesignLimits limits, List<ShipDesignProblem> problems)
        {
            var anchored = new List<(ShipPartPlacement, ShipPartDefinition)>();
            var taken = new ShipPartBlocks();
            foreach (var placement in design.Parts)
            {
                if (!ShipPartOrientation.IsValid(placement.Orientation))
                {
                    problems.Add(new(ShipDesignProblemKind.InvalidOrientation, placement.InstanceId,
                        $"{placement} has no yaw {placement.Orientation} (0–{ShipPartOrientation.Count - 1})."));
                }

                if (!catalog.TryGet(placement.PartId, out var part))
                {
                    problems.Add(new(ShipDesignProblemKind.UnknownPart, placement.InstanceId,
                        $"{placement}: no loaded part is called \"{placement.PartId}\"; it is kept but not built."));
                    continue;
                }

                if (part.Attach != placement.Anchor.Kind || !ShipPartPose.TryGetAnchorPoint(placement.Anchor, shape, out var point))
                {
                    problems.Add(new(ShipDesignProblemKind.UnanchoredPart, placement.InstanceId, part.Attach != placement.Anchor.Kind
                        ? $"{placement}: a {part.DisplayName} goes on a {part.Attach.ToString().ToLowerInvariant()}."
                        : $"{placement}: there is no {(placement.Anchor.Kind == ShipPartAttach.Node ? "node" : "plank floor")} there to hold it."));
                    continue;
                }

                anchored.Add((placement, part));
                var blocks = ShipPartPose.Blocks(placement, part, point);
                foreach (var block in blocks)
                {
                    if (!limits.Contains(ShipFrameUnits.FromMetres(block.x), ShipFrameUnits.FromMetres(block.y), ShipFrameUnits.FromMetres(block.z)))
                    {
                        problems.Add(new(ShipDesignProblemKind.OutOfBounds, placement.InstanceId,
                            $"{placement} reaches more than {limits.MaxExtent} m from the origin."));
                        break;
                    }

                    if (taken.TryGetOverlap(block, placement.InstanceId, out var other))
                    {
                        problems.Add(new(ShipDesignProblemKind.Overlap, placement.InstanceId, $"{placement} overlaps part #{other}."));
                        break;
                    }
                }

                foreach (var block in blocks) taken.Add(block, placement.InstanceId);
            }

            return anchored;
        }

        private static void CheckFlight(ShipDesign design, IShipPartCatalog catalog, ShipFrameTuning tuning, ShipStatsTuning flight,
            List<ShipDesignProblem> problems)
        {
            var stats = new ShipStatsProcessor().Compute(design, catalog, flight, tuning);
            if (!stats.CanLift)
            {
                problems.Add(new(ShipDesignProblemKind.TooHeavy, 0, stats.Lift > 0f
                    ? $"The spirit lifts {stats.Lift:0} but the ship weighs {stats.Mass:0}: lose weight."
                    : "Nothing lifts the ship: place a cairn for its spirit."));
            }

            if (!stats.HasThrust) problems.Add(new(ShipDesignProblemKind.NoThrust, 0, "Nothing drives the ship: add an engine or a propeller."));
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

        private static void CheckCoreAndConnection(List<(ShipPartPlacement Placement, ShipPartDefinition Part)> anchored, ShipFrameShape shape,
            List<ShipDesignProblem> problems)
        {
            ShipPartPlacement? core = null;
            ShipPartPlacement? cairn = null;
            foreach (var (placement, part) in anchored)
            {
                if (part.IsCairn)
                {
                    if (cairn == null) cairn = placement;
                    else problems.Add(new(ShipDesignProblemKind.ExtraCairn, placement.InstanceId,
                        $"{placement} is a second cairn; a ship has one spirit (#{cairn.InstanceId})."));
                }

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
                problems.Add(new(ShipDesignProblemKind.MissingCore, 0, "The design has no core part (the helm) on its frame."));
                return;
            }

            var pieces = Pieces(shape.Frame);
            int root = pieces.Find(NodeOf(core.Anchor, shape.Frame));
            foreach (var (placement, _) in anchored)
            {
                if (pieces.Find(NodeOf(placement.Anchor, shape.Frame)) == root) continue;
                problems.Add(new(ShipDesignProblemKind.Disconnected, placement.InstanceId, $"{placement} is on a piece of frame apart from the core's."));
            }

            // One problem per loose piece of frame, named by its lowest node.
            var reported = new HashSet<int>();
            foreach (var node in shape.Frame.Nodes)
            {
                int piece = pieces.Find(node.Id);
                if (piece == root || !reported.Add(piece)) continue;
                problems.Add(new(ShipDesignProblemKind.Disconnected, node.Id, $"The frame around {node} is not joined to the core."));
            }
        }

        /// <summary>The node an anchor joins the frame through: its node, or its face's first corner.</summary>
        private static int NodeOf(ShipPartAnchor anchor, ShipFrame frame) =>
            anchor.Kind == ShipPartAttach.Node ? anchor.Element : frame.TryGetFace(anchor.Element, out var face) && face.Nodes.Count > 0 ? face.Nodes[0] : 0;

        /// <summary>Which frame nodes hang together through edges: union-find over node ids.</summary>
        private static UnionFind Pieces(ShipFrame frame)
        {
            var joined = new UnionFind();
            foreach (var node in frame.Nodes) joined.Find(node.Id);
            foreach (var edge in frame.Edges)
            {
                if (frame.TryGetNode(edge.A, out _) && frame.TryGetNode(edge.B, out _)) joined.Union(edge.A, edge.B);
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
