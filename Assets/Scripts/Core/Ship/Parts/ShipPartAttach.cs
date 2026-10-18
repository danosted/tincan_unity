#nullable enable
namespace TinCan.Core.Ship.Parts
{
    /// <summary>
    /// The kind of frame element a part hangs on. Edges carry railings and stairs as part of the frame itself, so no part
    /// attaches to one. Plan: .docs/plans/frame-only-ships.md.
    /// </summary>
    public enum ShipPartAttach : byte
    {
        /// <summary>Stands on a plank floor at a point of it.</summary>
        Face,

        /// <summary>Sits on a frame node and follows it.</summary>
        Node,
    }
}
