#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// One story's generated hull: its plank floors and its beams (floor edges, posts rising from it, node blocks), with a
    /// fingerprint of the frame data they come from, so a rebuild can skip stories that did not change.
    /// </summary>
    public sealed class ShipFrameStoryMesh
    {
        public ShipFrameStoryMesh(int story, ulong fingerprint, ShipFrameMesh planks, ShipFrameMesh beams)
        {
            Story = story;
            Fingerprint = fingerprint;
            Planks = planks;
            Beams = beams;
        }

        public int Story { get; }
        public ulong Fingerprint { get; }
        public ShipFrameMesh Planks { get; }
        public ShipFrameMesh Beams { get; }
    }
}
