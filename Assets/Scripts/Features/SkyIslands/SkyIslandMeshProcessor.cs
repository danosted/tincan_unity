#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Domain, pure: the shape of one island, in its own space with the middle of its top at the origin. A rolling
    /// grassy disc whose edge wanders, a rounded rim, then rings of craggy rock that taper down to a point hanging
    /// <see cref="SkyIslandSpec.Depth"/> below. One closed mesh, wound so faces point out (clockwise seen from outside,
    /// as Unity expects), with smooth normals and the material masks in the vertex colours (<see cref="SkyIslandMeshData"/>).
    /// The same island at two resolutions gives its visual and its collision mesh.
    /// </summary>
    public class SkyIslandMeshProcessor
    {
        public void Build(in SkyIslandSpec island, SkyIslandMeshResolution resolution, SkyIslandMeshData into)
        {
            into.Clear();
            var cut = resolution.Clamped;
            var shape = new SkyIslandShape(island);
            int segments = cut.Segments;

            // Centre of the top.
            AddVertex(into, new Vector3(0f, shape.Height(0f, 0f, 0f), 0f), 255, 255, 128, 0f);

            // The top: rings out to just inside the rim.
            for (int ring = 1; ring <= cut.TopRings; ring++)
            {
                float reach = SkyIslandShape.TopReach * ring / cut.TopRings;
                for (int i = 0; i < segments; i++)
                {
                    float angle = Angle(i, segments);
                    float radius = shape.Edge(angle) * reach;
                    float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
                    AddVertex(into, new Vector3(x, shape.Height(x, z, reach), z), 255, 255, 128, 0f);
                }
            }

            // The rim, rounded a little down.
            for (int i = 0; i < segments; i++)
            {
                float angle = Angle(i, segments);
                float radius = shape.Edge(angle);
                AddVertex(into, new Vector3(Mathf.Cos(angle) * radius, -shape.Lip, Mathf.Sin(angle) * radius), 200, 235, 128, 0f);
            }

            // The underside: craggy rings tapering to the apex, drifting toward it. Under a spire the rock drops steadily;
            // between spires it stays high and tucks in late (each angle has its own curve down), and each ring steps in or
            // out a little (the ledges).
            for (int ring = 1; ring <= cut.UnderRings; ring++)
            {
                float down = (float)ring / (cut.UnderRings + 1);
                byte strata = (byte)(shape.Strata(ring) * 255f);
                float ledge = shape.Ledge(ring);
                var drift = shape.ApexOffset * Mathf.Pow(down, 1.5f);
                for (int i = 0; i < segments; i++)
                {
                    float angle = Angle(i, segments);
                    float y = -shape.Lip - (island.Depth - shape.Lip) * Mathf.Pow(down, shape.Hang(angle));
                    float crag = shape.Crag(angle, down);
                    float radius = shape.Under(angle, down, crag, ledge);
                    byte grass = ring == 1 ? (byte)50 : (byte)0;
                    float depth = Mathf.Clamp01(-y / island.Depth);
                    float occlusion = Mathf.Lerp(0.85f, 0.45f, depth) - 0.15f * Mathf.Max(0f, -crag);
                    AddVertex(into,
                        new Vector3(drift.x + Mathf.Cos(angle) * radius, y, drift.y + Mathf.Sin(angle) * radius),
                        grass, (byte)(Mathf.Clamp01(occlusion) * 255f), strata, depth);
                }
            }

            // The apex.
            AddVertex(into, new Vector3(shape.ApexOffset.x, -island.Depth, shape.ApexOffset.y), 0, 90, 128, 1f);

            Triangulate(into, segments, cut.TopRings + 1 + cut.UnderRings);
            SmoothNormals(into);
        }

        /// <summary>
        /// The island at this resolution as convex pieces: for every pair of neighbouring levels (the top's centre, each
        /// ring, the apex), wedges of <paramref name="wedgeSegments"/> segments, each with the middles of its two levels so
        /// the pieces meet at the axis. Coarse rings (no more than two wedges' worth) make one piece per slab.
        /// With <paramref name="topPrismSegments"/> above 0 (a hilly landmass), the top, out to the rim, is cut into upright
        /// prisms that many segments wide instead: each patch of ground down to just under the rim, so a hull never
        /// spans a valley and makes a floor over it. The underside below the rim stays in wedges.
        /// </summary>
        public void BuildPieces(in SkyIslandSpec island, SkyIslandMeshResolution resolution, int wedgeSegments,
            SkyIslandMeshData scratch, SkyIslandCollisionPieces into, int topPrismSegments = 0)
        {
            Build(island, resolution, scratch);
            into.Clear();

            var cut = resolution.Clamped;
            int segments = cut.Segments;
            int rings = cut.TopRings + 1 + cut.UnderRings;
            int wedge = segments <= 2 * Mathf.Max(3, wedgeSegments) ? segments : Mathf.Max(3, wedgeSegments);
            int wedges = Mathf.CeilToInt((float)segments / wedge);
            var vertices = scratch.Vertices;
            int apex = vertices.Count - 1;

            // Levels: -1 is the top's centre, 0..rings-1 the rings (TopRings is the rim), rings the apex.
            int firstWedgeLevel = -1;
            if (topPrismSegments > 0)
            {
                firstWedgeLevel = cut.TopRings;
                AddTopPrisms(into, vertices, cut.TopRings, segments, Mathf.Clamp(topPrismSegments, 1, segments),
                    -new SkyIslandShape(island).Lip - 1f);
            }

            for (int level = firstWedgeLevel; level < rings; level++)
            {
                for (int w = 0; w < wedges; w++)
                {
                    int first = w * wedge, last = Mathf.Min(segments, first + wedge);
                    into.BeginPiece();
                    AddLevel(into, vertices, level, first, last, segments, rings, apex);
                    AddLevel(into, vertices, level + 1, first, last, segments, rings, apex);
                }
            }
        }

        /// <summary>
        /// Upright prisms over the top, from the centre out to the rim: for every pair of neighbouring levels, patches
        /// <paramref name="span"/> segments wide, each with its ground points and the same points dropped to
        /// <paramref name="bottom"/>.
        /// </summary>
        private static void AddTopPrisms(SkyIslandCollisionPieces into, List<Vector3> vertices, int topRings, int segments,
            int span, float bottom)
        {
            int patches = Mathf.CeilToInt((float)segments / span);
            for (int level = -1; level < topRings; level++)
            {
                for (int p = 0; p < patches; p++)
                {
                    int first = p * span, last = Mathf.Min(segments, first + span);
                    into.BeginPiece();
                    AddPrismLevel(into, vertices, level, first, last, segments, bottom);
                    AddPrismLevel(into, vertices, level + 1, first, last, segments, bottom);
                }
            }
        }

        /// <summary>A level's points across one patch (both ends included), each with its copy at the bottom.</summary>
        private static void AddPrismLevel(SkyIslandCollisionPieces into, List<Vector3> vertices, int level, int first, int last,
            int segments, float bottom)
        {
            if (level < 0)
            {
                into.Points.Add(vertices[0]);
                into.Points.Add(new Vector3(vertices[0].x, bottom, vertices[0].z));
                return;
            }

            int start = 1 + level * segments;
            for (int i = first; i <= last; i++)
            {
                var point = vertices[start + i % segments];
                into.Points.Add(point);
                into.Points.Add(new Vector3(point.x, Mathf.Min(bottom, point.y - 0.5f), point.z));
            }
        }

        /// <summary>A level's points across one wedge (both ends included), plus its middle; the centre and apex are one point.</summary>
        private static void AddLevel(SkyIslandCollisionPieces into, List<Vector3> vertices, int level, int first, int last,
            int segments, int rings, int apex)
        {
            if (level < 0)
            {
                into.Points.Add(vertices[0]);
                return;
            }
            if (level >= rings)
            {
                into.Points.Add(vertices[apex]);
                return;
            }

            int start = 1 + level * segments;
            var middle = Vector3.zero;
            for (int i = 0; i < segments; i++) middle += vertices[start + i];
            into.Points.Add(middle / segments);
            for (int i = first; i <= last; i++) into.Points.Add(vertices[start + i % segments]);
        }

        private static void Triangulate(SkyIslandMeshData mesh, int segments, int rings)
        {
            int apex = mesh.Vertices.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.Triangles.Add(0);
                mesh.Triangles.Add(1 + next);
                mesh.Triangles.Add(1 + i);
            }

            for (int ring = 0; ring < rings - 1; ring++)
            {
                int upper = 1 + ring * segments, lower = upper + segments;
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    mesh.Triangles.Add(upper + i);
                    mesh.Triangles.Add(upper + next);
                    mesh.Triangles.Add(lower + i);

                    mesh.Triangles.Add(upper + next);
                    mesh.Triangles.Add(lower + next);
                    mesh.Triangles.Add(lower + i);
                }
            }

            int last = 1 + (rings - 1) * segments;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.Triangles.Add(last + i);
                mesh.Triangles.Add(last + next);
                mesh.Triangles.Add(apex);
            }
        }

        /// <summary>Area-weighted face normals summed per vertex. The rim gets a soft, rounded edge.</summary>
        private static void SmoothNormals(SkyIslandMeshData mesh)
        {
            mesh.Normals.Clear();
            for (int i = 0; i < mesh.Vertices.Count; i++) mesh.Normals.Add(Vector3.zero);

            var triangles = mesh.Triangles;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                var face = Vector3.Cross(mesh.Vertices[b] - mesh.Vertices[a], mesh.Vertices[c] - mesh.Vertices[a]);
                mesh.Normals[a] += face;
                mesh.Normals[b] += face;
                mesh.Normals[c] += face;
            }

            for (int i = 0; i < mesh.Normals.Count; i++) mesh.Normals[i] = mesh.Normals[i].normalized;
        }

        private static void AddVertex(SkyIslandMeshData mesh, Vector3 position, byte grass, byte occlusion, byte strata, float down)
        {
            mesh.Vertices.Add(position);
            mesh.Colors.Add(new Color32(grass, occlusion, strata, (byte)(Mathf.Clamp01(down) * 255f)));
        }

        private static float Angle(int index, int segments) => index * 2f * Mathf.PI / segments;
    }
}
