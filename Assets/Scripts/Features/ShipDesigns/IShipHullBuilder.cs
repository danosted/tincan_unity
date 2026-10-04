#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Builds a design's structural parts as plain child objects of a ship (no NetworkObjects): each peer builds its own
    /// from the replicated design. A handle is whatever the builder needs to remove the part again.
    /// </summary>
    public interface IShipHullBuilder
    {
        object Build(Transform ship, GameObject visual, Vector3 localPosition, Quaternion localRotation);

        void Remove(object handle);

        /// <summary>Fits the ship's "on board" volume (players inside it ride the ship) around the design.</summary>
        void FitShipVolume(Transform ship, Bounds localBounds);
    }
}
