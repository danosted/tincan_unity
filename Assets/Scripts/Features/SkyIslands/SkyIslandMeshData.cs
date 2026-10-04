#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// An island mesh as plain lists, filled by <see cref="SkyIslandMeshProcessor"/> and reused between builds.
    /// Vertex colour: r grass, g ambient occlusion, b a strata value per ring, a how far down the rock (0 top, 1 apex).
    /// </summary>
    public sealed class SkyIslandMeshData
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Color32> Colors = new();
        public readonly List<int> Triangles = new();

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Triangles.Clear();
        }
    }
}
