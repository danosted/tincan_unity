#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A frame in ship-local metres: each face's corners and triangles, each edge's segment. Built once from a frame and
    /// shared by the validator, the stats and the mesh processor, so they all see the same triangles. References to
    /// missing nodes are skipped (the validator reports them).
    /// </summary>
    public sealed class ShipFrameShape
    {
        public readonly struct FaceShape
        {
            public FaceShape(ShipFrameFace face, int story, bool isWall, IReadOnlyList<Vector3> corners, IReadOnlyList<int> triangles, Vector3 facing)
            {
                Face = face;
                Story = story;
                IsWall = isWall;
                Facing = facing;
                Corners = corners;
                Triangles = triangles;
            }

            public ShipFrameFace Face { get; }

            /// <summary>The lowest story of its corners (a wall belongs to the story it rises from).</summary>
            public int Story { get; }

            /// <summary>A wall face: its corners are on two adjacent stories, and it is triangulated in its own plane.</summary>
            public bool IsWall { get; }

            /// <summary>Where the triangles' fronts face: up for a floor, out of the plane for a wall.</summary>
            public Vector3 Facing { get; }

            public IReadOnlyList<Vector3> Corners { get; }

            /// <summary>Index triples into <see cref="Corners"/>, each facing up.</summary>
            public IReadOnlyList<int> Triangles { get; }

            public IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> EachTriangle()
            {
                for (int i = 0; i + 2 < Triangles.Count; i += 3) yield return (Corners[Triangles[i]], Corners[Triangles[i + 1]], Corners[Triangles[i + 2]]);
            }
        }

        public readonly struct EdgeShape
        {
            public EdgeShape(ShipFrameEdge edge, int story, Vector3 from, Vector3 to)
            {
                Edge = edge;
                Story = story;
                From = from;
                To = to;
            }

            public ShipFrameEdge Edge { get; }

            /// <summary>The lower story of the edge's two nodes (a post belongs to the story it rises from).</summary>
            public int Story { get; }

            public Vector3 From { get; }
            public Vector3 To { get; }
            public float Length => Vector3.Distance(From, To);
        }

        private ShipFrameShape(ShipFrame frame, IReadOnlyList<FaceShape> faces, IReadOnlyList<EdgeShape> edges)
        {
            Frame = frame;
            Faces = faces;
            Edges = edges;
        }

        public ShipFrame Frame { get; }
        public IReadOnlyList<FaceShape> Faces { get; }
        public IReadOnlyList<EdgeShape> Edges { get; }

        public IEnumerable<FaceShape> PlankFaces => Faces.Where(f => f.Face.Fill == ShipFrameFill.Planks);

        /// <summary>Plank floors only: what parts stand on and floors are checked against.</summary>
        public IEnumerable<FaceShape> PlankFloors => PlankFaces.Where(f => !f.IsWall);

        public static ShipFrameShape From(ShipFrame frame)
        {
            var faces = new List<FaceShape>(frame.Faces.Count);
            foreach (var face in frame.Faces)
            {
                if (face.Nodes.Count < 3 || !face.Nodes.All(id => frame.TryGetNode(id, out _))) continue;

                var corners = face.Nodes.Select(frame.Position).ToList();
                var stories = face.Nodes.Select(id => frame.TryGetNode(id, out var node) ? node.Story : 0).ToList();
                bool isWall = stories.Max() != stories.Min();
                var facing = Vector3.up;
                var triangles = isWall ? ShipFrameGeometry.TriangulateInPlane(corners, out facing) : ShipFrameGeometry.Triangulate(corners);
                faces.Add(new FaceShape(face, stories.Min(), isWall, corners, triangles, facing));
            }

            var edges = new List<EdgeShape>(frame.Edges.Count);
            foreach (var edge in frame.Edges)
            {
                if (!frame.TryGetNode(edge.A, out var a) || !frame.TryGetNode(edge.B, out var b)) continue;
                edges.Add(new EdgeShape(edge, Mathf.Min(a.Story, b.Story), frame.Position(a), frame.Position(b)));
            }

            return new ShipFrameShape(frame, faces, edges);
        }

        /// <summary>Plank area (m², in 3D) and beam length (m): what the frame weighs and holds.</summary>
        public (float PlankArea, float BeamLength) Totals()
        {
            float area = PlankFaces.SelectMany(f => f.EachTriangle()).Sum(t => ShipFrameGeometry.Area(t.A, t.B, t.C));
            float length = Edges.Sum(e => e.Length);
            return (area, length);
        }

        /// <summary>The frame's ship-local bounds (nodes only), or false for a frame without nodes.</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            if (Frame.Nodes.Count == 0) return false;

            bounds = new Bounds(Frame.Position(Frame.Nodes[0]), Vector3.zero);
            foreach (var node in Frame.Nodes) bounds.Encapsulate(Frame.Position(node));
            return true;
        }
    }
}
