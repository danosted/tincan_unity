#nullable enable
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The frame-mode overlay: which node is selected, what the cursor is on (drawn highlighted, or in the blocked colour
    /// in delete mode), and the line a click would draw from the selected node, if any.
    /// </summary>
    public readonly struct ShipyardFrameMarks
    {
        public ShipyardFrameMarks(int? selected, ShipyardFramePick hover, bool deleting, Vector3? pendingTo)
        {
            Selected = selected;
            Hover = hover;
            Deleting = deleting;
            PendingTo = pendingTo;
        }

        public int? Selected { get; }
        public ShipyardFramePick Hover { get; }
        public bool Deleting { get; }

        /// <summary>Where a new edge from the selected node would end (the cursor on the floor plane), or null for none.</summary>
        public Vector3? PendingTo { get; }
    }
}
