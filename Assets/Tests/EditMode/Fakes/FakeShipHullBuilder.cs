#nullable enable
using System.Collections.Generic;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Records what a hull builder was asked to build (parts and frame meshes) and remove, without creating objects.</summary>
    public sealed class FakeShipHullBuilder : IShipHullBuilder
    {
        public sealed class Built
        {
            public Built(GameObject visual, Vector3 position, Quaternion rotation)
            {
                Visual = visual;
                Position = position;
                Rotation = rotation;
            }

            public GameObject Visual { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }

        public sealed class BuiltMesh
        {
            public BuiltMesh(string name, ShipFrameMesh mesh, Material? material, ShipFrameMeshUse use)
            {
                Name = name;
                Mesh = mesh;
                Material = material;
                Use = use;
            }

            public string Name { get; }
            public ShipFrameMesh Mesh { get; }
            public Material? Material { get; }
            public ShipFrameMeshUse Use { get; }
        }

        public List<Built> Standing { get; } = new();
        public List<BuiltMesh> Meshes { get; } = new();
        public int MeshBuilds { get; private set; }
        public int Builds { get; private set; }
        public int Removals { get; private set; }
        public Bounds? Volume { get; private set; }

        public object Build(Transform ship, GameObject visual, Vector3 localPosition, Quaternion localRotation)
        {
            var built = new Built(visual, localPosition, localRotation);
            Standing.Add(built);
            Builds++;
            return built;
        }

        public object BuildMesh(Transform ship, string name, ShipFrameMesh mesh, Material? material, ShipFrameMeshUse use)
        {
            var built = new BuiltMesh(name, mesh, material, use);
            Meshes.Add(built);
            MeshBuilds++;
            return built;
        }

        public void Remove(object handle)
        {
            if (handle is BuiltMesh mesh)
            {
                Meshes.Remove(mesh);
                Removals++;
                return;
            }

            Standing.Remove((Built)handle);
            Removals++;
        }

        public void FitShipVolume(Transform ship, Bounds localBounds) => Volume = localBounds;
    }
}
