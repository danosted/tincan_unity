#nullable enable
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// How the frame tools build: the story they work on, the grid nodes snap to (frame units), the first story's floor and
    /// height when a design has no frame yet, and the design limits.
    /// </summary>
    public readonly struct ShipyardFrameToolSettings
    {
        public ShipyardFrameToolSettings(int story, int snap, int newBase, int newStoryHeight, ShipDesignLimits limits)
        {
            Story = story;
            Snap = snap;
            NewBase = newBase;
            NewStoryHeight = newStoryHeight;
            Limits = limits;
        }

        public int Story { get; }
        public int Snap { get; }
        public int NewBase { get; }
        public int NewStoryHeight { get; }
        public ShipDesignLimits Limits { get; }
    }
}
