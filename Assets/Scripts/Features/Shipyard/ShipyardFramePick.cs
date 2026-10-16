#nullable enable
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// What the cursor is on in frame mode: the nearest node, else edge, else face (with the point on it), and where the
    /// cursor's ray meets the working story's floor plane, if it does. Positions are in the preview's (ship) space.
    /// </summary>
    public readonly struct ShipyardFramePick
    {
        public ShipyardFramePick(ShipyardFramePickKind kind, int id, Vector3 point, bool onPlane, Vector3 planePoint)
        {
            Kind = kind;
            Id = id;
            Point = point;
            OnPlane = onPlane;
            PlanePoint = planePoint;
        }

        public ShipyardFramePickKind Kind { get; }

        /// <summary>The node, edge or face id (0 for none).</summary>
        public int Id { get; }

        /// <summary>Where on the picked element the ray passes (the node itself, the nearest point of the edge, the face hit).</summary>
        public Vector3 Point { get; }

        public bool OnPlane { get; }
        public Vector3 PlanePoint { get; }

        public bool Is(ShipyardFramePickKind kind) => Kind == kind;

        public override string ToString() =>
            $"{(Kind == ShipyardFramePickKind.None ? "nothing" : $"{Kind} #{Id}")}{(OnPlane ? $", plane at {PlanePoint}" : string.Empty)}";
    }
}
