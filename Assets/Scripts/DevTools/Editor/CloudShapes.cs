#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Cloud meshes for <c>TinCan/Environment/Stylized Cloud</c>, from a seed: overlapping puffs (icospheres) merged into
    /// one mesh. Each vertex carries its puff's normal (NORMAL), the whole cloud's shape normal (TEXCOORD1), which the
    /// shader blends toward so the cloud shades as one mass, and its height in the cloud (TEXCOORD0.x, 0 base .. 1 top).
    /// The same seed gives the same mesh. Plan: .docs/plans/look-slice.md ("The cloud lab").
    /// </summary>
    public static class CloudShapes
    {
        /// <summary>How far a crease reaches from where two puffs meet, as a share of the smaller puff's radius (so a
        /// small puff on a big one only darkens at its foot).</summary>
        private const float CreaseReach = 0.8f;

        /// <summary>How a cumulus' lobes are built (<see cref="Cumulus"/>).</summary>
        public readonly struct CumulusStyle
        {
            public CumulusStyle(float lobeShape, float core, float reachMin, float reachMax, bool crownEveryLobe, float tuck)
            {
                LobeShape = lobeShape;
                Core = core;
                ReachMin = reachMin;
                ReachMax = reachMax;
                CrownEveryLobe = crownEveryLobe;
                Tuck = tuck;
            }

            /// <summary>How much a puff's shape normal follows its own lobe rather than the whole cloud, 0..1.</summary>
            public float LobeShape { get; }
            /// <summary>The core puff's radius, as a share of the lobe's smallest radius.</summary>
            public float Core { get; }
            /// <summary>How far from the lobe's centre its other puffs sit, as a share of its radii.</summary>
            public float ReachMin { get; }
            public float ReachMax { get; }
            /// <summary>Crown puffs shared out over every lobe (on its upper half), rather than drawn from all puffs at
            /// once (which favours the tall lobe).</summary>
            public bool CrownEveryLobe { get; }
            /// <summary>0..1: how far the side lobes are drawn in toward the middle and lowered, so the cloud reads as one
            /// heap rather than lobes in a row.</summary>
            public float Tuck { get; }

            /// <summary>The first lobed cumulus (2026-11-01), kept because it reads well from a distance.</summary>
            public static CumulusStyle LobesV1 =>
                new(lobeShape: 0.65f, core: 0.85f, reachMin: 0.3f, reachMax: 0.55f, crownEveryLobe: false, tuck: 0f);

            /// <summary>Lumpier lobes (a smaller core, puffs further out, crown puffs on every lobe), shaded more as one
            /// cloud, the side lobes drawn in and lowered.</summary>
            public static CumulusStyle Heap =>
                new(lobeShape: 0.4f, core: 0.7f, reachMin: 0.45f, reachMax: 0.7f, crownEveryLobe: true, tuck: 0.6f);
        }

        /// <summary>A heaped cumulus without lobes (the version before them, 2026-11-01): <paramref name="puffs"/> puffs in
        /// a dome over a flat base at y 0, <paramref name="size"/> wide, tall and deep (metres), larger puffs low and in the
        /// middle, all shaded by one ellipsoid; and <paramref name="crown"/> small puffs on the upper faces.</summary>
        public static Mesh Dome(string name, int seed, Vector3 size, int puffs, int crown, int subdivisions = 3)
        {
            var random = new System.Random(seed);
            var half = new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
            // The shape normal comes from an ellipsoid centred a little above the base, so the top and sides round off
            // and the base reads flat.
            var centre = new Vector3(0f, size.y * 0.3f, 0f);
            var radii = new Vector3(half.x, size.y * 0.75f, half.z);
            var list = new List<(Vector3 Centre, float Radius)>();
            for (int i = 0; i < puffs; i++)
            {
                // Height first, then a disc that narrows toward the top: a dome.
                float up = Mathf.Pow(Next(random), 1.4f);
                float spread = Mathf.Lerp(0.85f, 0.25f, up);
                float angle = Next(random) * Mathf.PI * 2f, reach = Mathf.Sqrt(Next(random)) * spread;
                var at = new Vector3(Mathf.Cos(angle) * reach * half.x, 0f, Mathf.Sin(angle) * reach * half.z);
                float radius = Mathf.Lerp(0.42f, 0.22f, up * 0.6f + reach * 0.4f) * Mathf.Min(half.x, half.z) * Mathf.Lerp(0.8f, 1.2f, Next(random));
                at.y = Mathf.Lerp(radius * 0.4f, size.y - radius, up);
                list.Add((at, radius));
            }

            int main = list.Count;
            for (int i = 0; i < crown; i++)
            {
                var (host, hostRadius) = list[random.Next(main)];
                var direction = new Vector3(Next(random) * 2f - 1f, Next(random) * 0.8f + 0.35f, Next(random) * 2f - 1f).normalized;
                var at = host + direction * hostRadius * 0.9f;
                float radius = hostRadius * Mathf.Lerp(0.2f, 0.34f, Next(random));
                if (at.y > size.y * 0.35f) list.Add((at, radius));
            }

            return Build(name, list, subdivisions, baseY: 0f, topY: size.y, (position, _, _) => Ellipsoid(position, centre, radii));
        }

        /// <summary>A cumulus of a few big lobes over a flat base at y 0, <paramref name="size"/> wide, tall and deep
        /// (metres): <paramref name="lobes"/> masses side by side, the middle ones tallest, each a cluster of
        /// <paramref name="puffsPerLobe"/> puffs shaded toward its own lobe's shape (so a lobe reads as one round mass),
        /// with creases only where lobes meet; and <paramref name="crown"/> small puffs on the upper faces, so only the top
        /// outline is lumpy. <paramref name="style"/> sets how (<see cref="CumulusStyle"/>).</summary>
        public static Mesh Cumulus(string name, int seed, Vector3 size, int lobes, int puffsPerLobe, int crown, CumulusStyle style, int subdivisions = 3)
        {
            var random = new System.Random(seed);
            var half = new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
            var cloudCentre = new Vector3(0f, size.y * 0.3f, 0f);
            var cloudRadii = new Vector3(half.x, size.y * 0.75f, half.z);
            var shapes = new List<(Vector3 Centre, Vector3 Radii)>();
            for (int lobe = 0; lobe < lobes; lobe++)
            {
                float across = lobes == 1 ? 0f : Mathf.Lerp(-1f, 1f, lobe / (float)(lobes - 1));
                float side = Mathf.Abs(across);
                float radius = half.x / lobes * Mathf.Lerp(1.15f, 1.45f, Next(random));
                float height = size.y * Mathf.Lerp(0.6f, 1f, 1f - side) * Mathf.Lerp(0.85f, 1f, Next(random)) * (1f - style.Tuck * 0.25f * side);
                var centre = new Vector3(across * Mathf.Max(half.x - radius, 0f) * (1f - style.Tuck * 0.35f), height * 0.45f,
                    (Next(random) - 0.5f) * half.z * 0.4f);
                shapes.Add((centre, new Vector3(radius, height * 0.55f, Mathf.Min(radius, half.z))));
            }

            var list = new List<(Vector3 Centre, float Radius)>();
            var groups = new List<int>();
            for (int lobe = 0; lobe < lobes; lobe++)
            {
                var (centre, radii) = shapes[lobe];
                float smallest = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
                // A core that fills the lobe, then puffs around it, mostly toward the top and sides.
                list.Add((centre, smallest * style.Core));
                groups.Add(lobe);
                for (int i = 0; i < puffsPerLobe; i++)
                {
                    var direction = new Vector3(Next(random) * 2f - 1f, Next(random) * 1.3f - 0.3f, Next(random) * 2f - 1f).normalized;
                    var at = centre + Vector3.Scale(direction * Mathf.Lerp(style.ReachMin, style.ReachMax, Next(random)), radii);
                    list.Add((at, smallest * Mathf.Lerp(0.5f, 0.72f, Next(random))));
                    groups.Add(lobe);
                }
            }

            int main = list.Count;
            for (int i = 0; i < crown; i++)
            {
                int host = style.CrownEveryLobe ? HostIn(i % lobes) : random.Next(main);
                var direction = new Vector3(Next(random) * 2f - 1f, Next(random) * 0.8f + 0.35f, Next(random) * 2f - 1f).normalized;
                var at = list[host].Centre + direction * list[host].Radius * 0.9f;
                float radius = list[host].Radius * Mathf.Lerp(0.2f, 0.34f, Next(random));
                float above = style.CrownEveryLobe ? shapes[groups[host]].Centre.y : size.y * 0.35f;
                if (at.y < above) continue;
                list.Add((at, radius));
                groups.Add(groups[host]);
            }

            return Build(name, list, subdivisions, baseY: 0f, topY: size.y,
                (position, _, puff) =>
                {
                    var (centre, radii) = shapes[groups[puff]];
                    return Vector3.Lerp(Ellipsoid(position, centre, radii), Ellipsoid(position, cloudCentre, cloudRadii), 1f - style.LobeShape).normalized;
                },
                groups);

            // A random puff of one lobe (its core and cluster sit together in the list).
            int HostIn(int lobe) => lobe * (puffsPerLobe + 1) + random.Next(puffsPerLobe + 1);
        }

        /// <summary>Sea approach A: puffs on a jittered grid over <paramref name="area"/> (metres, centred on the origin),
        /// mostly sunk so a low dome of each shows at the field's shape height; none in the field's holes, smaller toward
        /// a rim. Shape normals point mostly up, so it shades as a surface.</summary>
        public static Mesh SeaPuffs(string name, CloudSeaField field, Vector2 area, int seed, int subdivisions = 2)
        {
            var random = new System.Random(seed);
            float cell = field.Cell;
            var list = new List<(Vector3 Centre, float Radius)>();
            for (float x = -area.x * 0.5f; x < area.x * 0.5f; x += cell)
            for (float z = -area.y * 0.5f; z < area.y * 0.5f; z += cell)
            {
                float jx = (Next(random) - 0.5f) * cell, jz = (Next(random) - 0.5f) * cell, size = Next(random);
                float open = field.Openness(x + jx, z + jz);
                if (open < 0.5f) continue;
                float radius = cell * Mathf.Lerp(0.55f, 1.1f, size) * Mathf.Lerp(0.6f, 1f, open);
                list.Add((new Vector3(x + jx, field.ShapeHeight(x + jx, z + jz) - radius * 0.85f, z + jz), radius));
            }

            return Build(name, list, subdivisions, field.Floor, field.Top,
                (_, puffNormal, _) => new Vector3(puffNormal.x * 0.35f, Mathf.Max(puffNormal.y, 0f) + 0.7f, puffNormal.z * 0.35f).normalized);
        }

        /// <summary>Sea approaches B and C: the field's surface as one grid mesh, a vertex every <paramref name="step"/>
        /// metres. NORMAL is the surface's own slope (lumps and all), TEXCOORD1 the slope without lumps; quads wholly
        /// below the floor are left out, so holes are open.</summary>
        public static Mesh SeaSurface(string name, CloudSeaField field, Vector2 area, float step)
        {
            int columns = Mathf.CeilToInt(area.x / step) + 1, rows = Mathf.CeilToInt(area.y / step) + 1;
            var positions = new List<Vector3>(columns * rows);
            var normals = new List<Vector3>(positions.Capacity);
            var heights = new List<Vector2>(positions.Capacity);
            var shapes = new List<Vector3>(positions.Capacity);
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                float x = -area.x * 0.5f + column * step, z = -area.y * 0.5f + row * step;
                float y = field.Height(x, z);
                positions.Add(new Vector3(x, y, z));
                normals.Add(field.SurfaceNormal(x, z, step * 0.5f));
                shapes.Add(field.ShapeNormal(x, z, step * 2f));
                heights.Add(new Vector2(Mathf.InverseLerp(field.Floor, field.Top, y), 0f));
            }

            // Creases: how far a point sits below the average of its neighbours two steps away, where domes meet.
            for (int row = 2; row < rows - 2; row++)
            for (int column = 2; column < columns - 2; column++)
            {
                int i = row * columns + column;
                float around = (positions[i - 2].y + positions[i + 2].y + positions[i - 2 * columns].y + positions[i + 2 * columns].y) * 0.25f;
                heights[i] = new Vector2(heights[i].x, Mathf.Clamp01((around - positions[i].y) / (field.Cell * 0.06f)));
            }

            var triangles = new List<int>();
            for (int row = 0; row < rows - 1; row++)
            for (int column = 0; column < columns - 1; column++)
            {
                int a = row * columns + column, b = a + 1, c = a + columns, d = c + 1;
                if (positions[a].y < field.Floor && positions[b].y < field.Floor && positions[c].y < field.Floor && positions[d].y < field.Floor) continue;
                // Clockwise seen from above.
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }

            var mesh = new Mesh { name = name, indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, heights);
            mesh.SetUVs(1, shapes);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Sea approach D's extra: heaped puffs along the field's hole rims (where openness is between a quarter
        /// and three quarters), sitting on its surface, for cauliflower edges where the sea breaks.</summary>
        public static Mesh RimPuffs(string name, CloudSeaField field, Vector2 area, int seed, int subdivisions = 2)
        {
            var random = new System.Random(seed);
            float spacing = field.Cell * 0.6f;
            var list = new List<(Vector3 Centre, float Radius)>();
            for (float x = -area.x * 0.5f; x < area.x * 0.5f; x += spacing)
            for (float z = -area.y * 0.5f; z < area.y * 0.5f; z += spacing)
            {
                float jx = (Next(random) - 0.5f) * spacing, jz = (Next(random) - 0.5f) * spacing, size = Next(random);
                float open = field.Openness(x + jx, z + jz);
                if (open < 0.25f || open > 0.75f) continue;
                float radius = field.Cell * Mathf.Lerp(0.35f, 0.7f, size);
                list.Add((new Vector3(x + jx, field.Height(x + jx, z + jz) - radius * 0.35f, z + jz), radius));
            }

            return Build(name, list, subdivisions, field.Floor, field.Top + field.Cell * 0.5f,
                (_, puffNormal, _) => (puffNormal + Vector3.up * 0.6f).normalized);
        }

        private delegate Vector3 ShapeNormal(Vector3 position, Vector3 puffNormal, int puff);

        /// <summary>Merges puffs into one mesh. With <paramref name="groups"/> (one per puff), creases form only between
        /// puffs of different groups (lobes); without, between any two that overlap.</summary>
        private static Mesh Build(string name, List<(Vector3 Centre, float Radius)> puffs, int subdivisions, float baseY, float topY, ShapeNormal shapeNormal,
            List<int>? groups = null)
        {
            var (sphereVertices, sphereTriangles) = Icosphere(subdivisions);
            var positions = new List<Vector3>(puffs.Count * sphereVertices.Count);
            var normals = new List<Vector3>(positions.Capacity);
            var heights = new List<Vector2>(positions.Capacity);
            var shapes = new List<Vector3>(positions.Capacity);
            var triangles = new List<int>(puffs.Count * sphereTriangles.Count);
            var overlaps = Overlaps(puffs, groups);
            for (int puff = 0; puff < puffs.Count; puff++)
            {
                var (centre, radius) = puffs[puff];
                int first = positions.Count;
                foreach (var unit in sphereVertices)
                {
                    var position = centre + unit * radius;
                    var normal = unit;
                    if (position.y < baseY)
                    {
                        // Flattened onto the base: it faces down, like the cloud's underside.
                        position.y = baseY;
                        normal = Vector3.down;
                    }

                    positions.Add(position);
                    normals.Add(normal);
                    heights.Add(new Vector2(Mathf.InverseLerp(baseY, topY, position.y), Crease(position, radius, puffs, overlaps[puff])));
                    shapes.Add(position.y <= baseY ? Vector3.down : shapeNormal(position, normal, puff));
                }

                foreach (int index in sphereTriangles) triangles.Add(first + index);
            }

            var mesh = new Mesh { name = name, indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, heights);
            mesh.SetUVs(1, shapes);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>For each puff, the others it overlaps (in another group, when there are groups): only those can make
        /// a crease on it.</summary>
        private static List<int>[] Overlaps(List<(Vector3 Centre, float Radius)> puffs, List<int>? groups)
        {
            var overlaps = new List<int>[puffs.Count];
            for (int i = 0; i < puffs.Count; i++)
            {
                overlaps[i] = new List<int>();
                for (int j = 0; j < puffs.Count; j++)
                {
                    if (i != j && (groups == null || groups[i] != groups[j]) && (puffs[i].Centre - puffs[j].Centre).sqrMagnitude < (puffs[i].Radius + puffs[j].Radius) * (puffs[i].Radius + puffs[j].Radius))
                        overlaps[i].Add(j);
                }
            }

            return overlaps;
        }

        /// <summary>1 where a point on one puff meets another puff, falling to 0 most of the smaller radius away: a broad,
        /// soft fold rather than a ring at the seam.</summary>
        private static float Crease(Vector3 position, float ownRadius, List<(Vector3 Centre, float Radius)> puffs, List<int> overlaps)
        {
            float crease = 0f;
            foreach (int other in overlaps)
            {
                var (centre, radius) = puffs[other];
                float gap = Vector3.Distance(position, centre) - radius;
                float near = 1f - Mathf.Clamp01(gap / (Mathf.Min(radius, ownRadius) * CreaseReach));
                crease = Mathf.Max(crease, near * near * (3f - 2f * near));
            }

            return crease;
        }

        private static Vector3 Ellipsoid(Vector3 position, Vector3 centre, Vector3 radii)
        {
            var local = position - centre;
            return new Vector3(local.x / (radii.x * radii.x), local.y / (radii.y * radii.y), local.z / (radii.z * radii.z)).normalized;
        }

        /// <summary>A unit icosphere: 12 vertices at 0 subdivisions, about four times as many per step.</summary>
        private static (List<Vector3> Vertices, List<int> Triangles) Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vertices = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int step = 0; step < subdivisions; step++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }

                triangles = next;

                int Midpoint(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (midpoints.TryGetValue(key, out int index)) return index;
                    vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                    midpoints[key] = vertices.Count - 1;
                    return vertices.Count - 1;
                }
            }

            // Unity draws a triangle's front where its vertices run clockwise, (b - a) x (c - a) pointing out.
            for (int i = 0; i < triangles.Count; i += 3)
            {
                var a = vertices[triangles[i]];
                if (Vector3.Dot(Vector3.Cross(vertices[triangles[i + 1]] - a, vertices[triangles[i + 2]] - a), a) < 0f)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }

            return (vertices, triangles);
        }

        private static float Next(System.Random random) => (float)random.NextDouble();
    }
}
