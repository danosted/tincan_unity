#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: applies one <see cref="ShipFrameEdit"/> to a design's frame and keeps its faces in step, so a face keeps its id
    /// and fill through edits. Refused: references to missing nodes, edges or faces; positions beyond the limits; a design
    /// whose frame is full; edges between stories that are not adjacent; a second edge between two nodes; merging nodes on
    /// different stories; a face whose sides are not all edges. Geometry (drift, slope, crossing sides) is the validator's,
    /// so a builder can pass through invalid shapes. Rules for faces:
    /// <list type="bullet">
    /// <item>merging nodes moves their corners; a face left with fewer than three corners, pinched (a corner twice), or
    /// with the same corners as a face with a lower id, goes;</item>
    /// <item>removing an edge removes the one face it bounds, or merges the two it divides into the one with the lower id;</item>
    /// <item>splitting an edge adds the new corner to the faces it bounds;</item>
    /// <item>splitting a face keeps the id on the larger half (seen from above); the other half is a new face with the same fill.</item>
    /// </list>
    /// Every applied edit is undone by restoring the frame before it. Ids come from the design's shared counter and are
    /// never reused.
    /// </summary>
    public sealed class ShipFrameEditProcessor
    {
        public ShipDesignEditResult Apply(ShipDesign design, ShipFrameEdit edit, ShipDesignLimits limits)
        {
            var frame = design.Frame;
            return edit.Kind switch
            {
                ShipFrameEditKind.AddStory => AddStory(design, frame, edit, limits),
                ShipFrameEditKind.AddNode => AddNode(design, frame, edit, limits),
                ShipFrameEditKind.MoveNode => MoveNode(design, frame, edit, limits),
                ShipFrameEditKind.RemoveNode => RemoveNode(design, frame, edit.Id),
                ShipFrameEditKind.MergeNodes => MergeNodes(design, frame, edit.Id, edit.Other),
                ShipFrameEditKind.AddEdge => AddEdge(design, frame, edit.Id, edit.Other, limits),
                ShipFrameEditKind.RemoveEdge => RemoveEdge(design, frame, edit.Id),
                ShipFrameEditKind.SplitEdge => SplitEdge(design, frame, edit, limits),
                ShipFrameEditKind.SetRailing => SetRailing(design, frame, edit.Id, edit.Railing),
                ShipFrameEditKind.AddFace => AddFace(design, frame, edit.Nodes, edit.Fill, limits),
                ShipFrameEditKind.SplitFace => SplitFace(design, frame, edit, limits),
                ShipFrameEditKind.SetFaceFill => SetFaceFill(design, frame, edit.Id, edit.Fill),
                ShipFrameEditKind.RemoveFace => RemoveFace(design, frame, edit.Id),
                ShipFrameEditKind.Restore => Restore(design, edit.Frame),
                _ => ShipDesignEditResult.Refused(design, $"Unknown frame edit {edit.Kind}."),
            };
        }

        private static ShipDesignEditResult AddStory(ShipDesign design, ShipFrame frame, ShipFrameEdit edit, ShipDesignLimits limits)
        {
            if (edit.Story <= 0) return ShipDesignEditResult.Refused(design, "A story must have a height.");
            if (frame.StoryHeights.Count >= limits.MaxStories) return ShipDesignEditResult.Refused(design, $"The frame has {limits.MaxStories} stories already.");

            return Done(design, frame.With(storyHeights: frame.StoryHeights.Append(edit.Story)), design.NextPartInstanceId);
        }

        private static ShipDesignEditResult AddNode(ShipDesign design, ShipFrame frame, ShipFrameEdit edit, ShipDesignLimits limits)
        {
            if (edit.Story < 0 || edit.Story >= frame.StoryHeights.Count) return ShipDesignEditResult.Refused(design, $"There is no story {edit.Story}.");
            var refusal = Full(frame, 1, limits) ?? OutOfReach(frame, edit.Story, edit.X, edit.Y, edit.Z, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            int id = NextId(design);
            var node = new ShipFrameNode(id, edit.Story, edit.X, edit.Y, edit.Z);
            return Done(design, frame.With(nodes: frame.Nodes.Append(node)), id + 1, id);
        }

        private static ShipDesignEditResult MoveNode(ShipDesign design, ShipFrame frame, ShipFrameEdit edit, ShipDesignLimits limits)
        {
            if (!frame.TryGetNode(edit.Id, out var node)) return ShipDesignEditResult.Refused(design, $"There is no node #{edit.Id}.");
            var refusal = OutOfReach(frame, node.Story, edit.X, edit.Y, edit.Z, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            var moved = node.MovedTo(edit.X, edit.Y, edit.Z);
            return Done(design, frame.With(nodes: frame.Nodes.Select(n => n.Id == node.Id ? moved : n)), design.NextPartInstanceId);
        }

        private static ShipDesignEditResult RemoveNode(ShipDesign design, ShipFrame frame, int id)
        {
            if (!frame.TryGetNode(id, out _)) return ShipDesignEditResult.Refused(design, $"There is no node #{id}.");

            var next = frame.With(
                nodes: frame.Nodes.Where(n => n.Id != id),
                edges: frame.Edges.Where(e => !e.Joins(id)),
                faces: frame.Faces.Where(f => !f.Nodes.Contains(id)));
            return Done(design, next, design.NextPartInstanceId);
        }

        private static ShipDesignEditResult MergeNodes(ShipDesign design, ShipFrame frame, int keep, int gone)
        {
            if (keep == gone) return ShipDesignEditResult.Refused(design, "A node cannot merge with itself.");
            if (!frame.TryGetNode(keep, out var kept)) return ShipDesignEditResult.Refused(design, $"There is no node #{keep}.");
            if (!frame.TryGetNode(gone, out var dropped)) return ShipDesignEditResult.Refused(design, $"There is no node #{gone}.");
            if (kept.Story != dropped.Story) return ShipDesignEditResult.Refused(design, "Only nodes on the same story merge.");

            var edges = new List<ShipFrameEdge>();
            foreach (var edge in frame.Edges)
            {
                var moved = edge.Between(edge.A == gone ? keep : edge.A, edge.B == gone ? keep : edge.B);
                if (moved.A == moved.B) continue;

                int twin = edges.FindIndex(e => e.Joins(moved.A, moved.B));
                if (twin < 0)
                {
                    edges.Add(moved);
                    continue;
                }

                // The lower id stays (edges come in id order); a railing on either stays too.
                if (moved.Railing && !edges[twin].Railing) edges[twin] = new ShipFrameEdge(edges[twin].Id, edges[twin].A, edges[twin].B, true);
            }

            var faces = new List<ShipFrameFace>();
            foreach (var face in frame.Faces)
            {
                var loop = WithoutRepeats(face.Nodes.Select(n => n == gone ? keep : n).ToList());
                if (loop.Count < 3 || loop.Distinct().Count() != loop.Count || faces.Any(f => SameCorners(f.Nodes, loop))) continue;
                faces.Add(face.With(loop));
            }

            return Done(design, frame.With(nodes: frame.Nodes.Where(n => n.Id != gone), edges: edges, faces: faces), design.NextPartInstanceId);
        }

        private static ShipDesignEditResult AddEdge(ShipDesign design, ShipFrame frame, int a, int b, ShipDesignLimits limits)
        {
            if (a == b) return ShipDesignEditResult.Refused(design, "An edge joins two different nodes.");
            if (!frame.TryGetNode(a, out var first)) return ShipDesignEditResult.Refused(design, $"There is no node #{a}.");
            if (!frame.TryGetNode(b, out var second)) return ShipDesignEditResult.Refused(design, $"There is no node #{b}.");
            if (Math.Abs(first.Story - second.Story) > 1) return ShipDesignEditResult.Refused(design, "Edges join nodes on the same story or on adjacent stories.");
            if (frame.TryGetEdgeBetween(a, b, out var existing)) return ShipDesignEditResult.Refused(design, $"Nodes #{a} and #{b} are already joined by {existing}.");
            var refusal = Full(frame, 1, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            int id = NextId(design);
            return Done(design, frame.With(edges: frame.Edges.Append(new ShipFrameEdge(id, a, b))), id + 1, id);
        }

        private static ShipDesignEditResult RemoveEdge(ShipDesign design, ShipFrame frame, int id)
        {
            if (!frame.TryGetEdge(id, out var edge)) return ShipDesignEditResult.Refused(design, $"There is no edge #{id}.");

            var bounded = frame.Faces.Where(f => f.HasSide(edge.A, edge.B)).ToList();
            var faces = frame.Faces.Where(f => !bounded.Contains(f)).ToList();
            if (bounded.Count == 2)
            {
                var merged = Merge(bounded[0], bounded[1], edge.A, edge.B);
                if (merged == null) return ShipDesignEditResult.Refused(design, $"Faces #{bounded[0].Id} and #{bounded[1].Id} share more than {edge}; remove one of them first.");
                faces.Add(merged);
            }
            else if (bounded.Count > 2)
            {
                return ShipDesignEditResult.Refused(design, $"{edge} bounds {bounded.Count} faces; remove some of them first.");
            }

            return Done(design, frame.With(edges: frame.Edges.Where(e => e.Id != id), faces: faces), design.NextPartInstanceId);
        }

        private static ShipDesignEditResult SplitEdge(ShipDesign design, ShipFrame frame, ShipFrameEdit edit, ShipDesignLimits limits)
        {
            if (!frame.TryGetEdge(edit.Id, out var edge)) return ShipDesignEditResult.Refused(design, $"There is no edge #{edit.Id}.");
            frame.TryGetNode(edge.A, out var a);
            frame.TryGetNode(edge.B, out var b);
            if (a.Story != b.Story) return ShipDesignEditResult.Refused(design, "Only floor edges split; a post joins two stories.");
            var refusal = Full(frame, 2, limits) ?? OutOfReach(frame, a.Story, edit.X, edit.Y, edit.Z, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            int nodeId = NextId(design);
            int edgeId = nodeId + 1;
            var node = new ShipFrameNode(nodeId, a.Story, edit.X, edit.Y, edit.Z);
            var edges = frame.Edges.Select(e => e.Id == edge.Id ? e.Between(edge.A, nodeId) : e)
                .Append(new ShipFrameEdge(edgeId, nodeId, edge.B, edge.Railing));
            var faces = frame.Faces.Select(f => f.HasSide(edge.A, edge.B) ? f.With(InsertBetween(f.Nodes, edge.A, edge.B, nodeId)) : f);

            return Done(design, frame.With(nodes: frame.Nodes.Append(node), edges: edges, faces: faces), edgeId + 1, nodeId);
        }

        private static ShipDesignEditResult SetRailing(ShipDesign design, ShipFrame frame, int id, bool railing)
        {
            if (!frame.TryGetEdge(id, out var edge)) return ShipDesignEditResult.Refused(design, $"There is no edge #{id}.");

            var changed = new ShipFrameEdge(edge.Id, edge.A, edge.B, railing);
            return Done(design, frame.With(edges: frame.Edges.Select(e => e.Id == id ? changed : e)), design.NextPartInstanceId);
        }

        private static ShipDesignEditResult AddFace(ShipDesign design, ShipFrame frame, IReadOnlyList<int> loop, ShipFrameFill fill, ShipDesignLimits limits)
        {
            var refusal = FaceRefusal(frame, loop, limits) ?? Full(frame, 1, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            int id = NextId(design);
            return Done(design, frame.With(faces: frame.Faces.Append(new ShipFrameFace(id, loop.ToList(), fill))), id + 1, id);
        }

        private static ShipDesignEditResult SplitFace(ShipDesign design, ShipFrame frame, ShipFrameEdit edit, ShipDesignLimits limits)
        {
            if (!frame.TryGetFace(edit.Id, out var face)) return ShipDesignEditResult.Refused(design, $"There is no face #{edit.Id}.");
            if (edit.Nodes.Count != 2) return ShipDesignEditResult.Refused(design, "A face splits between two of its corners.");

            int a = edit.Nodes[0], b = edit.Nodes[1];
            int i = IndexOf(face.Nodes, a), j = IndexOf(face.Nodes, b);
            if (i < 0 || j < 0) return ShipDesignEditResult.Refused(design, $"Nodes #{a} and #{b} are not both corners of {face}.");
            if (a == b || face.HasSide(a, b)) return ShipDesignEditResult.Refused(design, $"Nodes #{a} and #{b} are neighbours on {face}; nothing to split.");

            bool needsEdge = !frame.TryGetEdgeBetween(a, b, out _);
            var refusal = Full(frame, needsEdge ? 2 : 1, limits);
            if (refusal != null) return ShipDesignEditResult.Refused(design, refusal);

            var first = Walk(face.Nodes, i, j);
            var second = Walk(face.Nodes, j, i);
            bool firstKeeps = AreaOf(frame, first) >= AreaOf(frame, second);
            int id = NextId(design);
            var kept = face.With(firstKeeps ? first : second);
            var added = new ShipFrameFace(id, firstKeeps ? second : first, face.Fill);
            var faces = frame.Faces.Select(f => f.Id == face.Id ? kept : f).Append(added);
            var edges = needsEdge ? frame.Edges.Append(new ShipFrameEdge(id + 1, a, b)) : frame.Edges;

            return Done(design, frame.With(edges: edges, faces: faces), id + (needsEdge ? 2 : 1), id);
        }

        private static ShipDesignEditResult SetFaceFill(ShipDesign design, ShipFrame frame, int id, ShipFrameFill fill)
        {
            if (!frame.TryGetFace(id, out var face)) return ShipDesignEditResult.Refused(design, $"There is no face #{id}.");

            return Done(design, frame.With(faces: frame.Faces.Select(f => f.Id == id ? face.With(fill) : f)), design.NextPartInstanceId);
        }

        private static ShipDesignEditResult RemoveFace(ShipDesign design, ShipFrame frame, int id)
        {
            if (!frame.TryGetFace(id, out _)) return ShipDesignEditResult.Refused(design, $"There is no face #{id}.");

            return Done(design, frame.With(faces: frame.Faces.Where(f => f.Id != id)), design.NextPartInstanceId);
        }

        private static ShipDesignEditResult Restore(ShipDesign design, ShipFrame? frame)
        {
            if (frame == null) return ShipDesignEditResult.Refused(design, "Nothing to restore.");

            // Ids are never reused: the counter stays where it is, however old the frame put back.
            return Done(design, frame, Math.Max(design.NextPartInstanceId, frame.ElementCount == 0 ? 1 : frame.Ids.Max() + 1));
        }

        private static ShipDesignEditResult Done(ShipDesign design, ShipFrame frame, int nextId, int added = 0) =>
            ShipDesignEditResult.Done(design.WithFrame(frame, nextId), ShipDesignEdit.Frame(ShipFrameEdit.Restore(design.Frame)), added);

        private static int NextId(ShipDesign design) => Math.Max(design.NextPartInstanceId, design.IdFloor);

        private static string? Full(ShipFrame frame, int adding, ShipDesignLimits limits) =>
            frame.ElementCount + adding > limits.MaxFrameElements ? $"The frame is full ({limits.MaxFrameElements} nodes, edges and faces)." : null;

        private static string? OutOfReach(ShipFrame frame, int story, int x, int y, int z, ShipDesignLimits limits) =>
            limits.Contains(x, frame.StoryFloor(story) + y, z) ? null : $"({x}, {y}, {z}) is more than {limits.MaxExtent} cells from the origin.";

        private static string? FaceRefusal(ShipFrame frame, IReadOnlyList<int> loop, ShipDesignLimits limits)
        {
            if (loop.Count < 3) return "A face needs at least three corners.";
            if (loop.Count > limits.MaxFaceNodes) return $"A face has at most {limits.MaxFaceNodes} corners.";
            if (loop.Distinct().Count() != loop.Count) return "A face visits each corner once.";

            for (int i = 0; i < loop.Count; i++)
            {
                if (!frame.TryGetNode(loop[i], out _)) return $"There is no node #{loop[i]}.";
                int next = loop[(i + 1) % loop.Count];
                if (!frame.TryGetEdgeBetween(loop[i], next, out _)) return $"Nodes #{loop[i]} and #{next} are not joined by an edge.";
            }

            var same = frame.Faces.FirstOrDefault(f => SameCorners(f.Nodes, loop));
            return same != null ? $"{same} already has these corners." : null;
        }

        /// <summary>
        /// Two faces joined along their shared side a-b, as one loop: all of the first, then the second's corners between b
        /// and a. Null when they share more than that side.
        /// </summary>
        private static ShipFrameFace? Merge(ShipFrameFace one, ShipFrameFace two, int a, int b)
        {
            var (keeper, other) = one.Id < two.Id ? (one, two) : (two, one);
            var first = StartingAfterSide(keeper.Nodes, a, b, out int from, out int to);
            var second = StartingAfterSide(other.Nodes, a, b, out int otherFrom, out _);
            // first runs to → ... → from; second must run from → ... → to to close the loop the other way.
            if (otherFrom != to) second.Reverse();
            second = Rotate(second, second.IndexOf(from));

            var loop = first.Concat(second.Skip(1).Take(second.Count - 2)).ToList();
            if (loop.Distinct().Count() != loop.Count) return null;
            return keeper.With(loop);
        }

        /// <summary>The loop rotated so the side a-b closes it: it starts at one end and finishes at the other.</summary>
        private static List<int> StartingAfterSide(IReadOnlyList<int> loop, int a, int b, out int last, out int start)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                int from = loop[i], to = loop[(i + 1) % loop.Count];
                if (!((from == a && to == b) || (from == b && to == a))) continue;

                last = from;
                start = to;
                return Rotate(loop, (i + 1) % loop.Count);
            }

            last = start = 0;
            return loop.ToList();
        }

        private static List<int> Rotate(IReadOnlyList<int> loop, int start)
        {
            var rotated = new List<int>(loop.Count);
            for (int i = 0; i < loop.Count; i++) rotated.Add(loop[(start + i) % loop.Count]);
            return rotated;
        }

        /// <summary>The corners from index i to index j, going forward round the loop, both ends included.</summary>
        private static List<int> Walk(IReadOnlyList<int> loop, int i, int j)
        {
            var walk = new List<int>();
            for (int k = i; ; k = (k + 1) % loop.Count)
            {
                walk.Add(loop[k]);
                if (k == j) return walk;
            }
        }

        private static List<int> InsertBetween(IReadOnlyList<int> loop, int a, int b, int node)
        {
            var result = new List<int>(loop.Count + 1);
            for (int i = 0; i < loop.Count; i++)
            {
                int from = loop[i], to = loop[(i + 1) % loop.Count];
                result.Add(from);
                if ((from == a && to == b) || (from == b && to == a)) result.Add(node);
            }

            return result;
        }

        private static List<int> WithoutRepeats(List<int> loop)
        {
            var result = new List<int>(loop.Count);
            foreach (int node in loop)
            {
                if (result.Count > 0 && result[^1] == node) continue;
                result.Add(node);
            }

            while (result.Count > 1 && result[0] == result[^1]) result.RemoveAt(result.Count - 1);
            return result;
        }

        private static bool SameCorners(IReadOnlyList<int> a, IReadOnlyList<int> b) =>
            a.Count == b.Count && new HashSet<int>(a).SetEquals(b);

        private static int IndexOf(IReadOnlyList<int> loop, int node)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                if (loop[i] == node) return i;
            }

            return -1;
        }

        private static float AreaOf(ShipFrame frame, IReadOnlyList<int> loop) =>
            ShipFrameGeometry.AreaXZ(loop.Select(frame.Position).ToList());
    }
}
