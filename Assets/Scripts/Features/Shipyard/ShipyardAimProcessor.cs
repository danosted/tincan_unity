#nullable enable
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: turns the cursor's ray into grid cells. Over a built part, the new part goes in the cell beside the face the
    /// cursor is on, and Remove takes the part behind that face. Over nothing, the ray meets the build floor (the bottom
    /// of the origin's layer) and the new part goes in the cell above it. A ray that never meets the floor aims nowhere.
    /// </summary>
    public sealed class ShipyardAimProcessor
    {
        /// <summary>The floor's height in the preview: the bottom face of cell layer 0.</summary>
        public const float FloorY = -0.5f * ShipGrid.CellSize;

        public ShipyardAim Aim(ShipyardRay ray)
        {
            float half = 0.5f * ShipGrid.CellSize;
            if (ray.HasHit)
            {
                var normal = ray.HitNormal.normalized;
                return new ShipyardAim(CellAt(ray.HitPoint + normal * half), CellAt(ray.HitPoint - normal * half));
            }

            if (ray.Direction.y > -1e-4f) return ShipyardAim.None;

            float distance = (FloorY - ray.Origin.y) / ray.Direction.y;
            if (distance < 0f) return ShipyardAim.None;

            var onFloor = ray.Origin + ray.Direction * distance;
            return new ShipyardAim(CellAt(onFloor + Vector3.up * half), null);
        }

        public static ShipGridCell CellAt(Vector3 local) =>
            new(Mathf.RoundToInt(local.x / ShipGrid.CellSize), Mathf.RoundToInt(local.y / ShipGrid.CellSize), Mathf.RoundToInt(local.z / ShipGrid.CellSize));
    }
}
