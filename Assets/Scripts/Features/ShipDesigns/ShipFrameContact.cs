#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: where a part's cell meets the frame. A cell rests on a plank face when its underside (centre or a corner,
    /// seen from above, over the face) is within <see cref="ShipFrameTuning.RestTolerance"/> of the floor there; it touches
    /// an edge when the beam passes through the cell (grown by half a beam). Each contact is reported by one of the
    /// element's nodes, which is what joining needs.
    /// </summary>
    public static class ShipFrameContact
    {
        private const float CornerInset = 0.05f;

        public static IEnumerable<int> TouchedNodes(ShipFrameShape shape, ShipGridCell cell, ShipFrameTuning tuning)
        {
            var centre = ShipGrid.CellCentre(cell);
            float half = ShipGrid.CellSize * 0.5f;
            float underside = centre.y - half;

            foreach (var face in shape.PlankFloors)
            {
                if (RestsOn(face, centre, half - CornerInset, underside, tuning.RestTolerance)) yield return face.Face.Nodes[0];
            }

            var box = new Bounds(centre, Vector3.one * (ShipGrid.CellSize + tuning.BeamWidth));
            foreach (var edge in shape.Edges)
            {
                if (ShipFrameGeometry.SegmentHitsBox(edge.From, edge.To, box)) yield return edge.Edge.A;
            }
        }

        private static bool RestsOn(ShipFrameShape.FaceShape face, Vector3 centre, float reach, float underside, float tolerance)
        {
            for (int sample = 0; sample < 5; sample++)
            {
                float x = centre.x + (sample switch { 1 or 2 => -reach, 3 or 4 => reach, _ => 0f });
                float z = centre.z + (sample switch { 1 or 3 => -reach, 2 or 4 => reach, _ => 0f });
                foreach (var (a, b, c) in face.EachTriangle())
                {
                    if (ShipFrameGeometry.TryHeightAt(x, z, a, b, c, out float height) && Mathf.Abs(underside - height) <= tolerance) return true;
                }
            }

            return false;
        }
    }
}
