#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Plain mesh data from <see cref="ShipFrameMeshProcessor"/>: ship-local vertices, triangles (Unity's clockwise front
    /// faces) and UVs in texture repeats. Every quad has its own vertices, so normals come out flat per side.
    /// </summary>
    public sealed class ShipFrameMesh
    {
        public List<Vector3> Vertices { get; } = new();
        public List<int> Triangles { get; } = new();
        public List<Vector2> Uvs { get; } = new();

        public bool IsEmpty => Triangles.Count == 0;

        public int AddVertex(Vector3 position, Vector2 uv)
        {
            Vertices.Add(position);
            Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>A quad a-b-c-d (in order round its edge), wound so its front faces <paramref name="outward"/>.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int ia = AddVertex(a, uvA), ib = AddVertex(b, uvB), ic = AddVertex(c, uvC), id = AddVertex(d, uvD);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f)
            {
                AddTriangle(ia, ib, ic);
                AddTriangle(ia, ic, id);
                return;
            }

            AddTriangle(ia, ic, ib);
            AddTriangle(ia, id, ic);
        }
    }
}
