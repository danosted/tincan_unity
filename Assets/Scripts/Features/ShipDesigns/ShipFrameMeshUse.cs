#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>What a generated mesh is for: seen and collided with (planks, beams), only seen (stair steps), or only collided with (stair ramps).</summary>
    public enum ShipFrameMeshUse
    {
        Solid,
        Seen,
        Collider,
    }
}
