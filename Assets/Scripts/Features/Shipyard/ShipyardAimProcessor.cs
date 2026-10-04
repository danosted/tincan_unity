#nullable enable
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: turns the cursor's ray into grid cells. New parts go on the working level (PgUp/PgDn): the cell where the ray
    /// meets that level's floor. Remove takes the part the ray hits, on any level. A ray that never meets the level's
    /// floor (looking away from it) places nowhere.
    /// </summary>
    public sealed class ShipyardAimProcessor
    {
        /// <summary>The height of a level's floor in the preview: the bottom face of its cells.</summary>
        public static float FloorY(int level) => (level - 0.5f) * ShipGrid.CellSize;

        public ShipyardAim Aim(ShipyardRay ray, int level)
        {
            ShipGridCell? partCell = ray.HasHit ? CellAt(ray.HitPoint - ray.HitNormal.normalized * (0.5f * ShipGrid.CellSize)) : null;

            if (Mathf.Abs(ray.Direction.y) < 1e-4f) return ShipyardAim.Nowhere(partCell);
            float distance = (FloorY(level) - ray.Origin.y) / ray.Direction.y;
            if (distance < 0f) return ShipyardAim.Nowhere(partCell);

            var onFloor = ray.Origin + ray.Direction * distance;
            var cell = CellAt(onFloor);
            return new ShipyardAim(new ShipGridCell(cell.X, level, cell.Z), partCell);
        }

        public static ShipGridCell CellAt(Vector3 local) =>
            new(Mathf.RoundToInt(local.x / ShipGrid.CellSize), Mathf.RoundToInt(local.y / ShipGrid.CellSize), Mathf.RoundToInt(local.z / ShipGrid.CellSize));
    }
}
