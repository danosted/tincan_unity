#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="SkyIslandMeshProcessor"/>: a closed, outward-facing island within its size.</summary>
    public class SkyIslandMeshProcessorTests
    {
        private static readonly SkyIslandMeshResolution Visual = new(48, 6, 14);
        private static readonly SkyIslandMeshResolution Coarse = new(10, 1, 3);

        private readonly SkyIslandMeshProcessor _meshes = new();

        [Test]
        public void SizesFollowTheResolution()
        {
            var mesh = Build(Island(1), Visual);

            Assert.That(mesh.Vertices.Count, Is.EqualTo(Visual.VertexCount));
            Assert.That(mesh.Normals.Count, Is.EqualTo(mesh.Vertices.Count));
            Assert.That(mesh.Colors.Count, Is.EqualTo(mesh.Vertices.Count));
            int rings = Visual.TopRings + 1 + Visual.UnderRings;
            Assert.That(mesh.Triangles.Count, Is.EqualTo(3 * (2 * Visual.Segments + 2 * Visual.Segments * (rings - 1))));
        }

        [TestCase(1u)]
        [TestCase(77u)]
        [TestCase(123456u)]
        public void TheMeshIsClosed_AndWoundOneWay(uint shapeSeed)
        {
            foreach (var resolution in new[] { Visual, Coarse })
            {
                var mesh = Build(Island(shapeSeed), resolution);
                var edges = new HashSet<(int, int)>();
                var triangles = mesh.Triangles;
                for (int t = 0; t < triangles.Count; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        var edge = (triangles[t + e], triangles[t + (e + 1) % 3]);
                        Assert.That(edges.Add(edge), Is.True, $"edge {edge} used twice the same way");
                    }
                }
                foreach (var (a, b) in edges) Assert.That(edges.Contains((b, a)), Is.True, $"edge {a}-{b} is open");
            }
        }

        [Test]
        public void FacesPointOut()
        {
            var mesh = Build(Island(5), Visual);

            Assert.That(SignedVolume(mesh), Is.GreaterThan(0f), "outward winding encloses a positive volume");
            Assert.That(mesh.Normals[0].y, Is.GreaterThan(0.9f), "the top's centre faces up");
            Assert.That(mesh.Normals[^1].y, Is.LessThan(-0.5f), "the apex faces down");
        }

        [Test]
        public void TheIsland_StaysWithinItsRadiusAndDepth()
        {
            var island = Island(9, radius: 60f, depth: 80f);
            var mesh = Build(island, Visual);

            foreach (var vertex in mesh.Vertices)
                Assert.That(new Vector2(vertex.x, vertex.z).magnitude, Is.LessThanOrEqualTo(island.Radius + 1e-3f));
            Assert.That(mesh.Vertices.Min(vertex => vertex.y), Is.EqualTo(-island.Depth).Within(1e-3f), "the apex");
            Assert.That(mesh.Vertices.Max(vertex => vertex.y), Is.InRange(0f, island.Radius * 0.2f), "rolling, not a mountain");
        }

        [Test]
        public void EverySeed_GivesFiniteVertices_AndNoCollapsedTriangles()
        {
            for (uint seed = 0; seed < 300; seed++)
            {
                foreach (var resolution in new[] { Visual, Coarse, new SkyIslandMeshResolution(16, 2, 5) })
                {
                    var mesh = Build(Island(seed, radius: 5f + seed % 90, depth: 8f + seed % 120), resolution);
                    foreach (var vertex in mesh.Vertices)
                        Assert.That(float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z), Is.True, $"seed {seed}: {vertex}");
                    for (int t = 0; t < mesh.Triangles.Count; t += 3)
                    {
                        var a = mesh.Vertices[mesh.Triangles[t]];
                        var area = Vector3.Cross(mesh.Vertices[mesh.Triangles[t + 1]] - a, mesh.Vertices[mesh.Triangles[t + 2]] - a).magnitude;
                        Assert.That(area, Is.GreaterThan(1e-4f), $"seed {seed}: triangle {t / 3} collapsed");
                    }
                }
            }
        }

        [Test]
        public void TheSameIsland_GivesTheSameMesh()
        {
            var first = Build(Island(31), Visual).Vertices.ToArray();
            var second = Build(Island(31), Visual).Vertices.ToArray();
            var other = Build(Island(32), Visual).Vertices.ToArray();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        }

        [Test]
        public void GrassOnTop_RockBelow()
        {
            var mesh = Build(Island(3), Visual);

            Assert.That(mesh.Colors[0].r, Is.EqualTo(255), "top: grass");
            Assert.That(mesh.Colors[^1].r, Is.EqualTo(0), "apex: rock");
            Assert.That(mesh.Colors[^1].a, Is.EqualTo(255), "apex: all the way down");
        }

        private SkyIslandMeshData Build(SkyIslandSpec island, SkyIslandMeshResolution resolution)
        {
            var mesh = new SkyIslandMeshData();
            _meshes.Build(island, resolution, mesh);
            return mesh;
        }

        private static SkyIslandSpec Island(uint shapeSeed, float radius = 50f, float depth = 70f) =>
            new(new SkyIslandId(1, 0, 0, 0), Vector3.zero, radius, depth, shapeSeed);

        private static float SignedVolume(SkyIslandMeshData mesh)
        {
            float volume = 0f;
            var v = mesh.Vertices;
            for (int t = 0; t < mesh.Triangles.Count; t += 3)
            {
                var a = v[mesh.Triangles[t]];
                var b = v[mesh.Triangles[t + 1]];
                var c = v[mesh.Triangles[t + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
            }
            return volume;
        }
    }
}
