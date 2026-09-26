#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.DevTools
{
    /// <summary>Collects latency samples in milliseconds and summarises them.</summary>
    public sealed class LatencyStats
    {
        private readonly List<float> _samples = new();

        public int Count => _samples.Count;
        public int Timeouts { get; private set; }

        public void Add(float milliseconds) => _samples.Add(milliseconds);
        public void AddTimeout() => Timeouts++;

        public float Mean
        {
            get
            {
                if (_samples.Count == 0) return 0f;
                float sum = 0f;
                foreach (var sample in _samples) sum += sample;
                return sum / _samples.Count;
            }
        }

        public float Max
        {
            get
            {
                float max = 0f;
                foreach (var sample in _samples) max = Math.Max(max, sample);
                return max;
            }
        }

        /// <summary>Nearest-rank percentile, <paramref name="percent"/> in [0, 100].</summary>
        public float Percentile(float percent)
        {
            if (_samples.Count == 0) return 0f;

            var sorted = new List<float>(_samples);
            sorted.Sort();
            int rank = (int)Math.Ceiling(percent / 100f * sorted.Count);
            return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
        }

        public LatencySummary Summarise() => new()
        {
            n = Count,
            timeouts = Timeouts,
            meanMs = Round(Mean),
            p50Ms = Round(Percentile(50f)),
            p95Ms = Round(Percentile(95f)),
            maxMs = Round(Max)
        };

        private static float Round(float value) => (float)Math.Round(value, 1);
    }

    /// <summary>JSON-friendly latency summary (lower-case fields keep the report compact and diff-able).</summary>
    [Serializable]
    public struct LatencySummary
    {
        public int n;
        public int timeouts;
        public float meanMs;
        public float p50Ms;
        public float p95Ms;
        public float maxMs;

        public override string ToString() => n == 0 && timeouts == 0
            ? "-"
            : $"p50 {p50Ms:0} / p95 {p95Ms:0} / max {maxMs:0} ms (n={n}{(timeouts > 0 ? $", {timeouts} timeouts" : string.Empty)})";
    }
}
