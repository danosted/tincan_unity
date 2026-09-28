#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Event condition: the ship has at most this many broken parts (0 = fully repaired).</summary>
    public sealed class BrokenPartsAtMost : IEventCondition
    {
        public BrokenPartsAtMost(int count) => Count = count;

        public int Count { get; }

        public override string ToString() => $"BrokenPartsAtMost {Count}";
    }
}
