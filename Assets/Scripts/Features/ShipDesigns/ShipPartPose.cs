#nullable enable
using System.Linq;
using TinCan.Core.Ship.Parts;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Where a placed part stands, from its anchor and the frame: a face part on the plank floor at its point (at the
    /// floor's height there), a node part at its node; its prefab's pivot offset turned with its yaw. A part whose anchor
    /// is missing, of the wrong kind, or off its face has no pose (the validator says why) and is not built.
    /// </summary>
    public static class ShipPartPose
    {
        /// <summary>The size of a footprint block, in metres.</summary>
        public const float BlockSize = 1f;

        public static Quaternion LocalRotation(ShipPartPlacement placement) => ShipPartOrientation.ToRotation(placement.Orientation);

        /// <summary>The point the part stands on: on its face's floor, or at its node.</summary>
        public static bool TryGetAnchorPoint(ShipPartAnchor anchor, ShipFrameShape shape, out Vector3 point)
        {
            point = default;
            if (anchor.Kind == ShipPartAttach.Node)
            {
                if (!shape.Frame.TryGetNode(anchor.Element, out var node)) return false;
                point = shape.Frame.Position(node);
                return true;
            }

            float x = ShipFrameUnits.ToMetres(anchor.X), z = ShipFrameUnits.ToMetres(anchor.Z);
            foreach (var face in shape.Faces)
            {
                if (face.Face.Id != anchor.Element || face.IsWall || face.Face.Fill != ShipFrameFill.Planks) continue;
                foreach (var (a, b, c) in face.EachTriangle())
                {
                    if (!ShipFrameGeometry.TryHeightAt(x, z, a, b, c, out float height)) continue;
                    point = new Vector3(x, height, z);
                    return true;
                }
            }

            return false;
        }

        /// <summary>The part's prefab pose in ship space, or false when its anchor does not hold it.</summary>
        public static bool TryGetPose(ShipPartPlacement placement, ShipPartDefinition part, ShipFrameShape shape, out Vector3 position, out Quaternion rotation)
        {
            rotation = LocalRotation(placement);
            position = default;
            if (part.Attach != placement.Anchor.Kind || !TryGetAnchorPoint(placement.Anchor, shape, out var point)) return false;

            position = point + rotation * part.PivotOffset;
            return true;
        }

        /// <summary>The centre of each 1 m block the part fills, in ship space (its origin block stands on its anchor point).</summary>
        public static Vector3[] Blocks(ShipPartPlacement placement, ShipPartDefinition part, Vector3 anchorPoint) =>
            part.Footprint
                .Select(offset => anchorPoint + (Vector3)ShipPartOrientation.Rotate(offset, placement.Orientation) * BlockSize + Vector3.up * (BlockSize * 0.5f))
                .ToArray();

        /// <summary>The ship-local box around every known, anchored part's blocks, or false for a design with none.</summary>
        public static bool TryGetBounds(ShipDesign design, IShipPartCatalog catalog, ShipFrameShape shape, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var placement in design.Parts)
            {
                if (!catalog.TryGet(placement.PartId, out var part) || !TryGetAnchorPoint(placement.Anchor, shape, out var point)) continue;
                foreach (var centre in Blocks(placement, part, point))
                {
                    var block = new Bounds(centre, Vector3.one * BlockSize);
                    if (!any) bounds = block;
                    else bounds.Encapsulate(block);
                    any = true;
                }
            }

            return any;
        }
    }
}
