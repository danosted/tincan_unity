#nullable enable
using System;
using System.IO;
using UnityEngine;

namespace TinCan.DevTools
{
    /// <summary>The harness result for one instance, written as JSON under <c>Logs/net-telemetry/</c>.</summary>
    [Serializable]
    public class MovementTelemetryReport
    {
        public string role = string.Empty;
        public string route = string.Empty;
        public string preset = string.Empty;
        public string startedUtc = string.Empty;
        public bool routeCompleted;
        public float durationS;
        public int frames;
        public float avgFps;
        public float avgRttMs;
        public float maxRttMs;
        public ResponseSummary shipStill;
        public ResponseSummary shipMoving;
        public string notes =
            "Latencies are measured on the locally rendered pose in ship-local space. " +
            "Floors from movement physics alone: start ~40 ms (acceleration to 2 cm), stop ~175 ms (deceleration to half speed).";
    }

    public static class HarnessPaths
    {
        /// <summary>
        /// <c>&lt;project&gt;/Logs/net-telemetry</c>. MPPM additional editors run from <c>Library/VP/&lt;id&gt;</c>, so their
        /// reports land in the main project's folder too.
        /// </summary>
        public static string TelemetryDirectory() => TelemetryDirectory(Application.dataPath);

        public static string TelemetryDirectory(string dataPath)
        {
            string root = Path.GetFullPath(Path.Combine(dataPath, "..")).Replace('\\', '/');
            int clone = root.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            if (clone >= 0) root = root.Substring(0, clone);
            return Path.Combine(root, "Logs", "net-telemetry").Replace('\\', '/');
        }
    }
}
