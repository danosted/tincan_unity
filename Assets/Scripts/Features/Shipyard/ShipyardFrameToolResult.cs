#nullable enable
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// What a frame tool gesture did: the new design and the edit that undoes all of it (or null when nothing changed),
    /// the node selected afterwards, and a line for the player.
    /// </summary>
    public sealed class ShipyardFrameToolResult
    {
        public ShipyardFrameToolResult(ShipDesign? design, ShipDesignEdit? undo, int? selected, string message)
        {
            Design = design;
            Undo = undo;
            Selected = selected;
            Message = message;
        }

        public ShipDesign? Design { get; }
        public ShipDesignEdit? Undo { get; }
        public int? Selected { get; }
        public string Message { get; }
        public bool Changed => Design != null;

        public override string ToString() => $"{(Changed ? "changed" : "unchanged")}, selected {Selected?.ToString() ?? "none"}: {Message}";
    }
}
