#nullable enable
namespace TinCan.Features.GrappleHook
{
    /// <summary>What the holder aims at: nothing in sight, a surface the hook would catch, or one beyond its range.</summary>
    public enum GrappleAimReach
    {
        None,
        InRange,
        OutOfRange
    }
}
