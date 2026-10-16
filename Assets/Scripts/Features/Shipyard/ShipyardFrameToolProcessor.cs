#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: the shipyard's frame tools, turning cursor gestures into frame edits (<see cref="ShipFrameEditProcessor"/>).
    /// Each gesture is one undo step, however many edits it makes. Clicks (Place):
    /// <list type="bullet">
    /// <item>on a node: select it; on the selected node: deselect; on another node with one selected: join them with an
    /// edge (or, when both are corners of one face, split that face). A new edge that closes a loop on the story planks it
    /// as a new face;</item>
    /// <item>on an edge: put a new node on it there (joined to the selected node, if any);</item>
    /// <item>anywhere else on the floor plane: a new node there, joined to the selected node, if any. A design without a
    /// frame gets its first story at the working level first.</item>
    /// </list>
    /// Dragging a node moves it on its story's plane; dropping it on another node merges it into that one. Positions snap
    /// to <see cref="ShipyardFrameToolSettings.Snap"/> frame units; an Alt-drag changes its height instead
    /// (<see cref="SetHeight"/>). New nodes go on the working story (<see cref="ShipyardFrameToolSettings.Story"/>), stories
    /// being added up to it; joining nodes on adjacent stories makes a post, and an edge that closes a wall between two
    /// stories planks it too. Fill toggles a face between planks and open; Drift raises or lowers the selected node;
    /// Delete removes the node, edge or face under the cursor. Plan:
    /// .docs/plans/ship-frames.md (S2).
    /// </summary>
    public sealed class ShipyardFrameToolProcessor
    {
        private readonly ShipFrameEditProcessor _edits = new();

        public ShipyardFrameToolResult Click(ShipDesign design, ShipyardFramePick pick, int? selected, ShipyardFrameToolSettings settings)
        {
            var frame = design.Frame;
            switch (pick.Kind)
            {
                case ShipyardFramePickKind.Node:
                    return ClickNode(design, pick.Id, selected, settings);
                case ShipyardFramePickKind.Edge when frame.TryGetEdge(pick.Id, out var edge):
                    return ClickEdge(design, edge, pick.Point, selected, settings);
            }

            if (!pick.OnPlane) return Unchanged(selected, "Aim at the build floor to add a node.");

            var (x, z) = Snap(pick.PlanePoint, settings.Snap);
            var existing = frame.Nodes.Where(n => n.Story == settings.Story && n.X == x && n.Z == z).Select(n => (int?)n.Id).FirstOrDefault();
            if (existing != null) return ClickNode(design, existing.Value, selected, settings);

            return Run(design, selected, settings.Limits, steps =>
            {
                if (steps.Current.Frame.StoryHeights.Count == 0)
                {
                    var first = new ShipFrame(settings.NewBase, new[] { settings.NewStoryHeight }, Array.Empty<ShipFrameNode>(),
                        Array.Empty<ShipFrameEdge>(), Array.Empty<ShipFrameFace>());
                    if (!steps.Apply(ShipFrameEdit.Restore(first))) return false;
                }

                while (steps.Current.Frame.StoryHeights.Count <= settings.Story)
                {
                    if (!steps.Apply(ShipFrameEdit.AddStory(settings.NewStoryHeight))) return false;
                }

                if (!steps.Apply(ShipFrameEdit.AddNode(settings.Story, x, 0, z))) return false;
                int node = steps.Added;
                if (selected is { } from && steps.Current.Frame.TryGetNode(from, out _) && !Connect(steps, from, node)) return false;
                steps.Select = node;
                return true;
            }, "Node added.");
        }

        /// <summary>The design with the node at <paramref name="target"/> (ship space, snapped; for showing a drag), or null when it would not move.</summary>
        public ShipDesign? DragPreview(ShipDesign design, int node, Vector3 target, ShipyardFrameToolSettings settings)
        {
            if (!design.Frame.TryGetNode(node, out var moving)) return null;
            var (x, z) = Snap(target, settings.Snap);
            if (x == moving.X && z == moving.Z) return null;

            var result = _edits.Apply(design, ShipFrameEdit.MoveNode(node, x, moving.Y, z), settings.Limits);
            return result.Applied ? result.Design : null;
        }

        /// <summary>
        /// Ends a drag: merge into the node it was dropped on (<paramref name="pick"/>, which leaves the dragged node out), else
        /// move it to <paramref name="target"/> (where the drag would put it, ship space), snapped.
        /// </summary>
        public ShipyardFrameToolResult Drop(ShipDesign design, int node, ShipyardFramePick pick, Vector3 target, ShipyardFrameToolSettings settings)
        {
            if (!design.Frame.TryGetNode(node, out var moving)) return Unchanged(null, $"There is no node #{node}.");

            if (pick.Kind == ShipyardFramePickKind.Node && pick.Id != node)
            {
                int into = pick.Id;
                return Run(design, node, settings.Limits, steps =>
                {
                    if (!steps.Apply(ShipFrameEdit.MergeNodes(into, node))) return false;
                    steps.Select = into;
                    return true;
                }, "Nodes merged.");
            }

            var (x, z) = Snap(target, settings.Snap);
            if (x == moving.X && z == moving.Z) return Unchanged(node, string.Empty);

            return Run(design, node, settings.Limits, steps => steps.Apply(ShipFrameEdit.MoveNode(node, x, moving.Y, z)), "Node moved.");
        }

        /// <summary>The design with the node at this drift (for showing an Alt-drag), or null when it would not change.</summary>
        public ShipDesign? HeightPreview(ShipDesign design, int node, int drift, ShipDesignLimits limits)
        {
            if (!design.Frame.TryGetNode(node, out var moving) || moving.Y == drift) return null;
            var result = _edits.Apply(design, ShipFrameEdit.MoveNode(node, moving.X, drift, moving.Z), limits);
            return result.Applied ? result.Design : null;
        }

        /// <summary>Ends an Alt-drag: the node's drift above its story's floor becomes <paramref name="drift"/> frame units.</summary>
        public ShipyardFrameToolResult SetHeight(ShipDesign design, int node, int drift, ShipDesignLimits limits)
        {
            if (!design.Frame.TryGetNode(node, out var moving)) return Unchanged(null, $"There is no node #{node}.");
            if (moving.Y == drift) return Unchanged(node, string.Empty);

            return Run(design, node, limits, steps => steps.Apply(ShipFrameEdit.MoveNode(node, moving.X, drift, moving.Z)),
                $"Node #{node} drifts {ShipFrameUnits.ToMetres(drift):0.0} m from its story's floor.");
        }

        /// <summary>
        /// Grows the ship from what is under the cursor, <paramref name="length"/> frame units at a time:
        /// <list type="bullet">
        /// <item>a wall face moves out (away from the ship's middle, seen from above), leaving a floor below, a deck above
        /// and walls at its sides; the old face opens, so the ship grows through it, and a railing on its floor edge moves out;</item>
        /// <item>an outer floor edge (one floor on one side) grows a strip of floor outwards; its railing moves out;</item>
        /// <item>a floor face gets a story on it: nodes straight above (reusing any already there), posts, walls round it and a
        /// floor on top; railings along its sides come off (they would be under the walls).</item>
        /// </list>
        /// The faces it makes are open (fill them with Fill); the wall it pushes out keeps its fill. One undo step.
        /// </summary>
        public ShipyardFrameToolResult Extrude(ShipDesign design, ShipyardFramePick pick, int? selected, int length, ShipyardFrameToolSettings settings)
        {
            var frame = design.Frame;
            var shape = ShipFrameShape.From(frame);
            switch (pick.Kind)
            {
                case ShipyardFramePickKind.Edge when frame.TryGetEdge(pick.Id, out var edge):
                    return ExtrudeEdge(design, shape, edge, selected, length, settings);
                case ShipyardFramePickKind.Face:
                    var face = shape.Faces.FirstOrDefault(f => f.Face.Id == pick.Id);
                    if (face.Face == null) break;
                    return face.IsWall ? ExtrudeWall(design, shape, face, selected, length, settings) : RaiseStory(design, face, selected, settings);
            }

            return Unchanged(selected, "Point at a wall, a floor or an outer edge to extrude it.");
        }

        private ShipyardFrameToolResult ExtrudeWall(ShipDesign design, ShipFrameShape shape, ShipFrameShape.FaceShape wall, int? selected, int length,
            ShipyardFrameToolSettings settings)
        {
            var frame = design.Frame;
            var outward = new Vector3(wall.Facing.x, 0f, wall.Facing.z);
            if (outward.sqrMagnitude < 1e-4f) return Unchanged(selected, "That wall lies flat; extrude a floor instead.");
            outward.Normalize();
            var middle = Centre(frame.Nodes.Select(frame.Position));
            if (Vector3.Dot(outward, Centre(wall.Corners) - middle) < 0f) outward = -outward;

            int dx = Mathf.RoundToInt(outward.x * length), dz = Mathf.RoundToInt(outward.z * length);
            var loop = wall.Face.Nodes;
            return Run(design, selected, settings.Limits, steps =>
            {
                var moved = new Dictionary<int, int>();
                foreach (int id in loop)
                {
                    frame.TryGetNode(id, out var node);
                    if (!steps.Apply(ShipFrameEdit.AddNode(node.Story, node.X + dx, node.Y, node.Z + dz))) return false;
                    moved[id] = steps.Added;
                    if (!steps.Apply(ShipFrameEdit.AddEdge(id, moved[id]))) return false;
                }

                for (int i = 0; i < loop.Count; i++)
                {
                    int p = loop[i], q = loop[(i + 1) % loop.Count];
                    if (!steps.Apply(ShipFrameEdit.AddEdge(moved[p], moved[q]))) return false;
                    MoveRailing(steps, p, q, steps.Added);
                    steps.TryApply(ShipFrameEdit.AddFace(new[] { p, q, moved[q], moved[p] }, ShipFrameFill.Open));
                }

                steps.TryApply(ShipFrameEdit.AddFace(loop.Select(id => moved[id]).ToList(), wall.Face.Fill));
                steps.TryApply(ShipFrameEdit.SetFaceFill(wall.Face.Id, ShipFrameFill.Open));
                return true;
            }, "Wall pushed out.");
        }

        private ShipyardFrameToolResult ExtrudeEdge(ShipDesign design, ShipFrameShape shape, ShipFrameEdge edge, int? selected, int length,
            ShipyardFrameToolSettings settings)
        {
            var frame = design.Frame;
            frame.TryGetNode(edge.A, out var a);
            frame.TryGetNode(edge.B, out var b);
            if (a.Story != b.Story) return Unchanged(selected, "A post does not extrude; point at a wall or a floor edge.");

            var floors = shape.PlankFloors.Concat(shape.Faces.Where(f => !f.IsWall && f.Face.Fill == ShipFrameFill.Open))
                .Where(f => f.Face.HasSide(edge.A, edge.B)).ToList();
            if (floors.Count != 1) return Unchanged(selected, "Only an outer edge (a floor on one side only) extrudes.");

            var along = frame.Position(b) - frame.Position(a);
            var outward = new Vector3(along.z, 0f, -along.x).normalized;
            if (Vector3.Dot(outward, (frame.Position(a) + frame.Position(b)) * 0.5f - Centre(floors[0].Corners)) < 0f) outward = -outward;
            int dx = Mathf.RoundToInt(outward.x * length), dz = Mathf.RoundToInt(outward.z * length);

            return Run(design, selected, settings.Limits, steps =>
            {
                if (!steps.Apply(ShipFrameEdit.AddNode(a.Story, a.X + dx, a.Y, a.Z + dz))) return false;
                int a2 = steps.Added;
                if (!steps.Apply(ShipFrameEdit.AddNode(b.Story, b.X + dx, b.Y, b.Z + dz))) return false;
                int b2 = steps.Added;
                if (!steps.Apply(ShipFrameEdit.AddEdge(edge.A, a2)) || !steps.Apply(ShipFrameEdit.AddEdge(edge.B, b2))) return false;
                if (!steps.Apply(ShipFrameEdit.AddEdge(a2, b2))) return false;
                MoveRailing(steps, edge.A, edge.B, steps.Added);
                return steps.Apply(ShipFrameEdit.AddFace(new[] { edge.A, edge.B, b2, a2 }, ShipFrameFill.Open));
            }, "Deck extended.");
        }

        private ShipyardFrameToolResult RaiseStory(ShipDesign design, ShipFrameShape.FaceShape floor, int? selected, ShipyardFrameToolSettings settings)
        {
            var frame = design.Frame;
            var loop = floor.Face.Nodes;
            int story = floor.Story + 1;
            if (story >= settings.Limits.MaxStories) return Unchanged(selected, $"A frame has at most {settings.Limits.MaxStories} stories.");

            return Run(design, selected, settings.Limits, steps =>
            {
                while (steps.Current.Frame.StoryHeights.Count <= story)
                {
                    if (!steps.Apply(ShipFrameEdit.AddStory(settings.NewStoryHeight))) return false;
                }

                var above = new Dictionary<int, int>();
                foreach (int id in loop)
                {
                    frame.TryGetNode(id, out var node);
                    var existing = steps.Current.Frame.Nodes.Where(n => n.Story == story && n.X == node.X && n.Z == node.Z).Select(n => (int?)n.Id).FirstOrDefault();
                    if (existing == null)
                    {
                        if (!steps.Apply(ShipFrameEdit.AddNode(story, node.X, 0, node.Z))) return false;
                        existing = steps.Added;
                    }

                    above[id] = existing.Value;
                    if (!steps.Current.Frame.TryGetEdgeBetween(id, above[id], out _) && !steps.Apply(ShipFrameEdit.AddEdge(id, above[id]))) return false;
                }

                for (int i = 0; i < loop.Count; i++)
                {
                    int p = loop[i], q = loop[(i + 1) % loop.Count];
                    if (!steps.Current.Frame.TryGetEdgeBetween(above[p], above[q], out _) && !steps.Apply(ShipFrameEdit.AddEdge(above[p], above[q]))) return false;
                    // A railing would end up under the new wall: it goes.
                    if (steps.Current.Frame.TryGetEdgeBetween(p, q, out var under) && under.Railing) steps.TryApply(ShipFrameEdit.SetRailing(under.Id, false));
                    steps.TryApply(ShipFrameEdit.AddFace(new[] { p, q, above[q], above[p] }, ShipFrameFill.Open));
                }

                steps.TryApply(ShipFrameEdit.AddFace(loop.Select(id => above[id]).ToList(), ShipFrameFill.Open));
                return true;
            }, $"A story raised on {floor.Face}.");
        }

        /// <summary>A railing on the floor edge p-q moves to the new edge beside it.</summary>
        private static void MoveRailing(Steps steps, int p, int q, int newEdge)
        {
            if (!steps.Current.Frame.TryGetEdgeBetween(p, q, out var old) || !old.Railing) return;
            steps.TryApply(ShipFrameEdit.SetRailing(old.Id, false));
            steps.TryApply(ShipFrameEdit.SetRailing(newEdge, true));
        }

        private static Vector3 Centre(IEnumerable<Vector3> points)
        {
            var sum = Vector3.zero;
            int count = 0;
            foreach (var point in points)
            {
                sum += point;
                count++;
            }

            return count == 0 ? Vector3.zero : sum / count;
        }

        public ShipyardFrameToolResult ToggleFill(ShipDesign design, ShipyardFramePick pick, int? selected, ShipDesignLimits limits)
        {
            if (pick.Kind != ShipyardFramePickKind.Face || !design.Frame.TryGetFace(pick.Id, out var face))
            {
                return Unchanged(selected, "Point at a face to plank or open it.");
            }

            var fill = face.Fill == ShipFrameFill.Planks ? ShipFrameFill.Open : ShipFrameFill.Planks;
            return Run(design, selected, limits, steps => steps.Apply(ShipFrameEdit.SetFaceFill(face.Id, fill)),
                fill == ShipFrameFill.Planks ? "Face planked." : "Face opened.");
        }

        /// <summary>Raises (or lowers) the selected node by <paramref name="units"/> frame units.</summary>
        public ShipyardFrameToolResult Drift(ShipDesign design, int? selected, int units, ShipDesignLimits limits)
        {
            if (selected is not { } id || !design.Frame.TryGetNode(id, out var node)) return Unchanged(selected, "Select a node to raise or lower it.");

            return Run(design, id, limits, steps => steps.Apply(ShipFrameEdit.MoveNode(id, node.X, node.Y + units, node.Z)),
                $"Node #{id} drifts {ShipFrameUnits.ToMetres(node.Y + units):0.0} m from its story's floor.");
        }

        public ShipyardFrameToolResult Delete(ShipDesign design, ShipyardFramePick pick, int? selected, ShipDesignLimits limits)
        {
            var edit = pick.Kind switch
            {
                ShipyardFramePickKind.Node => ShipFrameEdit.RemoveNode(pick.Id),
                ShipyardFramePickKind.Edge => ShipFrameEdit.RemoveEdge(pick.Id),
                ShipyardFramePickKind.Face => ShipFrameEdit.RemoveFace(pick.Id),
                _ => null,
            };
            if (edit == null) return Unchanged(selected, "Point at a node, edge or face to delete it.");

            int? after = pick.Kind == ShipyardFramePickKind.Node && selected == pick.Id ? null : selected;
            return Run(design, after, limits, steps => steps.Apply(edit), $"{pick.Kind} #{pick.Id} deleted.");
        }

        private ShipyardFrameToolResult ClickNode(ShipDesign design, int node, int? selected, ShipyardFrameToolSettings settings)
        {
            if (selected == null || !design.Frame.TryGetNode(selected.Value, out _)) return Unchanged(node, $"Node #{node} selected.");
            if (selected == node) return Unchanged(null, string.Empty);
            if (design.Frame.TryGetEdgeBetween(selected.Value, node, out _)) return Unchanged(node, $"Node #{node} selected.");

            int from = selected.Value;
            bool splits = SharedFace(design.Frame, from, node) != null;
            return Run(design, node, settings.Limits, steps => Connect(steps, from, node), splits ? "Face split." : "Nodes joined.");
        }

        private ShipyardFrameToolResult ClickEdge(ShipDesign design, ShipFrameEdge edge, Vector3 point, int? selected, ShipyardFrameToolSettings settings)
        {
            var frame = design.Frame;
            frame.TryGetNode(edge.A, out var a);
            frame.TryGetNode(edge.B, out var b);
            var (x, z) = Snap(point, settings.Snap);
            if (x == a.X && z == a.Z) return ClickNode(design, a.Id, selected, settings);
            if (x == b.X && z == b.Z) return ClickNode(design, b.Id, selected, settings);

            int drift = Mathf.RoundToInt(point.y / ShipFrameUnits.Size) - frame.StoryFloor(a.Story);
            return Run(design, selected, settings.Limits, steps =>
            {
                if (!steps.Apply(ShipFrameEdit.SplitEdge(edge.Id, x, drift, z))) return false;
                int node = steps.Added;
                if (selected is { } from && from != edge.A && from != edge.B && frame.TryGetNode(from, out _) && !Connect(steps, from, node)) return false;
                steps.Select = node;
                return true;
            }, "Edge split.");
        }

        /// <summary>
        /// Joins two nodes: when both are corners of one face (and not neighbours on it), the face splits along the new edge;
        /// otherwise a plain join (<see cref="Join"/>).
        /// </summary>
        private static bool Connect(Steps steps, int from, int to)
        {
            var shared = SharedFace(steps.Current.Frame, from, to);
            return shared != null ? steps.Apply(ShipFrameEdit.SplitFace(shared.Id, from, to)) : Join(steps, from, to);
        }

        /// <summary>A face with both nodes as corners that are not next to each other on it, if any.</summary>
        private static ShipFrameFace? SharedFace(ShipFrame frame, int a, int b) =>
            frame.Faces.FirstOrDefault(f => f.Nodes.Contains(a) && f.Nodes.Contains(b) && !f.HasSide(a, b));

        /// <summary>Adds the edge, then planks the smallest floor loop it closes, if any, and the walls it closes.</summary>
        private static bool Join(Steps steps, int from, int to)
        {
            if (!steps.Apply(ShipFrameEdit.AddEdge(from, to))) return false;

            var loop = ClosedLoop(steps.Current.Frame, from, to);
            if (loop != null) steps.TryApply(ShipFrameEdit.AddFace(loop, ShipFrameFill.Planks));
            foreach (var wall in Walls(steps.Current.Frame, from, to)) steps.TryApply(ShipFrameEdit.AddFace(wall, ShipFrameFill.Planks));
            return true;
        }

        /// <summary>
        /// The shortest way round from <paramref name="to"/> back to <paramref name="from"/> over other floor edges of their
        /// story, as a face loop, when it is a new simple polygon; else null.
        /// </summary>
        private static IReadOnlyList<int>? ClosedLoop(ShipFrame frame, int from, int to)
        {
            if (!frame.TryGetNode(from, out var start) || !frame.TryGetNode(to, out var end) || start.Story != end.Story) return null;

            var neighbours = new Dictionary<int, List<int>>();
            foreach (var edge in frame.Edges)
            {
                if (edge.Joins(from, to)) continue;
                if (!frame.TryGetNode(edge.A, out var a) || !frame.TryGetNode(edge.B, out var b) || a.Story != start.Story || b.Story != start.Story) continue;
                Add(neighbours, edge.A, edge.B);
                Add(neighbours, edge.B, edge.A);
            }

            var previous = new Dictionary<int, int> { [to] = to };
            var queue = new Queue<int>();
            queue.Enqueue(to);
            while (queue.Count > 0 && !previous.ContainsKey(from))
            {
                int node = queue.Dequeue();
                if (!neighbours.TryGetValue(node, out var next)) continue;
                foreach (int other in next.OrderBy(n => n))
                {
                    if (previous.ContainsKey(other)) continue;
                    previous[other] = node;
                    queue.Enqueue(other);
                }
            }

            if (!previous.ContainsKey(from)) return null;
            var loop = new List<int>();
            for (int node = from; node != to; node = previous[node]) loop.Add(node);
            loop.Add(to);
            if (loop.Count < 3) return null;
            if (frame.Faces.Any(f => f.Nodes.Count == loop.Count && new HashSet<int>(f.Nodes).SetEquals(loop))) return null;
            return ShipFrameGeometry.IsSimpleXZ(loop.Select(frame.Position).ToList()) ? loop : null;
        }

        /// <summary>
        /// The wall faces a new edge closes between two adjacent stories: for a floor edge u-v, a post from each end to the
        /// story above or below, joined there (a quad) or meeting at one node (a triangle); for a post, a floor edge from its
        /// lower end whose other end has a post to the same top node (a triangle) or to a node joined to it (a quad). Loops
        /// that are faces already are left out.
        /// </summary>
        private static IReadOnlyList<IReadOnlyList<int>> Walls(ShipFrame frame, int from, int to)
        {
            var walls = new List<IReadOnlyList<int>>();
            if (!frame.TryGetNode(from, out var a) || !frame.TryGetNode(to, out var b)) return walls;

            if (a.Story == b.Story)
            {
                foreach (int story in new[] { a.Story - 1, a.Story + 1 })
                {
                    foreach (int top in Neighbours(frame, from, story))
                    foreach (int other in Neighbours(frame, to, story))
                    {
                        if (top == other) AddWall(frame, walls, from, to, top);
                        else if (frame.TryGetEdgeBetween(top, other, out _)) AddWall(frame, walls, from, to, other, top);
                    }
                }
            }
            else
            {
                var (low, high) = a.Story < b.Story ? (a, b) : (b, a);
                foreach (int side in Neighbours(frame, low.Id, low.Story))
                {
                    if (frame.TryGetEdgeBetween(side, high.Id, out _)) AddWall(frame, walls, low.Id, side, high.Id);
                    foreach (int top in Neighbours(frame, side, high.Story))
                    {
                        if (top != high.Id && frame.TryGetEdgeBetween(top, high.Id, out _)) AddWall(frame, walls, low.Id, side, top, high.Id);
                    }
                }
            }

            return walls;
        }

        /// <summary>The nodes joined to a node by an edge, on the given story, in id order.</summary>
        private static IEnumerable<int> Neighbours(ShipFrame frame, int node, int story) =>
            frame.Edges.Where(e => e.Joins(node))
                .Select(e => e.Other(node))
                .Where(other => frame.TryGetNode(other, out var n) && n.Story == story)
                .OrderBy(id => id);

        private static void AddWall(ShipFrame frame, List<IReadOnlyList<int>> walls, params int[] loop)
        {
            if (loop.Distinct().Count() != loop.Length) return;
            if (frame.Faces.Any(f => f.Nodes.Count == loop.Length && new HashSet<int>(f.Nodes).SetEquals(loop))) return;
            if (walls.Any(w => w.Count == loop.Length && new HashSet<int>(w).SetEquals(loop))) return;
            walls.Add(loop);
        }

        private static void Add(Dictionary<int, List<int>> neighbours, int node, int other)
        {
            if (!neighbours.TryGetValue(node, out var list)) neighbours[node] = list = new List<int>();
            list.Add(other);
        }

        public static (int X, int Z) Snap(Vector3 point, int snap)
        {
            int step = Math.Max(1, snap);
            int x = Mathf.RoundToInt(point.x / ShipFrameUnits.Size / step) * step;
            int z = Mathf.RoundToInt(point.z / ShipFrameUnits.Size / step) * step;
            return (x, z);
        }

        private static ShipyardFrameToolResult Unchanged(int? selected, string message) => new(null, null, selected, message);

        /// <summary>
        /// Runs a gesture's edits in order on a copy. All or nothing: one refusal leaves the design as it was and says why.
        /// The selection is <paramref name="selected"/> unless the gesture picks another. Undo restores the frame from
        /// before the gesture.
        /// </summary>
        private ShipyardFrameToolResult Run(ShipDesign design, int? selected, ShipDesignLimits limits, Func<Steps, bool> gesture, string message)
        {
            var steps = new Steps(_edits, design, limits) { Select = selected };
            if (!gesture(steps)) return Unchanged(selected, steps.Refusal ?? "Nothing to do.");

            var undo = ShipDesignEdit.Frame(ShipFrameEdit.Restore(design.Frame));
            return new ShipyardFrameToolResult(steps.Current, undo, steps.Select, message);
        }

        private sealed class Steps
        {
            private readonly ShipFrameEditProcessor _edits;
            private readonly ShipDesignLimits _limits;

            public Steps(ShipFrameEditProcessor edits, ShipDesign design, ShipDesignLimits limits)
            {
                _edits = edits;
                _limits = limits;
                Current = design;
            }

            public ShipDesign Current { get; private set; }
            public int Added { get; private set; }
            public int? Select { get; set; }
            public string? Refusal { get; private set; }

            public bool Apply(ShipFrameEdit edit)
            {
                var result = _edits.Apply(Current, edit, _limits);
                if (!result.Applied)
                {
                    Refusal = result.Refusal;
                    return false;
                }

                Current = result.Design;
                Added = result.AddedInstanceId;
                return true;
            }

            /// <summary>Applies when it can; a refusal here does not stop the gesture.</summary>
            public void TryApply(ShipFrameEdit edit)
            {
                var result = _edits.Apply(Current, edit, _limits);
                if (result.Applied) Current = result.Design;
            }
        }
    }
}
