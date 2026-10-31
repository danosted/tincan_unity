#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Event condition: every pocket of the field has gone off or been left behind (<see cref="IGasField.IsCleared"/>).</summary>
    public sealed class GasFieldCleared : IEventCondition
    {
        public override string ToString() => "GasFieldCleared";
    }
}
