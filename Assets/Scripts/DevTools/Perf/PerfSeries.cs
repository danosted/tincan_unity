#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.DevTools.Perf
{
    /// <summary>
    /// One measured value over the perf window (a per-frame cost, or a per-second rate). Adding a sample does not
    /// allocate while the list has capacity, so the sampler can preallocate and record without disturbing the GC numbers
    /// it measures. Summaries sort a copy and are only taken when the window ends.
    /// </summary>
    public sealed class PerfSeries
    {
        private readonly List<float> _samples;

        public string Name { get; }
        public string Unit { get; }
        public int Count => _samples.Count;
        public IReadOnlyList<float> Samples => _samples;

        public PerfSeries(string name, string unit, int capacity = 0)
        {
            Name = name;
            Unit = unit;
            _samples = new List<float>(capacity);
        }

        public void Add(float value) => _samples.Add(value);

        public PerfSummary Summarise(float? hitchThreshold = null)
        {
            if (_samples.Count == 0) return new PerfSummary(Name, Unit, 0, 0f, 0f, 0f, 0f, 0f, 0f, 0);

            var sorted = new List<float>(_samples);
            sorted.Sort();
            double sum = 0d;
            foreach (var sample in _samples) sum += sample;

            int hitches = 0;
            if (hitchThreshold is { } threshold)
            {
                foreach (var sample in _samples)
                {
                    if (sample > threshold) hitches++;
                }
            }

            return new PerfSummary(Name, Unit, sorted.Count, (float)(sum / sorted.Count), Percentile(sorted, 50f),
                Percentile(sorted, 95f), Percentile(sorted, 99f), sorted[^1], (float)sum, hitches);
        }

        /// <summary>Nearest-rank percentile of an ascending list, <paramref name="percent"/> in [0, 100].</summary>
        public static float Percentile(IReadOnlyList<float> sorted, float percent)
        {
            if (sorted.Count == 0) return 0f;
            int rank = (int)Math.Ceiling(percent / 100f * sorted.Count);
            return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
        }

        /// <summary>
        /// Least-squares slope of the samples against their index (units per sample), skipping the first
        /// <paramref name="skip"/>. For a per-minute memory series this is growth per minute: about zero means no leak.
        /// </summary>
        public static float Slope(IReadOnlyList<float> samples, int skip = 0)
        {
            int n = samples.Count - skip;
            if (n < 2) return 0f;

            double meanX = (n - 1) / 2d;
            double meanY = 0d;
            for (int i = 0; i < n; i++) meanY += samples[skip + i];
            meanY /= n;

            double covariance = 0d;
            double variance = 0d;
            for (int i = 0; i < n; i++)
            {
                double dx = i - meanX;
                covariance += dx * (samples[skip + i] - meanY);
                variance += dx * dx;
            }

            return (float)(covariance / variance);
        }
    }

    /// <summary>The numbers a report keeps for one series.</summary>
    public readonly struct PerfSummary
    {
        public readonly string Name;
        public readonly string Unit;
        public readonly int Count;
        public readonly float Mean;
        public readonly float P50;
        public readonly float P95;
        public readonly float P99;
        public readonly float Max;
        public readonly float Total;
        /// <summary>Samples above the hitch threshold; 0 when the series has none.</summary>
        public readonly int Hitches;

        public PerfSummary(string name, string unit, int count, float mean, float p50, float p95, float p99, float max, float total, int hitches)
        {
            Name = name;
            Unit = unit;
            Count = count;
            Mean = mean;
            P50 = p50;
            P95 = p95;
            P99 = p99;
            Max = max;
            Total = total;
            Hitches = hitches;
        }
    }
}
