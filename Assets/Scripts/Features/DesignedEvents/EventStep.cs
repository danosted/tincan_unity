#nullable enable
namespace TinCan.Features.DesignedEvents
{
    /// <summary>What the running event does this tick (<see cref="EventRunProcessor"/>).</summary>
    public enum EventStep
    {
        Stay,
        NextPhase,
        Succeed,
        Fail
    }
}
