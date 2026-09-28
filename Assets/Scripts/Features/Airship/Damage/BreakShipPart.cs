#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Event action: break one damage point on the ship, by index.</summary>
    public sealed class BreakShipPart : IEventAction
    {
        public BreakShipPart(int index) => Index = index;

        public int Index { get; }

        public override string ToString() => $"BreakShipPart {Index}";
    }
}
