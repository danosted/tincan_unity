#nullable enable
namespace TinCan.Features.GrappleHook
{
    /// <summary>What one reel step did: still pulling, arrived at the hook point, or the line snapped (too far away).</summary>
    public enum GrappleReelOutcome
    {
        Reeling,
        Arrived,
        Snapped
    }
}
