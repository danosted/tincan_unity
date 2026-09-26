#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Features.HumanoidMovement;
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
        public List<ServerInputSummary> serverInputs = new();
        public PredictionSummary prediction;
        public string notes =
            "Latencies are measured on the locally rendered pose in ship-local space. " +
            "Floors from movement physics alone: start ~40 ms (acceleration to 2 cm), stop ~175 ms (deceleration to half speed).";
    }

    /// <summary>
    /// Server view of one remote player's input stream. Healthy means starved and skipped stay near zero and the
    /// mean depth sits around one.
    /// </summary>
    [Serializable]
    public struct ServerInputSummary
    {
        public string player;
        public int ticks;
        public int received;
        public int consumed;
        public int starved;
        public int skipped;
        public int maxDepth;
        public float meanDepth;

        public static ServerInputSummary From(string player, HumanoidInputBufferStats stats) => new()
        {
            player = player,
            ticks = stats.Ticks,
            received = stats.Received,
            consumed = stats.Consumed,
            starved = stats.Starved,
            skipped = stats.Skipped,
            maxDepth = stats.MaxDepth,
            meanDepth = (float)Math.Round(stats.MeanDepth, 2)
        };

        public override string ToString() =>
            $"{player}: ticks {ticks}, starved {starved}, skipped {skipped}, depth avg {meanDepth:0.0} max {maxDepth}";
    }

    /// <summary>
    /// Owner-side prediction health. Healthy means almost every ack matches, corrections are rare and a few cm, and
    /// there are no snaps outside teleports. <c>ackLatencyMs</c> is input sent → server confirms it applied that input:
    /// round trip plus the server's input buffer.
    /// </summary>
    [Serializable]
    public struct PredictionSummary
    {
        public int acks;
        public int matches;
        public int corrections;
        public int snaps;
        public float meanCorrectionM;
        public float maxCorrectionM;
        public float ackLatencyMs;
        public float maxAckLatencyMs;

        public static PredictionSummary From(HumanoidPredictionStats stats) => new()
        {
            acks = stats.Acks,
            matches = stats.Matches,
            corrections = stats.Corrections,
            snaps = stats.Snaps,
            meanCorrectionM = (float)Math.Round(stats.MeanCorrection, 3),
            maxCorrectionM = (float)Math.Round(stats.MaxCorrection, 3),
            ackLatencyMs = (float)Math.Round(stats.MeanAckLatency, 1),
            maxAckLatencyMs = (float)Math.Round(stats.AckLatencyMax, 1)
        };

        public override string ToString() => acks == 0
            ? "prediction -"
            : $"acks {acks}: match {matches}, correct {corrections} (avg {meanCorrectionM * 100f:0.0} cm, max {maxCorrectionM * 100f:0.0} cm), snap {snaps} | ack {ackLatencyMs:0} ms";
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
