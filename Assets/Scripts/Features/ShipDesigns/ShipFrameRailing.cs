#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>What runs along a floor edge: nothing, a railing, or a gate (a railing with its middle left open).</summary>
    public enum ShipFrameRailing : byte
    {
        None,
        Rail,
        Gate,
    }
}
