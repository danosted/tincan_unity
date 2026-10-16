#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: a frame's hull as mesh data, one <see cref="ShipFrameStoryMesh"/> per story (.docs/plans/ship-frames.md).
    /// <list type="bullet">
    /// <item>Planks: each plank face is a slab <see cref="ShipFrameTuning.PlankThickness"/> thick whose top is where its
    /// nodes are (its triangles from <see cref="ShipFrameShape"/>), extruded straight down, with sides. UVs run in metres
    /// along the face's longest side, so boards run lengthwise and keep their size on any face.</item>
    /// <item>Walls: a planked wall face (corners on two stories) is a slab of the same thickness centred on its plane,
    /// built with the lower story.</item>
    /// <item>Railings: posts and two rails along each floor edge marked as railed, <see cref="ShipFrameTuning.RailHeight"/>
    /// high, built with the beams (so they have colliders: nobody walks off a railed edge).</item>
    /// <item>Beams: a square prism <see cref="ShipFrameTuning.BeamWidth"/> wide along each edge, and a block at each node to
    /// cover the joints. Floor beams hang just under the floor's top (no step to trip on, no z-fighting); posts are
    /// centred on their line.</item>
    /// </list>
    /// The same frame always gives the same meshes. Unknown references are skipped (the validator reports them).
    /// </summary>
    public sealed class ShipFrameMeshProcessor
    {
        /// <summary>How far under the floor's top the beams' tops sit.</summary>
        private const float BeamDrop = 0.01f;
        /// <summary>A node block is this much wider than a beam.</summary>
        private const float NodeBlockScale = 1.25f;
        /// <summary>Railing posts and rails: their width (m), and the most a span between posts may be (m).</summary>
        private const float RailWidth = 0.08f;
        private const float RailPostSpacing = 1.2f;

        public IReadOnlyList<ShipFrameStoryMesh> Build(ShipFrame frame, ShipFrameTuning tuning) => Build(ShipFrameShape.From(frame), tuning);

        public IReadOnlyList<ShipFrameStoryMesh> Build(ShipFrameShape shape, ShipFrameTuning tuning)
        {
            var frame = shape.Frame;
            var stories = new List<ShipFrameStoryMesh>();
            for (int story = 0; story < frame.StoryHeights.Count; story++)
            {
                var planks = new ShipFrameMesh();
                foreach (var face in shape.PlankFaces.Where(f => f.Story == story))
                {
                    if (face.IsWall) AddWall(planks, face, tuning);
                    else AddSlab(planks, face, tuning);
                }

                var beams = new ShipFrameMesh();
                foreach (var edge in shape.Edges.Where(e => e.Story == story))
                {
                    AddBeam(beams, edge, tuning);
                    if (edge.Edge.Railing) AddRailing(beams, edge, tuning);
                }
                foreach (var node in frame.Nodes.Where(n => n.Story == story)) AddNodeBlock(beams, frame.Position(node), tuning);

                stories.Add(new ShipFrameStoryMesh(story, Fingerprint(shape, story), planks, beams));
            }

            return stories;
        }

        /// <summary>A stable hash of what a story's meshes are built from: its floor, nodes, edges and faces.</summary>
        public static ulong Fingerprint(ShipFrameShape shape, int story)
        {
            var frame = shape.Frame;
            ulong hash = 14695981039346656037UL;
            void Add(int value)
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= 1099511628211UL;
                }
            }

            void AddPoint(Vector3 point)
            {
                Add(ShipFrameUnits.FromMetres(point.x));
                Add(ShipFrameUnits.FromMetres(point.y));
                Add(ShipFrameUnits.FromMetres(point.z));
            }

            Add(story);
            Add(frame.StoryFloor(story));
            foreach (var node in frame.Nodes.Where(n => n.Story == story))
            {
                Add(node.Id);
                AddPoint(frame.Position(node));
            }

            foreach (var edge in shape.Edges.Where(e => e.Story == story))
            {
                Add(edge.Edge.Id);
                Add(edge.Edge.Railing ? 1 : 0);
                AddPoint(edge.From);
                AddPoint(edge.To);
            }

            foreach (var face in shape.PlankFaces.Where(f => f.Story == story))
            {
                Add(face.Face.Id);
                foreach (var corner in face.Corners) AddPoint(corner);
            }

            return hash;
        }

        private static void AddSlab(ShipFrameMesh mesh, ShipFrameShape.FaceShape face, ShipFrameTuning tuning)
        {
            var corners = face.Corners;
            var (along, across) = BoardAxes(corners);
            Vector2 Uv(Vector3 p) => new Vector2(Vector3.Dot(p, along), Vector3.Dot(p, across)) / tuning.TextureMetres;
            var down = Vector3.down * tuning.PlankThickness;

            // Top and bottom share their corners within each surface.
            var top = corners.Select(c => mesh.AddVertex(c, Uv(c))).ToList();
            var bottom = corners.Select(c => mesh.AddVertex(c + down, Uv(c))).ToList();
            for (int i = 0; i + 2 < face.Triangles.Count; i += 3)
            {
                int a = face.Triangles[i], b = face.Triangles[i + 1], c = face.Triangles[i + 2];
                mesh.AddTriangle(top[a], top[b], top[c]);
                mesh.AddTriangle(bottom[a], bottom[c], bottom[b]);
            }

            // Sides face away from the inside: the inside is on the left of a side for a loop that turns that way.
            float turn = ShipFrameGeometry.SignedArea2(corners);
            for (int i = 0; i < corners.Count; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Count];
                var side = b - a;
                var outward = new Vector3(side.z, 0f, -side.x) * (turn > 0f ? 1f : -1f);
                float length = new Vector2(side.x, side.z).magnitude / tuning.TextureMetres;
                float depth = tuning.PlankThickness / tuning.TextureMetres;
                mesh.AddQuad(a, b, b + down, a + down, outward, new Vector2(0f, 0f), new Vector2(length, 0f), new Vector2(length, depth), new Vector2(0f, depth));
            }
        }

        /// <summary>The direction of the face's longest side (seen from above) and the one across it.</summary>
        private static (Vector3 Along, Vector3 Across) BoardAxes(IReadOnlyList<Vector3> corners)
        {
            var along = Vector3.forward;
            float longest = -1f;
            for (int i = 0; i < corners.Count; i++)
            {
                var side = corners[(i + 1) % corners.Count] - corners[i];
                side.y = 0f;
                if (side.sqrMagnitude <= longest) continue;
                longest = side.sqrMagnitude;
                along = side.normalized;
            }

            return (along, new Vector3(along.z, 0f, -along.x));
        }

        /// <summary>
        /// A wall face: a slab as thick as a floor, centred on the face's plane (it has no inside to grow towards), its
        /// front and back facing out of the plane, with sides. UVs run in metres along the bottom side, up the wall.
        /// </summary>
        private static void AddWall(ShipFrameMesh mesh, ShipFrameShape.FaceShape face, ShipFrameTuning tuning)
        {
            var corners = face.Corners;
            var facing = face.Facing.normalized;
            var half = facing * (tuning.PlankThickness * 0.5f);
            var along = LongestSide(corners);
            var up = Vector3.Cross(facing, along).normalized;
            Vector2 Uv(Vector3 p) => new Vector2(Vector3.Dot(p, along), Vector3.Dot(p, up)) / tuning.TextureMetres;

            var front = corners.Select(c => mesh.AddVertex(c + half, Uv(c))).ToList();
            var back = corners.Select(c => mesh.AddVertex(c - half, Uv(c))).ToList();
            for (int i = 0; i + 2 < face.Triangles.Count; i += 3)
            {
                int a = face.Triangles[i], b = face.Triangles[i + 1], c = face.Triangles[i + 2];
                mesh.AddTriangle(front[a], front[b], front[c]);
                mesh.AddTriangle(back[a], back[c], back[b]);
            }

            var centre = corners.Aggregate(Vector3.zero, (sum, c) => sum + c) / corners.Count;
            for (int i = 0; i < corners.Count; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Count];
                var outward = Vector3.Cross(b - a, facing);
                if (Vector3.Dot(outward, (a + b) * 0.5f - centre) < 0f) outward = -outward;
                float length = Vector3.Distance(a, b) / tuning.TextureMetres;
                float depth = tuning.PlankThickness / tuning.TextureMetres;
                mesh.AddQuad(a + half, b + half, b - half, a - half, outward, Vector2.zero, new Vector2(length, 0f), new Vector2(length, depth), new Vector2(0f, depth));
            }
        }

        /// <summary>The direction of a loop's longest side, in space.</summary>
        private static Vector3 LongestSide(IReadOnlyList<Vector3> corners)
        {
            var along = Vector3.right;
            float longest = -1f;
            for (int i = 0; i < corners.Count; i++)
            {
                var side = corners[(i + 1) % corners.Count] - corners[i];
                if (side.sqrMagnitude <= longest) continue;
                longest = side.sqrMagnitude;
                along = side.normalized;
            }

            return along;
        }

        /// <summary>
        /// A railing along a floor edge: posts at both ends and at most <see cref="RailPostSpacing"/> apart, a top rail at
        /// <see cref="ShipFrameTuning.RailHeight"/> and a middle rail, following the edge's slope.
        /// </summary>
        private static void AddRailing(ShipFrameMesh mesh, ShipFrameShape.EdgeShape edge, ShipFrameTuning tuning)
        {
            var axis = edge.To - edge.From;
            if (axis.sqrMagnitude < 1e-6f) return;

            var side = Vector3.Cross(Vector3.up, axis).normalized;
            float post = RailWidth * 0.5f;
            int spans = Mathf.Max(1, Mathf.CeilToInt(axis.magnitude / RailPostSpacing));
            for (int i = 0; i <= spans; i++)
            {
                var foot = edge.From + axis * ((float)i / spans);
                AddBox(mesh, foot, foot + Vector3.up * tuning.RailHeight, side * post, Vector3.Cross(Vector3.up, side).normalized * post, tuning.TextureMetres);
            }

            foreach (float height in new[] { tuning.RailHeight, tuning.RailHeight * 0.5f })
            {
                var lift = Vector3.up * height;
                var up = Vector3.Cross(axis, side).normalized;
                AddBox(mesh, edge.From + lift, edge.To + lift, side * post, up * (RailWidth * 0.4f), tuning.TextureMetres);
            }
        }

        private static void AddBeam(ShipFrameMesh mesh, ShipFrameShape.EdgeShape edge, ShipFrameTuning tuning)
        {
            var axis = edge.To - edge.From;
            if (axis.sqrMagnitude < 1e-6f) return;

            bool post = Mathf.Abs(axis.normalized.y) > 0.7f;
            var reference = post ? Vector3.forward : Vector3.up;
            var side = Vector3.Cross(reference, axis).normalized;
            var up = Vector3.Cross(axis, side).normalized;
            if (up.y < 0f) up = -up;

            float half = tuning.BeamWidth * 0.5f;
            var offset = post ? Vector3.zero : Vector3.down * (half + BeamDrop);
            AddBox(mesh, edge.From + offset, edge.To + offset, side * half, up * half, tuning.TextureMetres);
        }

        private static void AddNodeBlock(ShipFrameMesh mesh, Vector3 node, ShipFrameTuning tuning)
        {
            float half = tuning.BeamWidth * NodeBlockScale * 0.5f;
            var centre = node + Vector3.down * (half + BeamDrop);
            AddBox(mesh, centre + Vector3.back * half, centre + Vector3.forward * half, Vector3.right * half, Vector3.up * half, tuning.TextureMetres);
        }

        /// <summary>A box from one end centre to the other, its cross-section spanning ±side and ±up.</summary>
        private static void AddBox(ShipFrameMesh mesh, Vector3 from, Vector3 to, Vector3 side, Vector3 up, float textureMetres)
        {
            var centre = (from + to) * 0.5f;
            float length = Vector3.Distance(from, to) / textureMetres;
            float width = side.magnitude * 2f / textureMetres;
            var uv0 = Vector2.zero;
            var uvLength = new Vector2(length, 0f);
            var uvFull = new Vector2(length, width);
            var uvWidth = new Vector2(0f, width);

            Vector3 Corner(Vector3 end, float s, float u) => end + side * s + up * u;
            // The four long sides, then the two end caps.
            foreach (var (s0, u0, s1, u1) in new[] { (1f, 1f, -1f, 1f), (-1f, 1f, -1f, -1f), (-1f, -1f, 1f, -1f), (1f, -1f, 1f, 1f) })
            {
                var a = Corner(from, s0, u0);
                var b = Corner(to, s0, u0);
                var c = Corner(to, s1, u1);
                var d = Corner(from, s1, u1);
                mesh.AddQuad(a, b, c, d, (a + c) * 0.5f - centre, uv0, uvLength, uvFull, uvWidth);
            }

            foreach (var end in new[] { from, to })
            {
                var a = Corner(end, 1f, 1f);
                var b = Corner(end, -1f, 1f);
                var c = Corner(end, -1f, -1f);
                var d = Corner(end, 1f, -1f);
                mesh.AddQuad(a, b, c, d, end - centre, uv0, new Vector2(width, 0f), new Vector2(width, width), uvWidth);
            }
        }
    }
}
