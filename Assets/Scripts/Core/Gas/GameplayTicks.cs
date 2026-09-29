#nullable enable
using UnityEngine;

namespace TinCan.Core.Gas
{
    /// <summary>
    /// GAS measures time in simulation ticks: assets author seconds, and a duration becomes a whole number of ticks
    /// when it is applied. Owner and server then agree on which tick a window ends, whatever their clocks read.
    /// </summary>
    public static class GameplayTicks
    {
        /// <summary>Seconds to ticks, rounded up so a window is never shorter than authored (0.5 s at 30 Hz is 15).</summary>
        public static int FromSeconds(float seconds, int tickRate) =>
            seconds <= 0f ? 0 : Mathf.CeilToInt(seconds * tickRate - 1e-4f);
    }
}
