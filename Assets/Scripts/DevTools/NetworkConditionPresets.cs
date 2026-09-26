#nullable enable
using System;

namespace TinCan.DevTools
{
    /// <summary>
    /// Send-side conditions one peer applies to its own outgoing packets. Give both host and client the same preset:
    /// the round trip is then twice <see cref="SendDelayMs"/>.
    /// </summary>
    public readonly struct NetworkConditionPreset
    {
        public readonly string Name;
        public readonly uint SendDelayMs;
        public readonly uint SendJitterMs;
        public readonly float SendPacketLossPercent;

        public NetworkConditionPreset(string name, uint sendDelayMs, uint sendJitterMs, float sendPacketLossPercent)
        {
            Name = name;
            SendDelayMs = sendDelayMs;
            SendJitterMs = sendJitterMs;
            SendPacketLossPercent = sendPacketLossPercent;
        }

        public uint RoundTripMs => SendDelayMs * 2;

        public override string ToString() =>
            $"{Name} (~{RoundTripMs} ms RTT, ±{SendJitterMs} ms jitter, {SendPacketLossPercent}% loss per direction)";
    }

    public static class NetworkConditionPresets
    {
        public static readonly NetworkConditionPreset None = new("None", 0, 0, 0f);
        public static readonly NetworkConditionPreset Lag50 = new("Lag50", 25, 3, 0f);
        public static readonly NetworkConditionPreset Lag100 = new("Lag100", 50, 5, 1f);
        public static readonly NetworkConditionPreset Lag200 = new("Lag200", 100, 10, 2f);
        public static readonly NetworkConditionPreset Lossy = new("Lossy", 50, 15, 5f);

        private static readonly NetworkConditionPreset[] All = { None, Lag50, Lag100, Lag200, Lossy };

        public static bool TryGet(string? name, out NetworkConditionPreset preset)
        {
            foreach (var candidate in All)
            {
                if (!string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

                preset = candidate;
                return true;
            }

            preset = None;
            return false;
        }

        public static string Names => string.Join(", ", Array.ConvertAll(All, p => p.Name));
    }
}
