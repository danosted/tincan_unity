#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TinCan.DevTools.Perf
{
    /// <summary>
    /// One instance's perf report: what ran (role, label, build, frame cap), the measured window, a summary per series,
    /// and message counts by type. Written as JSON by hand, since the report holds maps JsonUtility cannot write. The
    /// format is the stable part of the perf pipeline: <c>.tools/perf.ps1</c> reads it. Field reference:
    /// .docs/PERFORMANCE.md, "Reading a report".
    /// </summary>
    public sealed class PerfReport
    {
        public const int FormatVersion = 1;

        private readonly List<KeyValuePair<string, string>> _info = new();
        private readonly List<PerfSummary> _metrics = new();
        private readonly List<string> _unavailable = new();
        private readonly SortedDictionary<string, long> _messagesSent = new();
        private readonly SortedDictionary<string, long> _messagesReceived = new();

        public string Role { get; }
        public float WindowSeconds { get; set; }
        public int Frames { get; set; }

        public PerfReport(string role) => Role = role;

        /// <summary>A descriptive field (label, platform, build flags, frame cap...).</summary>
        public void AddInfo(string key, string value) => _info.Add(new KeyValuePair<string, string>(key, value));

        public void AddMetric(PerfSummary summary) => _metrics.Add(summary);

        /// <summary>A counter this player does not expose (most need a development build).</summary>
        public void AddUnavailable(string counter) => _unavailable.Add(counter);

        public void AddMessages(string type, long sent, long received)
        {
            if (sent > 0) _messagesSent[type] = sent;
            if (received > 0) _messagesReceived[type] = received;
        }

        public IReadOnlyList<PerfSummary> Metrics => _metrics;

        public string ToJson()
        {
            var json = new StringBuilder(4096);
            json.Append("{\n");
            Field(json, "format", FormatVersion.ToString(CultureInfo.InvariantCulture), quote: false);
            Field(json, "role", Role);
            foreach (var pair in _info) Field(json, pair.Key, pair.Value);
            Field(json, "windowS", Number(WindowSeconds), quote: false);
            Field(json, "frames", Frames.ToString(CultureInfo.InvariantCulture), quote: false);

            json.Append("  \"metrics\": {");
            for (int i = 0; i < _metrics.Count; i++)
            {
                var m = _metrics[i];
                json.Append(i == 0 ? "\n" : ",\n");
                json.Append("    \"").Append(Escape(m.Name)).Append("\": { \"unit\": \"").Append(Escape(m.Unit)).Append('"')
                    .Append(", \"n\": ").Append(m.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"mean\": ").Append(Number(m.Mean))
                    .Append(", \"p50\": ").Append(Number(m.P50))
                    .Append(", \"p95\": ").Append(Number(m.P95))
                    .Append(", \"p99\": ").Append(Number(m.P99))
                    .Append(", \"max\": ").Append(Number(m.Max))
                    .Append(", \"total\": ").Append(Number(m.Total))
                    .Append(", \"hitches\": ").Append(m.Hitches.ToString(CultureInfo.InvariantCulture))
                    .Append(" }");
            }
            json.Append(_metrics.Count > 0 ? "\n  },\n" : "},\n");

            Map(json, "messagesSent", _messagesSent);
            Map(json, "messagesReceived", _messagesReceived);

            json.Append("  \"unavailable\": [");
            for (int i = 0; i < _unavailable.Count; i++)
            {
                if (i > 0) json.Append(", ");
                json.Append('"').Append(Escape(_unavailable[i])).Append('"');
            }
            json.Append("]\n}\n");
            return json.ToString();
        }

        /// <summary>One line for the log: the numbers people look at first.</summary>
        public string ToSummaryLine()
        {
            var line = new StringBuilder();
            line.Append(Role).Append(' ').Append(Number(WindowSeconds)).Append(" s, ").Append(Frames).Append(" frames");
            foreach (var m in _metrics)
            {
                line.Append(" | ").Append(m.Name).Append(" p50 ").Append(Number(m.P50)).Append(" p95 ").Append(Number(m.P95))
                    .Append(" max ").Append(Number(m.Max));
                if (m.Hitches > 0) line.Append(" hitches ").Append(m.Hitches);
            }
            return line.ToString();
        }

        private static void Field(StringBuilder json, string key, string value, bool quote = true)
        {
            json.Append("  \"").Append(Escape(key)).Append("\": ");
            if (quote) json.Append('"').Append(Escape(value)).Append('"');
            else json.Append(value);
            json.Append(",\n");
        }

        private static void Map(StringBuilder json, string key, SortedDictionary<string, long> values)
        {
            json.Append("  \"").Append(key).Append("\": {");
            bool first = true;
            foreach (var pair in values)
            {
                json.Append(first ? " " : ", ");
                json.Append('"').Append(Escape(pair.Key)).Append("\": ").Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                first = false;
            }
            json.Append(first ? "},\n" : " },\n");
        }

        public static string Number(float value) =>
            float.IsFinite(value) ? System.Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture) : "0";

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
