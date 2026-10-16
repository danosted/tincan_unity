#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>What fills a frame face: nothing (its edges are beams around a hole) or wooden planks.</summary>
    public enum ShipFrameFill : byte
    {
        Open = 0,
        Planks = 1,
    }
}
