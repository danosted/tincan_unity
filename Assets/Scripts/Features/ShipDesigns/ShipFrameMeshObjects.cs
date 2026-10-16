#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Turns <see cref="ShipFrameMesh"/> data into a scene object (mesh, renderer and a mesh collider of the same shape)
    /// and destroys built objects again, with the mesh when it was generated here. Ships are kinematic, so the non-convex
    /// collider is allowed; players stand on it.
    /// </summary>
    public static class ShipFrameMeshObjects
    {
        /// <summary>Generated meshes are named with this prefix, so destroying can tell them from prefab meshes.</summary>
        private const string MeshPrefix = "ShipFrame:";

        public static GameObject Create(Transform parent, string name, ShipFrameMesh data, Material? material, bool collider)
        {
            var mesh = new Mesh
            {
                name = MeshPrefix + name,
                indexFormat = data.Vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(data.Vertices);
            mesh.SetUVs(0, data.Uvs);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = instance.AddComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            if (collider) instance.AddComponent<MeshCollider>().sharedMesh = mesh;
            return instance;
        }

        /// <summary>Destroys a built object: a part copy, or a generated mesh object together with its mesh.</summary>
        public static void Destroy(GameObject instance)
        {
            if (instance == null) return;
            var filter = instance.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name.StartsWith(MeshPrefix, System.StringComparison.Ordinal))
            {
                Object.Destroy(filter.sharedMesh);
            }

            Object.Destroy(instance);
        }
    }
}
