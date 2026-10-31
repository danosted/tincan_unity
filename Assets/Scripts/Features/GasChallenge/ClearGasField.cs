#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Event action: despawn the gas field's pockets.</summary>
    public sealed class ClearGasField : IEventAction
    {
        public override string ToString() => "ClearGasField";
    }
}
