#nullable enable
using System.Collections.Generic;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Records what a hull builder was asked to build and remove, without creating objects.</summary>
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

        public List<Built> Standing { get; } = new();
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

        public void Remove(object handle)
        {
            Standing.Remove((Built)handle);
            Removals++;
        }

        public void FitShipVolume(Transform ship, Bounds localBounds) => Volume = localBounds;
    }
}
