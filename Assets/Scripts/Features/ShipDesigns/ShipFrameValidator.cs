#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: what is wrong inside a frame, added to the design's problem list (<see cref="ShipDesignValidator"/> runs it
    /// and checks ids and connection to the core). Rules (.docs/plans/ship-frames.md):
    /// <list type="bullet">
    /// <item>within the limits: stories, elements, the extent; every story has a height;</item>
    /// <item>nodes: on a story that exists, drifting no more than <see cref="ShipFrameTuning.MaxDrift"/>, with at least two edges;</item>
    /// <item>edges: between two different nodes that exist, on the same or adjacent stories, at least
    /// <see cref="ShipFrameTuning.MinEdgeLength"/> long, one per pair of nodes; a railing only on a floor edge, and not
    /// under a planked wall;</item>
    /// <item>faces: three or more distinct corners, each side an edge, not crossing itself, not too small. A floor face
    /// lies on one story; a wall face spans exactly two adjacent stories. Plank floors are no steeper than
    /// <see cref="ShipFrameTuning.MaxFloorSlope"/> and do not overlap another plank floor on their story;</item>
    /// <item>headroom: where a plank floor lies over a plank floor of the story below, there is at least
    /// <see cref="ShipFrameTuning.MinHeadroom"/> between the lower floor's top and the upper floor's underside, checked
    /// exactly where the gap is smallest (at corners and where sides cross, seen from above).</item>
    /// </list>
    /// </summary>
    public static class ShipFrameValidator
    {
        public static void Validate(ShipFrameShape shape, ShipFrameTuning tuning, ShipDesignLimits limits, List<ShipDesignProblem> problems)
        {
            var frame = shape.Frame;
            if (frame.StoryHeights.Count > limits.MaxStories)
            {
                problems.Add(new(ShipDesignProblemKind.FrameTooLarge, 0, $"{frame.StoryHeights.Count} stories; at most {limits.MaxStories}."));
            }

            if (frame.ElementCount > limits.MaxFrameElements)
            {
                problems.Add(new(ShipDesignProblemKind.FrameTooLarge, 0, $"{frame.ElementCount} nodes, edges and faces; at most {limits.MaxFrameElements}."));
            }

            for (int s = 0; s < frame.StoryHeights.Count; s++)
            {
                if (frame.StoryHeights[s] <= 0) problems.Add(new(ShipDesignProblemKind.FrameReference, 0, $"Story {s} has no height."));
            }

            CheckNodes(frame, tuning, limits, problems);
            CheckEdges(shape, tuning, problems);
            CheckFaces(shape, tuning, limits, problems);
            CheckHeadroom(shape, tuning, problems);
        }

        private static void CheckNodes(ShipFrame frame, ShipFrameTuning tuning, ShipDesignLimits limits, List<ShipDesignProblem> problems)
        {
            var degree = new Dictionary<int, int>();
            foreach (var edge in frame.Edges)
            {
                degree[edge.A] = degree.GetValueOrDefault(edge.A) + 1;
                degree[edge.B] = degree.GetValueOrDefault(edge.B) + 1;
            }

            int maxDrift = ShipFrameUnits.FromMetres(tuning.MaxDrift);
            foreach (var node in frame.Nodes)
            {
                if (node.Story < 0 || node.Story >= frame.StoryHeights.Count)
                {
                    problems.Add(new(ShipDesignProblemKind.FrameReference, node.Id, $"{node} is on a story the frame does not have."));
                    continue;
                }

                if (!limits.Contains(node.X, frame.StoryFloor(node.Story) + node.Y, node.Z))
                {
                    problems.Add(new(ShipDesignProblemKind.OutOfBounds, node.Id, $"{node} is more than {limits.MaxExtent} cells from the origin."));
                }

                if (Math.Abs(node.Y) > maxDrift)
                {
                    problems.Add(new(ShipDesignProblemKind.DriftOutOfBand, node.Id,
                        $"{node} drifts {ShipFrameUnits.ToMetres(node.Y):0.0} m from its story's floor; at most {tuning.MaxDrift:0.0} m."));
                }

                int edges = degree.GetValueOrDefault(node.Id);
                if (edges < 2) problems.Add(new(ShipDesignProblemKind.DanglingNode, node.Id, $"{node} has {edges} edge(s); a node needs at least two."));
            }
        }

        private static void CheckEdges(ShipFrameShape shape, ShipFrameTuning tuning, List<ShipDesignProblem> problems)
        {
            var frame = shape.Frame;
            var pairs = new Dictionary<(int, int), int>();
            foreach (var edge in frame.Edges)
            {
                if (!frame.TryGetNode(edge.A, out var a) || !frame.TryGetNode(edge.B, out var b))
                {
                    problems.Add(new(ShipDesignProblemKind.FrameReference, edge.Id, $"{edge} names a node that is not there."));
                    continue;
                }

                if (edge.A == edge.B)
                {
                    problems.Add(new(ShipDesignProblemKind.BadEdge, edge.Id, $"{edge} joins a node to itself."));
                    continue;
                }

                if (Math.Abs(a.Story - b.Story) > 1) problems.Add(new(ShipDesignProblemKind.BadEdge, edge.Id, $"{edge} skips a story."));

                float length = (frame.Position(a) - frame.Position(b)).magnitude;
                if (length < tuning.MinEdgeLength)
                {
                    problems.Add(new(ShipDesignProblemKind.BadEdge, edge.Id, $"{edge} is {length:0.00} m long; at least {tuning.MinEdgeLength:0.00} m."));
                }

                var key = (Math.Min(edge.A, edge.B), Math.Max(edge.A, edge.B));
                if (pairs.TryGetValue(key, out int twin)) problems.Add(new(ShipDesignProblemKind.BadEdge, edge.Id, $"{edge} doubles edge #{twin}."));
                else pairs.Add(key, edge.Id);

                if (!edge.Railing) continue;
                if (a.Story != b.Story)
                {
                    problems.Add(new(ShipDesignProblemKind.BadEdge, edge.Id, $"{edge} is a post; railings run along floor edges."));
                    continue;
                }

                var wall = shape.PlankFaces.FirstOrDefault(f => f.IsWall && f.Story == a.Story && f.Face.HasSide(edge.A, edge.B));
                if (wall.Face != null)
                {
                    problems.Add(new(ShipDesignProblemKind.BadEdge, edge.Id, $"{edge} has a railing under the planked wall {wall.Face}."));
                }
            }
        }

        private static void CheckFaces(ShipFrameShape shape, ShipFrameTuning tuning, ShipDesignLimits limits, List<ShipDesignProblem> problems)
        {
            var frame = shape.Frame;
            var shaped = new HashSet<int>(shape.Faces.Select(f => f.Face.Id));
            foreach (var face in frame.Faces)
            {
                if (face.Nodes.Count < 3 || face.Nodes.Count > limits.MaxFaceNodes || face.Nodes.Distinct().Count() != face.Nodes.Count)
                {
                    problems.Add(new(ShipDesignProblemKind.BadFace, face.Id, $"{face} needs 3 to {limits.MaxFaceNodes} distinct corners."));
                    continue;
                }

                if (!shaped.Contains(face.Id))
                {
                    problems.Add(new(ShipDesignProblemKind.FrameReference, face.Id, $"{face} names a node that is not there."));
                }
            }

            foreach (var faceShape in shape.Faces)
            {
                var face = faceShape.Face;
                if (face.Nodes.Distinct().Count() != face.Nodes.Count) continue;
                CheckFace(frame, faceShape, tuning, problems);
            }

            CheckOverlaps(shape, problems);
        }

        private static void CheckFace(ShipFrame frame, ShipFrameShape.FaceShape shape, ShipFrameTuning tuning, List<ShipDesignProblem> problems)
        {
            var face = shape.Face;
            int top = face.Nodes.Max(id => frame.TryGetNode(id, out var node) ? node.Story : shape.Story);
            if (top - shape.Story > 1)
            {
                problems.Add(new(ShipDesignProblemKind.BadFace, face.Id, $"{face} spans more than two stories; a wall joins adjacent stories."));
                return;
            }

            for (int i = 0; i < face.Nodes.Count; i++)
            {
                int a = face.Nodes[i], b = face.Nodes[(i + 1) % face.Nodes.Count];
                if (frame.TryGetEdgeBetween(a, b, out _)) continue;
                problems.Add(new(ShipDesignProblemKind.BadFace, face.Id, $"{face}: nodes #{a} and #{b} are not joined by an edge."));
            }

            bool simple = shape.IsWall ? ShipFrameGeometry.IsSimpleInPlane(shape.Corners) : ShipFrameGeometry.IsSimpleXZ(shape.Corners);
            if (!simple)
            {
                problems.Add(new(ShipDesignProblemKind.BadFace, face.Id, $"{face} crosses itself."));
                return;
            }

            float area = shape.IsWall
                ? shape.EachTriangle().Sum(t => ShipFrameGeometry.Area(t.A, t.B, t.C))
                : ShipFrameGeometry.AreaXZ(shape.Corners);
            if (area < tuning.MinFaceArea)
            {
                problems.Add(new(ShipDesignProblemKind.BadFace, face.Id, $"{face} is {area:0.00} m²; at least {tuning.MinFaceArea:0.00} m²."));
            }

            if (shape.IsWall || face.Fill != ShipFrameFill.Planks) return;
            float steepest = shape.EachTriangle().Select(t => ShipFrameGeometry.SlopeDegrees(t.A, t.B, t.C)).DefaultIfEmpty(0f).Max();
            if (steepest > tuning.MaxFloorSlope)
            {
                problems.Add(new(ShipDesignProblemKind.FloorTooSteep, face.Id, $"{face} tilts {steepest:0}°; a floor may tilt at most {tuning.MaxFloorSlope:0}°."));
            }
        }

        /// <summary>Plank floors on the same story must not overlap seen from above (sharing sides and corners is fine).</summary>
        private static void CheckOverlaps(ShipFrameShape shape, List<ShipDesignProblem> problems)
        {
            var planks = SimpleFloors(shape);
            for (int i = 0; i < planks.Count; i++)
            {
                for (int j = i + 1; j < planks.Count; j++)
                {
                    var a = planks[i];
                    var b = planks[j];
                    if (a.Story != b.Story || !BoxesMeet(a, b)) continue;
                    if (!ShipFrameGeometry.OverlapXZ(a.Corners, a.Triangles, b.Corners, b.Triangles)) continue;

                    problems.Add(new(ShipDesignProblemKind.BadFace, b.Face.Id, $"{b.Face} overlaps {a.Face}."));
                }
            }
        }

        /// <summary>
        /// Where a plank floor of story s+1 lies over one of story s, the room between them. Both are flat triangles, so the
        /// gap is smallest at a corner of one inside the other, or where their sides cross: exactly those points are
        /// measured.
        /// </summary>
        private static void CheckHeadroom(ShipFrameShape shape, ShipFrameTuning tuning, List<ShipDesignProblem> problems)
        {
            var floors = SimpleFloors(shape);
            foreach (var lower in floors)
            {
                foreach (var upper in floors)
                {
                    if (upper.Story != lower.Story + 1 || !BoxesMeet(lower, upper)) continue;

                    float least = float.MaxValue;
                    foreach (var point in Candidates(lower, upper))
                    {
                        if (!TryHeight(lower, point, out float floor) || !TryHeight(upper, point, out float ceiling)) continue;
                        least = Mathf.Min(least, ceiling - tuning.PlankThickness - floor);
                    }

                    if (least >= tuning.MinHeadroom || least == float.MaxValue) continue;
                    problems.Add(new(ShipDesignProblemKind.BadFace, upper.Face.Id,
                        $"{upper.Face} leaves {least:0.00} m over {lower.Face}; at least {tuning.MinHeadroom:0.0} m."));
                }
            }
        }

        private static IEnumerable<Vector2> Candidates(ShipFrameShape.FaceShape a, ShipFrameShape.FaceShape b)
        {
            foreach (var corner in a.Corners) yield return new Vector2(corner.x, corner.z);
            foreach (var corner in b.Corners) yield return new Vector2(corner.x, corner.z);
            for (int i = 0; i < a.Corners.Count; i++)
            {
                for (int j = 0; j < b.Corners.Count; j++)
                {
                    if (Crossing(a.Corners[i], a.Corners[(i + 1) % a.Corners.Count], b.Corners[j], b.Corners[(j + 1) % b.Corners.Count], out var point))
                    {
                        yield return point;
                    }
                }
            }
        }

        private static bool TryHeight(ShipFrameShape.FaceShape face, Vector2 point, out float height)
        {
            foreach (var (a, b, c) in face.EachTriangle())
            {
                if (ShipFrameGeometry.TryHeightAt(point.x, point.y, a, b, c, out height)) return true;
            }

            height = 0f;
            return false;
        }

        /// <summary>Where two sides cross seen from above, if they do.</summary>
        private static bool Crossing(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2, out Vector2 point)
        {
            point = default;
            var r = new Vector2(p2.x - p1.x, p2.z - p1.z);
            var s = new Vector2(q2.x - q1.x, q2.z - q1.z);
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 1e-6f) return false;

            var qp = new Vector2(q1.x - p1.x, q1.z - p1.z);
            float t = (qp.x * s.y - qp.y * s.x) / denominator;
            float u = (qp.x * r.y - qp.y * r.x) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            point = new Vector2(p1.x + r.x * t, p1.z + r.y * t);
            return true;
        }

        private static List<ShipFrameShape.FaceShape> SimpleFloors(ShipFrameShape shape) =>
            shape.PlankFloors.Where(f => ShipFrameGeometry.IsSimpleXZ(f.Corners)).ToList();

        private static bool BoxesMeet(ShipFrameShape.FaceShape a, ShipFrameShape.FaceShape b) =>
            a.Corners.Min(c => c.x) <= b.Corners.Max(c => c.x) && b.Corners.Min(c => c.x) <= a.Corners.Max(c => c.x) &&
            a.Corners.Min(c => c.z) <= b.Corners.Max(c => c.z) && b.Corners.Min(c => c.z) <= a.Corners.Max(c => c.z);
    }
}
