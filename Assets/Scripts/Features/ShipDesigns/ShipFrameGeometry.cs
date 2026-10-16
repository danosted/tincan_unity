#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure geometry for frame faces, seen from above (x, z) with heights carried along: area, simplicity, ear-clipping
    /// triangulation, slope, and point and segment tests. Floor faces need not be flat, so a face is triangulated in its
    /// top-down outline and each triangle takes its corners' heights. The same loop always gives the same triangles.
    /// </summary>
    public static class ShipFrameGeometry
    {
        private const float Epsilon = 1e-6f;

        /// <summary>Twice the signed top-down area (shoelace over x, z). Negative means every triangle of it faces up.</summary>
        public static float SignedArea2(IReadOnlyList<Vector3> loop)
        {
            float sum = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                sum += a.x * b.z - b.x * a.z;
            }

            return sum;
        }

        /// <summary>The top-down area of a loop, in square metres.</summary>
        public static float AreaXZ(IReadOnlyList<Vector3> loop) => Mathf.Abs(SignedArea2(loop)) * 0.5f;

        /// <summary>No two corners at the same top-down point and no two sides crossing or touching, except neighbours at their shared corner.</summary>
        public static bool IsSimpleXZ(IReadOnlyList<Vector3> loop)
        {
            int n = loop.Count;
            if (n < 3) return false;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (SameXZ(loop[i], loop[j])) return false;
                }
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    bool neighbours = j == i + 1 || (i == 0 && j == n - 1);
                    if (neighbours) continue;
                    if (SegmentsTouchXZ(loop[i], loop[(i + 1) % n], loop[j], loop[(j + 1) % n])) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ear clipping in the top-down outline: triangles as index triples into the loop, each wound to face up (Unity's
        /// clockwise front face seen from above). A loop that is not simple may come out short.
        /// </summary>
        public static List<int> Triangulate(IReadOnlyList<Vector3> loop)
        {
            var triangles = new List<int>();
            var remaining = new List<int>(loop.Count);
            for (int i = 0; i < loop.Count; i++) remaining.Add(i);
            if (SignedArea2(loop) > 0f) remaining.Reverse();

            while (remaining.Count > 3)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int prev = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int cur = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    if (!IsEar(loop, remaining, prev, cur, next)) continue;

                    triangles.Add(prev);
                    triangles.Add(cur);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped) return triangles;
            }

            if (remaining.Count == 3 && Cross2(loop[remaining[0]], loop[remaining[1]], loop[remaining[2]]) < -Epsilon)
            {
                triangles.AddRange(remaining);
            }

            return triangles;
        }

        /// <summary>
        /// Ear clipping in the loop's own plane (for wall faces, which stand up): triangles as index triples into the loop,
        /// all wound the same way. <paramref name="facing"/> is the way their front faces point.
        /// </summary>
        public static List<int> TriangulateInPlane(IReadOnlyList<Vector3> loop, out Vector3 facing)
        {
            var normal = NewellNormal(loop);
            facing = Vector3.zero;
            if (normal.sqrMagnitude < Epsilon) return new List<int>();

            var (u, v) = PlaneAxes(normal);
            var projected = new List<Vector3>(loop.Count);
            foreach (var point in loop) projected.Add(new Vector3(Vector3.Dot(point, u), 0f, Vector3.Dot(point, v)));

            // Triangles that face up in the projection face -normal in space (u x v = normal, and up there is -cross).
            facing = -normal;
            return Triangulate(projected);
        }

        /// <summary>Whether a loop is simple in its own plane (see <see cref="IsSimpleXZ"/>).</summary>
        public static bool IsSimpleInPlane(IReadOnlyList<Vector3> loop)
        {
            var normal = NewellNormal(loop);
            if (normal.sqrMagnitude < Epsilon) return false;

            var (u, v) = PlaneAxes(normal);
            var projected = new List<Vector3>(loop.Count);
            foreach (var point in loop) projected.Add(new Vector3(Vector3.Dot(point, u), 0f, Vector3.Dot(point, v)));
            return IsSimpleXZ(projected);
        }

        /// <summary>The unit normal of a loop by Newell's method (zero for a degenerate loop).</summary>
        public static Vector3 NewellNormal(IReadOnlyList<Vector3> loop)
        {
            var normal = Vector3.zero;
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                normal.x += (a.y - b.y) * (a.z + b.z);
                normal.y += (a.z - b.z) * (a.x + b.x);
                normal.z += (a.x - b.x) * (a.y + b.y);
            }

            return normal.sqrMagnitude < Epsilon ? Vector3.zero : normal.normalized;
        }

        private static (Vector3 U, Vector3 V) PlaneAxes(Vector3 normal)
        {
            var reference = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward;
            var u = Vector3.Cross(reference, normal).normalized;
            return (u, Vector3.Cross(normal, u));
        }

        /// <summary>How far a triangle tilts from level, in degrees (0 flat, 90 a wall).</summary>
        public static float SlopeDegrees(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < Epsilon) return 0f;
            return Vector3.Angle(normal.y >= 0f ? normal : -normal, Vector3.up);
        }

        /// <summary>The area of a triangle in 3D, in square metres.</summary>
        public static float Area(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).magnitude * 0.5f;

        /// <summary>Whether a top-down point lies inside (or on) a triangle, and the triangle's height there.</summary>
        public static bool TryHeightAt(float x, float z, Vector3 a, Vector3 b, Vector3 c, out float height)
        {
            height = 0f;
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < Epsilon) return false;

            float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
            float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
            float w = 1f - u - v;
            const float slack = -1e-4f;
            if (u < slack || v < slack || w < slack) return false;

            height = u * a.y + v * b.y + w * c.y;
            return true;
        }

        /// <summary>Whether a segment passes through (or touches) an axis-aligned box: the slab test.</summary>
        public static bool SegmentHitsBox(Vector3 from, Vector3 to, Bounds box)
        {
            float enter = 0f, exit = 1f;
            var direction = to - from;
            for (int axis = 0; axis < 3; axis++)
            {
                float start = from[axis], step = direction[axis], min = box.min[axis], max = box.max[axis];
                if (Mathf.Abs(step) < Epsilon)
                {
                    if (start < min || start > max) return false;
                    continue;
                }

                float t0 = (min - start) / step, t1 = (max - start) / step;
                if (t0 > t1) (t0, t1) = (t1, t0);
                enter = Mathf.Max(enter, t0);
                exit = Mathf.Min(exit, t1);
                if (enter > exit) return false;
            }

            return true;
        }

        /// <summary>Whether a top-down point lies strictly inside a loop (not on its sides): ray casting along +x.</summary>
        public static bool StrictlyInsideXZ(float x, float z, IReadOnlyList<Vector3> loop)
        {
            bool inside = false;
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                var a = loop[i];
                var b = loop[j];
                if (DistanceToSegmentXZ(x, z, a, b) < 1e-4f) return false;
                if ((a.z > z) == (b.z > z)) continue;
                if (x < (b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }

            return inside;
        }

        /// <summary>
        /// Whether two simple loops overlap seen from above, beyond sharing sides or corners: a side of one properly
        /// crosses a side of the other, or a triangle centre of one lies strictly inside the other.
        /// </summary>
        public static bool OverlapXZ(IReadOnlyList<Vector3> a, IReadOnlyList<int> aTriangles, IReadOnlyList<Vector3> b, IReadOnlyList<int> bTriangles)
        {
            for (int i = 0; i < a.Count; i++)
            {
                for (int j = 0; j < b.Count; j++)
                {
                    if (SegmentsCrossXZ(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
                }
            }

            return AnyCentreInside(a, aTriangles, b) || AnyCentreInside(b, bTriangles, a);
        }

        private static bool AnyCentreInside(IReadOnlyList<Vector3> loop, IReadOnlyList<int> triangles, IReadOnlyList<Vector3> other)
        {
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                var centre = (loop[triangles[i]] + loop[triangles[i + 1]] + loop[triangles[i + 2]]) / 3f;
                if (StrictlyInsideXZ(centre.x, centre.z, other)) return true;
            }

            return false;
        }

        /// <summary>Two sides cross at a point inside both (touching at an end does not count).</summary>
        private static bool SegmentsCrossXZ(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
        {
            float d1 = Cross2(q1, q2, p1), d2 = Cross2(q1, q2, p2), d3 = Cross2(p1, p2, q1), d4 = Cross2(p1, p2, q2);
            return ((d1 > Epsilon && d2 < -Epsilon) || (d1 < -Epsilon && d2 > Epsilon)) &&
                   ((d3 > Epsilon && d4 < -Epsilon) || (d3 < -Epsilon && d4 > Epsilon));
        }

        private static float DistanceToSegmentXZ(float x, float z, Vector3 a, Vector3 b)
        {
            var p = new Vector2(x, z);
            var from = new Vector2(a.x, a.z);
            var along = new Vector2(b.x, b.z) - from;
            float t = along.sqrMagnitude < Epsilon ? 0f : Mathf.Clamp01(Vector2.Dot(p - from, along) / along.sqrMagnitude);
            return Vector2.Distance(p, from + along * t);
        }

        private static bool IsEar(IReadOnlyList<Vector3> loop, List<int> remaining, int prev, int cur, int next)
        {
            var a = loop[prev];
            var b = loop[cur];
            var c = loop[next];
            if (Cross2(a, b, c) >= -Epsilon) return false;

            foreach (int other in remaining)
            {
                if (other == prev || other == cur || other == next) continue;
                if (InsideOrOnXZ(loop[other], a, b, c)) return false;
            }

            return true;
        }

        /// <summary>The top-down cross product of (b - a) and (c - a): negative when a, b, c turn the way an up-facing triangle does.</summary>
        private static float Cross2(Vector3 a, Vector3 b, Vector3 c) => (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);

        private static bool InsideOrOnXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = Cross2(a, b, p), d2 = Cross2(b, c, p), d3 = Cross2(c, a, p);
            bool hasNegative = d1 < -Epsilon || d2 < -Epsilon || d3 < -Epsilon;
            bool hasPositive = d1 > Epsilon || d2 > Epsilon || d3 > Epsilon;
            return !(hasNegative && hasPositive);
        }

        private static bool SameXZ(Vector3 a, Vector3 b) => Mathf.Abs(a.x - b.x) < Epsilon && Mathf.Abs(a.z - b.z) < Epsilon;

        private static bool SegmentsTouchXZ(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
        {
            float d1 = Cross2(q1, q2, p1), d2 = Cross2(q1, q2, p2), d3 = Cross2(p1, p2, q1), d4 = Cross2(p1, p2, q2);
            if (((d1 > Epsilon && d2 < -Epsilon) || (d1 < -Epsilon && d2 > Epsilon)) &&
                ((d3 > Epsilon && d4 < -Epsilon) || (d3 < -Epsilon && d4 > Epsilon))) return true;

            return (Mathf.Abs(d1) <= Epsilon && OnSegmentXZ(q1, q2, p1)) || (Mathf.Abs(d2) <= Epsilon && OnSegmentXZ(q1, q2, p2)) ||
                   (Mathf.Abs(d3) <= Epsilon && OnSegmentXZ(p1, p2, q1)) || (Mathf.Abs(d4) <= Epsilon && OnSegmentXZ(p1, p2, q2));
        }

        private static bool OnSegmentXZ(Vector3 a, Vector3 b, Vector3 p) =>
            p.x >= Mathf.Min(a.x, b.x) - Epsilon && p.x <= Mathf.Max(a.x, b.x) + Epsilon &&
            p.z >= Mathf.Min(a.z, b.z) - Epsilon && p.z <= Mathf.Max(a.z, b.z) + Epsilon;
    }
}
