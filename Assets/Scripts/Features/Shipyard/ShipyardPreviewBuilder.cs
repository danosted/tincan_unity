#nullable enable
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Builds the shipyard's preview from stripped copies (<see cref="ShipyardPrefabs"/>) and the frame's generated meshes,
    /// colliders kept for aiming.
    /// </summary>
    public sealed class ShipyardPreviewBuilder : IShipHullBuilder
    {
        public object Build(Transform ship, GameObject visual, Vector3 localPosition, Quaternion localRotation)
        {
            var copy = ShipyardPrefabs.InstantiateStripped(visual, ship, keepColliders: true);
            copy.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return copy;
        }

        public object BuildMesh(Transform ship, string name, ShipFrameMesh mesh, Material? material) =>
            ShipFrameMeshObjects.Create(ship, name, mesh, material, collider: true);

        public void Remove(object handle)
        {
            if (handle is GameObject copy && copy != null) ShipFrameMeshObjects.Destroy(copy);
        }

        public void FitShipVolume(Transform ship, Bounds localBounds)
        {
        }
    }
}
