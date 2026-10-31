#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Event action: lay a gas field ahead of the ship (<see cref="IGasField.Spawn"/>).</summary>
    public sealed class SpawnGasField : IEventAction
    {
        public SpawnGasField(int count, float ahead, float spread)
        {
            Count = count;
            Ahead = ahead;
            Spread = spread;
        }

        public int Count { get; }
        public float Ahead { get; }
        public float Spread { get; }

        public override string ToString() => $"SpawnGasField {Count} pockets {Ahead:0} m ahead, {Spread:0} m spread";
    }
}
