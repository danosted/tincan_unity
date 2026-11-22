#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Domain, pure: a cave rock's shape (<see cref="SkyIslandCave"/>), in island space. A lumpy dome of rock with a wall
    /// of even thickness, standing on a short skirt that sinks into the ground, with a mouth cut low on the side it
    /// faces: a gap one column wide from under the ground up to 45 degrees of the dome. It is a grid of columns (round)
    /// and rings (up), the same point for the same cell at every resolution, so the visual (twice as fine) and the
    /// collision meet. The visual is appended to an island's mesh (outer rock mossy on top, the chamber dark, the mouth's
    /// edges closed); the collision is one convex piece per cell of the shell (outer and inner corners), so the ship's
    /// push-out and the ground probe treat it as island rock.
    /// </summary>
    public class SkyIslandCaveMeshProcessor
    {
        /// <summary>Columns round the dome at the collision's resolution; the mouth is one of them.</summary>
        public const int Columns = 12;

        /// <summary>Rings from the foot up to just under the top, at the collision's resolution; the mouth is the lower half.</summary>
        public const int Rings = 4;

        /// <summary>How far the skirt reaches below the dome's foot, metres.</summary>
        public const float Skirt = 2.5f;

        /// <summary>The visual is this many times finer than the collision, both ways.</summary>
        public const int VisualFactor = 2;

        private const float Lumps = 0.14f;

        private readonly List<int> _faces = new();

        /// <summary>Appends the cave's drawn rock to <paramref name="into"/> (its own vertices, normals and colours).</summary>
        public void AppendVisual(in SkyIslandCave cave, SkyIslandMeshData into)
        {
            int f = VisualFactor;
            int columns = Columns * f, rings = Rings * f;
            int start = into.Vertices.Count;
            _faces.Clear();

            int outer = AddShell(cave, into, f, outside: true);
            int inner = AddShell(cave, into, f, outside: false);
            int ringCount = rings + 1; // the skirt's foot and the rings, then the pole

            for (int row = 0; row <= rings; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (IsMouth(column, row, f)) continue;
                    int next = (column + 1) % columns;
                    if (row < rings)
                    {
                        var outward = Middle(into, Index(outer, row, column, columns), Index(outer, row + 1, next, columns)) - Axis(cave, into, outer, row, columns);
                        Quad(into, Index(outer, row, column, columns), Index(outer, row + 1, column, columns),
                            Index(outer, row + 1, next, columns), Index(outer, row, next, columns), outward);
                        Quad(into, Index(inner, row, column, columns), Index(inner, row + 1, column, columns),
                            Index(inner, row + 1, next, columns), Index(inner, row, next, columns), -outward);
                    }
                    else
                    {
                        int outerPole = outer + ringCount * columns, innerPole = inner + ringCount * columns;
                        Triangle(into, Index(outer, row, column, columns), outerPole, Index(outer, row, next, columns), Vector3.up);
                        Triangle(into, Index(inner, row, column, columns), innerPole, Index(inner, row, next, columns), Vector3.down);
                    }
                }
            }

            AddMouthEdges(cave, into, f, outer, inner, columns);
            Normals(into, start);
        }

        /// <summary>Appends one convex piece per cell of the shell (the mouth left open).</summary>
        public void AppendPieces(in SkyIslandCave cave, SkyIslandCollisionPieces into)
        {
            for (int row = 0; row <= Rings; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    if (IsMouth(column, row, 1)) continue;
                    into.BeginPiece();
                    foreach (bool outside in new[] { true, false })
                    {
                        into.Points.Add(Point(cave, column, row, outside, 1));
                        into.Points.Add(Point(cave, column + 1, row, outside, 1));
                        if (row < Rings)
                        {
                            into.Points.Add(Point(cave, column, row + 1, outside, 1));
                            into.Points.Add(Point(cave, column + 1, row + 1, outside, 1));
                        }
                        else into.Points.Add(Pole(cave, outside));
                    }
                }
            }
        }

        /// <summary>
        /// Is this cell part of the mouth? Column 0 (centred on the facing) in the rows from the skirt up to 45 degrees,
        /// at a resolution <paramref name="factor"/> times the collision's.
        /// </summary>
        public static bool IsMouth(int column, int row, int factor) => column < factor && row <= 2 * factor;

        /// <summary>
        /// A point of the shell: column round from the mouth's right edge, ring up (0 the skirt's foot, 1 the dome's foot,
        /// then up to just under the top), on the outside or the chamber's side.
        /// </summary>
        public static Vector3 Point(in SkyIslandCave cave, int column, int ring, bool outside, int factor)
        {
            int columns = Columns * factor, rings = Rings * factor;
            float azimuth = cave.Facing - Mathf.PI / Columns + (column % columns) * 2f * Mathf.PI / columns;
            float elevation = ring <= 1 ? 0f : 0.5f * Mathf.PI * (ring - 1) / rings;
            float lump = Lump(cave, azimuth, elevation);
            float radius = (outside ? cave.Radius + cave.Wall : cave.Radius) * lump;
            float height = (outside ? cave.Height + cave.Wall : cave.Height) * lump;
            float level = Mathf.Cos(elevation) * radius;
            float y = ring == 0 ? -Skirt : Mathf.Sin(elevation) * height;
            return cave.Base + new Vector3(Mathf.Cos(azimuth) * level, y, Mathf.Sin(azimuth) * level);
        }

        /// <summary>The top of the dome, outside or in.</summary>
        public static Vector3 Pole(in SkyIslandCave cave, bool outside)
        {
            float lump = Lump(cave, 0f, 0.5f * Mathf.PI);
            return cave.Base + Vector3.up * ((outside ? cave.Height + cave.Wall : cave.Height) * lump);
        }

        private static float Lump(in SkyIslandCave cave, float azimuth, float elevation) =>
            1f + Lumps * SkyIslandNoise.Fractal(cave.Seed, Mathf.Cos(azimuth) * 1.3f + 5f, elevation * 1.6f, Mathf.Sin(azimuth) * 1.3f + 5f, 2);

        /// <summary>One side of the shell's vertices (rings by columns, then the pole); returns where they start.</summary>
        private static int AddShell(in SkyIslandCave cave, SkyIslandMeshData into, int factor, bool outside)
        {
            int start = into.Vertices.Count;
            int columns = Columns * factor, rings = Rings * factor;
            for (int ring = 0; ring <= rings; ring++)
            {
                byte strata = (byte)(new SkyIslandRandom(SkyIslandRandom.Hash((int)cave.Seed, ring, 977)).Value() * 255f);
                for (int column = 0; column < columns; column++)
                {
                    into.Vertices.Add(Point(cave, column, ring, outside, factor));
                    into.Colors.Add(Colour(ring, rings, outside, strata));
                }
            }
            into.Vertices.Add(Pole(cave, outside));
            into.Colors.Add(Colour(rings + 1, rings, outside, 128));
            return start;
        }

        /// <summary>Outside: moss over the top, rock down the sides, a little shade at the foot. Inside: dark rock.</summary>
        private static Color32 Colour(int ring, int rings, bool outside, byte strata)
        {
            float up = Mathf.Clamp01((ring - 1f) / rings);
            if (!outside) return new Color32(0, (byte)Mathf.Lerp(55f, 85f, up), strata, 70);
            byte grass = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, up)) * 255f);
            return new Color32(grass, (byte)Mathf.Lerp(150f, 230f, up), strata, 40);
        }

        /// <summary>The mouth's two sides and its lintel, closing the wall's thickness, each face its own vertices.</summary>
        private void AddMouthEdges(in SkyIslandCave cave, SkyIslandMeshData into, int factor, int outer, int inner, int columns)
        {
            int top = 2 * factor + 1; // the ring at the mouth's top
            for (int ring = 0; ring < top; ring++)
            {
                // The side at column 0 faces into the mouth (toward +azimuth), the one at column factor away from it.
                EdgeQuad(into, Index(outer, ring, 0, columns), Index(outer, ring + 1, 0, columns),
                    Index(inner, ring + 1, 0, columns), Index(inner, ring, 0, columns), Tangent(cave, 0, factor));
                EdgeQuad(into, Index(outer, ring, factor, columns), Index(outer, ring + 1, factor, columns),
                    Index(inner, ring + 1, factor, columns), Index(inner, ring, factor, columns), -Tangent(cave, factor, factor));
            }
            for (int column = 0; column < factor; column++)
            {
                EdgeQuad(into, Index(outer, top, column, columns), Index(outer, top, column + 1, columns),
                    Index(inner, top, column + 1, columns), Index(inner, top, column, columns), Vector3.down);
            }
        }

        /// <summary>The level direction of growing azimuth at a column.</summary>
        private static Vector3 Tangent(in SkyIslandCave cave, int column, int factor)
        {
            float azimuth = cave.Facing - Mathf.PI / Columns + column * 2f * Mathf.PI / (Columns * factor);
            return new Vector3(-Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
        }

        private void EdgeQuad(SkyIslandMeshData mesh, int a, int b, int c, int d, Vector3 facing)
        {
            int start = mesh.Vertices.Count;
            foreach (int source in new[] { a, b, c, d })
            {
                mesh.Vertices.Add(mesh.Vertices[source]);
                mesh.Colors.Add(new Color32(0, 140, mesh.Colors[source].b, 60));
            }
            Quad(mesh, start, start + 1, start + 2, start + 3, facing);
        }

        private static int Index(int start, int ring, int column, int columns) => start + ring * columns + column % columns;

        private static Vector3 Middle(SkyIslandMeshData mesh, int a, int b) => (mesh.Vertices[a] + mesh.Vertices[b]) * 0.5f;

        /// <summary>The point on the dome's axis level with a ring's middle row, to tell outward from inward.</summary>
        private static Vector3 Axis(in SkyIslandCave cave, SkyIslandMeshData mesh, int shell, int ring, int columns)
        {
            float y = (mesh.Vertices[Index(shell, ring, 0, columns)].y + mesh.Vertices[Index(shell, ring + 1, 0, columns)].y) * 0.5f;
            return new Vector3(cave.Base.x, y, cave.Base.z);
        }

        /// <summary>Two triangles, wound so the face points along <paramref name="facing"/> (clockwise seen from there).</summary>
        private void Quad(SkyIslandMeshData mesh, int a, int b, int c, int d, Vector3 facing)
        {
            Triangle(mesh, a, b, c, facing);
            Triangle(mesh, a, c, d, facing);
        }

        private void Triangle(SkyIslandMeshData mesh, int a, int b, int c, Vector3 facing)
        {
            var v = mesh.Vertices;
            var normal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            bool flip = Vector3.Dot(normal, facing) < 0f;
            mesh.Triangles.Add(a);
            mesh.Triangles.Add(flip ? c : b);
            mesh.Triangles.Add(flip ? b : c);
            _faces.Add(mesh.Triangles.Count - 3);
        }

        /// <summary>Area-weighted smooth normals for the vertices from <paramref name="start"/> on, from this cave's faces.</summary>
        private void Normals(SkyIslandMeshData mesh, int start)
        {
            while (mesh.Normals.Count < start) mesh.Normals.Add(Vector3.up);
            for (int i = start; i < mesh.Vertices.Count; i++) mesh.Normals.Add(Vector3.zero);

            var triangles = mesh.Triangles;
            foreach (int t in _faces)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                var face = Vector3.Cross(mesh.Vertices[b] - mesh.Vertices[a], mesh.Vertices[c] - mesh.Vertices[a]);
                mesh.Normals[a] += face;
                mesh.Normals[b] += face;
                mesh.Normals[c] += face;
            }
            for (int i = start; i < mesh.Normals.Count; i++) mesh.Normals[i] = mesh.Normals[i].normalized;
        }
    }
}
