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
            var shape = new Shape(island);
            int segments = cut.Segments;

            // Centre of the top.
            AddVertex(into, new Vector3(0f, shape.Height(0f, 0f, 0f), 0f), 255, 255, 128, 0f);

            // The top: rings out to just inside the rim.
            for (int ring = 1; ring <= cut.TopRings; ring++)
            {
                float reach = 0.93f * ring / cut.TopRings;
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
        /// </summary>
        public void BuildPieces(in SkyIslandSpec island, SkyIslandMeshResolution resolution, int wedgeSegments,
            SkyIslandMeshData scratch, SkyIslandCollisionPieces into)
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

            // Levels: -1 is the top's centre, 0..rings-1 the rings, rings the apex.
            for (int level = -1; level < rings; level++)
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

        /// <summary>One island's character, rolled from its shape seed.</summary>
        private readonly struct Shape
        {
            private readonly uint _seed;
            private readonly float _radius;
            private readonly float _edgeAmount;
            private readonly float _edgeScale;
            private readonly float _hill;
            private readonly float _taper;
            private readonly float _cragAmount;
            private readonly float _lobeAmount;
            private readonly float _ledgeAmount;
            private readonly float _tuckIn;
            private readonly int _spires;
            private readonly float _spireWidth;
            private readonly float _spire0, _spire1, _spire2;
            private readonly float _height1, _height2;

            public readonly float Lip;
            public readonly Vector2 ApexOffset;

            public Shape(in SkyIslandSpec island)
            {
                var random = new SkyIslandRandom(island.ShapeSeed);
                _seed = island.ShapeSeed;
                _radius = island.Radius;
                _edgeAmount = random.Range(0.1f, 0.22f);
                _edgeScale = random.Range(1.1f, 2.2f);
                _hill = island.Radius * random.Range(0.03f, 0.08f);
                _taper = random.Range(0.6f, 1.3f);
                _cragAmount = random.Range(0.2f, 0.38f);
                _lobeAmount = random.Range(0.08f, 0.2f);
                _ledgeAmount = random.Range(0.04f, 0.12f);
                Lip = 0.5f + island.Radius * random.Range(0.02f, 0.05f);

                // Up to three spires: the deepest (the apex) and up to two shorter ones; between them the rock tucks in higher.
                _tuckIn = random.Range(1.8f, 2.8f);
                _spires = random.Range(1, 3);
                _spireWidth = random.Range(0.9f, 1.6f);
                _spire0 = random.Range(0f, 2f * Mathf.PI);
                _spire1 = _spire0 + random.Range(1.6f, 2.6f);
                _spire2 = _spire0 - random.Range(1.6f, 2.6f);
                _height1 = random.Range(0.7f, 0.92f);
                _height2 = random.Range(0.65f, 0.9f);
                ApexOffset = new Vector2(Mathf.Cos(_spire0), Mathf.Sin(_spire0)) * island.Radius * random.Range(0.1f, 0.3f);
            }

            /// <summary>The top's edge at this angle: within [1 - edge amount, 1] of the radius, so the radius is the most.</summary>
            public float Edge(float angle)
            {
                float noise = SkyIslandNoise.Fractal(_seed, Mathf.Cos(angle) * _edgeScale + 11f, 0f, Mathf.Sin(angle) * _edgeScale + 11f, 3);
                return _radius * (1f - _edgeAmount * (0.5f + 0.5f * noise));
            }

            /// <summary>Rolling ground, highest near the middle, flattening toward the rim.</summary>
            public float Height(float x, float z, float reach)
            {
                float scale = 2f / Mathf.Max(1f, _radius);
                float noise = SkyIslandNoise.Fractal(_seed + 101u, x * scale + 31f, 0f, z * scale + 31f, 3);
                float falloff = 1f - reach * reach;
                return (_hill * noise + _hill * 0.6f) * falloff;
            }

            /// <summary>Craggy noise on the underside at this angle and depth, in [-1, 1].</summary>
            public float Crag(float angle, float down) =>
                SkyIslandNoise.Fractal(_seed + 211u, Mathf.Cos(angle) * 3.5f + 53f, down * 5f, Mathf.Sin(angle) * 3.5f + 53f, 3);

            /// <summary>
            /// The underside's radius: tapering with depth, crags growing in below the rim, never past the rim above.
            /// </summary>
            public float Under(float angle, float down, float crag, float ledge)
            {
                float edge = Edge(angle);
                float taper = Mathf.Pow(1f - down, _taper);
                float grow = Mathf.Min(1f, down * 4f);
                float lobe = SkyIslandNoise.Value(_seed + 307u,
                    Mathf.Cos(angle) * 1.2f + 71f, down * 1.2f, Mathf.Sin(angle) * 1.2f + 71f);
                float rough = (1f + _cragAmount * grow * crag) * (1f + _lobeAmount * grow * lobe) * (1f + ledge * grow);
                return Mathf.Min(edge * 0.97f * taper * rough, edge * 0.98f);
            }

            /// <summary>
            /// How the rock drops at this angle: the exponent on the share of the way down. 1 under the deepest spire (a
            /// steady drop), up to <c>_tuckIn</c> between spires (it stays high and curves in late). Every angle still ends
            /// at the apex.
            /// </summary>
            public float Hang(float angle)
            {
                float spire = Bump(angle, _spire0);
                if (_spires >= 2) spire = Mathf.Max(spire, _height1 * Bump(angle, _spire1));
                if (_spires >= 3) spire = Mathf.Max(spire, _height2 * Bump(angle, _spire2));
                return Mathf.Lerp(_tuckIn, 1f, spire);
            }

            /// <summary>A ring stepping in or out, for ledges between the rock layers: within plus or minus the ledge amount.</summary>
            public float Ledge(int ring) =>
                _ledgeAmount * (new SkyIslandRandom(SkyIslandRandom.Hash((int)_seed, ring, 613)).Value() * 2f - 1f);

            /// <summary>1 at the spire's angle, falling smoothly to 0 one spire width away.</summary>
            private float Bump(float angle, float at)
            {
                float apart = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, at * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                float t = Mathf.Clamp01(1f - apart / _spireWidth);
                return t * t * (3f - 2f * t);
            }

            /// <summary>A rock layer's tone, one per ring.</summary>
            public float Strata(int ring) => new SkyIslandRandom(SkyIslandRandom.Hash((int)_seed, ring, 977)).Value();
        }
    }
}
