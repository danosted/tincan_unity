#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>One line of a scenario report: when (seconds since the scenario started), what, and how it went.</summary>
    [Serializable]
    public struct ScenarioTimelineEntry
    {
        public float t;
        public string kind;
        public string label;
        public bool ok;
        public string detail;
    }

    /// <summary>
    /// Ordered record of a run on one peer. Steps and published domain events (see <see cref="ScenarioEventRecorder"/>)
    /// land in the same list, so the report reads as one story.
    /// </summary>
    public sealed class ScenarioTimeline
    {
        private const int MaxEntries = 2000;

        private readonly List<ScenarioTimelineEntry> _entries = new();
        private float _start = -1f;
        private float _now;

        public bool IsStarted => _start >= 0f;
        public float Elapsed => IsStarted ? _now - _start : 0f;
        public IReadOnlyList<ScenarioTimelineEntry> Entries => _entries;

        public void Start(float now)
        {
            _entries.Clear();
            _start = now;
            _now = now;
        }

        /// <summary>Advances the clock. Entries are stamped with the latest time given here.</summary>
        public void SetTime(float now) => _now = now;

        public void Add(string kind, string label, bool ok = true, string detail = "")
        {
            if (!IsStarted || _entries.Count >= MaxEntries) return;

            _entries.Add(new ScenarioTimelineEntry
            {
                t = (float)Math.Round(Elapsed, 3),
                kind = kind,
                label = label,
                ok = ok,
                detail = detail
            });
        }
    }
}
