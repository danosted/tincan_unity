#nullable enable
using NUnit.Framework;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Features.SkyIslands.Tests
{
    /// <summary><see cref="SkyIslandCaveMeshProcessor"/>: a dome of rock with an open mouth, the visual and the collision in step.</summary>
    public class SkyIslandCaveMeshProcessorTests
    {
        private static readonly SkyIslandCave Cave = new(new Vector3(4f, 1f, -3f), 2f, 10f, 9f, 3f, 0.3f, 5u);

        private readonly SkyIslandCaveMeshProcessor _caves = new();

        [Test]
        public void TheCollision_IsOnePiecePerCell_LessTheMouth()
        {
            var pieces = new SkyIslandCollisionPieces();
            _caves.AppendPieces(Cave, pieces);

            int cells = (SkyIslandCaveMeshProcessor.Rings + 1) * SkyIslandCaveMeshProcessor.Columns;
            Assert.That(pieces.Count, Is.EqualTo(cells - 3), "the mouth: one column, the skirt and two rows up to 45 degrees");
            for (int piece = 0; piece < pieces.Count; piece++) Assert.That(pieces.Length(piece), Is.EqualTo(8).Or.EqualTo(6));
        }

        [Test]
        public void TheMouth_IsOpen_AndTheWallElsewhereIsNot()
        {
            var mesh = Visual();
            var chamber = Cave.Chamber + Vector3.up * 2f;
            var mouth = Cave.MouthDirection;

            Assert.That(Crossings(mesh, chamber + mouth * (Cave.OuterRadius + 6f), chamber), Is.Zero, "in through the mouth");
            // Off the grid's lines, so a segment never runs along an edge between two faces.
            var back = Quaternion.Euler(0f, 187f, 0f) * mouth;
            Assert.That(Crossings(mesh, chamber + back * (Cave.OuterRadius + 6f), chamber), Is.EqualTo(2), "through the back wall: out and in");
            var aside = chamber + new Vector3(1.3f, 0f, 0.7f);
            Assert.That(Crossings(mesh, aside + Vector3.up * 30f, aside), Is.EqualTo(2), "through the roof");
        }

        [Test]
        public void TheOutside_FacesOut_AndTheChamber_FacesIn()
        {
            var mesh = Visual();
            int columns = SkyIslandCaveMeshProcessor.Columns * SkyIslandCaveMeshProcessor.VisualFactor;
            int shell = (SkyIslandCaveMeshProcessor.Rings * SkyIslandCaveMeshProcessor.VisualFactor + 1) * columns + 1;

            for (int i = 0; i < 2 * shell; i++)
            {
                var normal = mesh.Normals[i];
                if (normal.sqrMagnitude < 0.5f) continue; // inside the mouth: no face uses it
                float outward = Vector3.Dot(normal, mesh.Vertices[i] - Cave.Base);
                Assert.That(i < shell ? outward : -outward, Is.GreaterThan(0f), $"vertex {i}");
            }
        }

        [Test]
        public void TheVisual_IsValid_AndMeetsTheCollision()
        {
            var mesh = Visual();
            Assert.That(mesh.Normals.Count, Is.EqualTo(mesh.Vertices.Count));
            Assert.That(mesh.Colors.Count, Is.EqualTo(mesh.Vertices.Count));
            for (int t = 0; t < mesh.Triangles.Count; t += 3)
            {
                var a = mesh.Vertices[mesh.Triangles[t]];
                float area = Vector3.Cross(mesh.Vertices[mesh.Triangles[t + 1]] - a, mesh.Vertices[mesh.Triangles[t + 2]] - a).magnitude;
                Assert.That(area, Is.GreaterThan(1e-4f), $"triangle {t / 3} collapsed");
            }

            int f = SkyIslandCaveMeshProcessor.VisualFactor;
            for (int ring = 0; ring <= SkyIslandCaveMeshProcessor.Rings; ring++)
            {
                int fine = ring == 0 ? 0 : f * (ring - 1) + 1;
                for (int column = 0; column < SkyIslandCaveMeshProcessor.Columns; column++)
                {
                    var coarse = SkyIslandCaveMeshProcessor.Point(Cave, column, ring, true, 1);
                    var visual = SkyIslandCaveMeshProcessor.Point(Cave, column * f, fine, true, f);
                    Assert.That((coarse - visual).magnitude, Is.LessThan(1e-4f), $"ring {ring} column {column}");
                }
            }
        }

        [Test]
        public void TheRock_SitsOnItsFoot_AndSinksItsSkirt()
        {
            var mesh = Visual();
            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (var vertex in mesh.Vertices)
            {
                lowest = Mathf.Min(lowest, vertex.y);
                highest = Mathf.Max(highest, vertex.y);
            }

            Assert.That(lowest, Is.EqualTo(Cave.Base.y - SkyIslandCaveMeshProcessor.Skirt).Within(1e-4f));
            Assert.That(highest - Cave.Base.y, Is.InRange((Cave.Height + Cave.Wall) * 0.8f, (Cave.Height + Cave.Wall) * 1.2f));
        }

        private SkyIslandMeshData Visual()
        {
            var mesh = new SkyIslandMeshData();
            _caves.AppendVisual(Cave, mesh);
            return mesh;
        }

        /// <summary>How many triangles the segment crosses.</summary>
        private static int Crossings(SkyIslandMeshData mesh, Vector3 from, Vector3 to)
        {
            var direction = to - from;
            int count = 0;
            for (int t = 0; t < mesh.Triangles.Count; t += 3)
            {
                if (Crosses(from, direction, mesh.Vertices[mesh.Triangles[t]], mesh.Vertices[mesh.Triangles[t + 1]],
                        mesh.Vertices[mesh.Triangles[t + 2]])) count++;
            }
            return count;
        }

        /// <summary>Moller-Trumbore, both faces, within the segment.</summary>
        private static bool Crosses(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var p = Vector3.Cross(direction, ac);
            float det = Vector3.Dot(ab, p);
            if (Mathf.Abs(det) < 1e-8f) return false;
            float inverse = 1f / det;
            var s = origin - a;
            float u = Vector3.Dot(s, p) * inverse;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(s, ab);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f) return false;
            float t = Vector3.Dot(ac, q) * inverse;
            return t >= 0f && t <= 1f;
        }
    }
}
