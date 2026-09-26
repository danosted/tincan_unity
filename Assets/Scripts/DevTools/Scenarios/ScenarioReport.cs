#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>One peer's scenario result, written as <c>Logs/feature-telemetry/&lt;scenario&gt;/latest-&lt;role&gt;.json</c>.</summary>
    [Serializable]
    public class ScenarioReport
    {
        public string scenario = string.Empty;
        public string role = string.Empty;
        public string mode = string.Empty;
        public string preset = string.Empty;
        public string startedUtc = string.Empty;
        public float durationS;
        public string status = string.Empty;
        public bool passed;
        public List<string> failures = new();
        public List<ScenarioExpectation> expectations = new();
        public List<string> checkpoints = new();
        public List<ScenarioTimelineEntry> timeline = new();
    }

    /// <summary>
    /// The one file an agent reads after a run: every peer's verdict and where its full report is. Written by the
    /// host once every peer has reported (or the wait timed out, which counts as a failure).
    /// </summary>
    [Serializable]
    public class ScenarioSummary
    {
        public string scenario = string.Empty;
        public string mode = string.Empty;
        public string finishedUtc = string.Empty;
        public bool passed;
        public List<ScenarioPeerVerdict> peers = new();
    }

    [Serializable]
    public struct ScenarioPeerVerdict
    {
        public string role;
        public string status;
        public bool passed;
        public List<string> failures;
        public string report;
    }

    public static class ScenarioPaths
    {
        public const string SummaryFile = "latest-summary.json";

        /// <summary><c>&lt;project&gt;/Logs/feature-telemetry/&lt;scenario&gt;</c>, shared by MPPM clones (see <see cref="HarnessPaths"/>).</summary>
        public static string ScenarioDirectory(string scenario) => ScenarioDirectory(Application.dataPath, scenario);

        public static string ScenarioDirectory(string dataPath, string scenario) =>
            Path.Combine(HarnessPaths.ProjectRoot(dataPath), "Logs", "feature-telemetry", scenario).Replace('\\', '/');

        public static string ReportFile(string role) => $"latest-{role}.json";

        /// <summary>Screenshots go in a per-role folder that is emptied at the start of each run, so it only ever holds the latest run.</summary>
        public static string CaptureDirectory(string scenarioDirectory, string role) =>
            Path.Combine(scenarioDirectory, role).Replace('\\', '/');
    }
}
