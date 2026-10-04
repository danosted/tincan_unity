#nullable enable
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Instantiates part visuals under a "DesignedHull" child of the ship, so they ride it and their colliders are its deck.
    /// The ship's <see cref="ParentLocalSpaceVolume"/> box is resized to the design, with room above for players.
    /// </summary>
    public sealed class ShipHullBuilder : IShipHullBuilder
    {
        public const string HullName = "DesignedHull";

        /// <summary>Room around the design inside which a player counts as on board: sides, below, above.</summary>
        private static readonly Vector3 SideMargin = new(2f, 0f, 2f);
        private const float BelowMargin = 2f;
        private const float AboveMargin = 8f;

        public object Build(Transform ship, GameObject visual, Vector3 localPosition, Quaternion localRotation)
        {
            var instance = Object.Instantiate(visual, Hull(ship), false);
            instance.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return instance;
        }

        public void Remove(object handle)
        {
            if (handle is GameObject instance && instance != null) Object.Destroy(instance);
        }

        public void FitShipVolume(Transform ship, Bounds localBounds)
        {
            var volume = ship.GetComponent<ParentLocalSpaceVolume>();
            var box = volume != null ? volume.GetComponent<BoxCollider>() : null;
            if (box == null) return;

            var min = localBounds.min - SideMargin - Vector3.up * BelowMargin;
            var max = localBounds.max + SideMargin + Vector3.up * AboveMargin;
            box.center = (min + max) * 0.5f;
            box.size = max - min;
        }

        private static Transform Hull(Transform ship)
        {
            var hull = ship.Find(HullName);
            if (hull != null) return hull;

            hull = new GameObject(HullName).transform;
            hull.SetParent(ship, false);
            return hull;
        }
    }
}
