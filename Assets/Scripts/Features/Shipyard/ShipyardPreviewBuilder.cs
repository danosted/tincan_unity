#nullable enable
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>Builds the shipyard's preview from stripped copies (<see cref="ShipyardPrefabs"/>), colliders kept for aiming.</summary>
    public sealed class ShipyardPreviewBuilder : IShipHullBuilder
    {
        public object Build(Transform ship, GameObject visual, Vector3 localPosition, Quaternion localRotation)
        {
            var copy = ShipyardPrefabs.InstantiateStripped(visual, ship, keepColliders: true);
            copy.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return copy;
        }

        public void Remove(object handle)
        {
            if (handle is GameObject copy && copy != null) Object.Destroy(copy);
        }

        public void FitShipVolume(Transform ship, Bounds localBounds)
        {
        }
    }
}
