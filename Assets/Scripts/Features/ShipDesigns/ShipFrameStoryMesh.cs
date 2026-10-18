#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// One story's generated hull: its plank floors and walls, its beams and railings (floor edges, posts rising from it,
    /// node blocks), the steps of the stairs standing in it (seen, not collided with) and their ramps (collided with, not
    /// seen), with a fingerprint of the frame data they come from, so a rebuild can skip stories that did not change.
    /// </summary>
    public sealed class ShipFrameStoryMesh
    {
        public ShipFrameStoryMesh(int story, ulong fingerprint, ShipFrameMesh planks, ShipFrameMesh beams, ShipFrameMesh steps, ShipFrameMesh ramps)
        {
            Story = story;
            Fingerprint = fingerprint;
            Planks = planks;
            Beams = beams;
            Steps = steps;
            Ramps = ramps;
        }

        public int Story { get; }
        public ulong Fingerprint { get; }
        public ShipFrameMesh Planks { get; }
        public ShipFrameMesh Beams { get; }
        public ShipFrameMesh Steps { get; }
        public ShipFrameMesh Ramps { get; }
    }
}
