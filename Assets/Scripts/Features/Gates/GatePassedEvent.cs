#nullable enable
namespace TinCan.Features.Gates
{
    /// <summary>The ship passed through a gate (server).</summary>
    public readonly struct GatePassedEvent
    {
        /// <summary>The gate's definition (Gate_Boon_SpeedBurst).</summary>
        public readonly string Gate;
        public readonly int Passed;

        public GatePassedEvent(string gate, int passed)
        {
            Gate = gate;
            Passed = passed;
        }
    }
}
