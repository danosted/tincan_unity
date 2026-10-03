#nullable enable
using System.Collections.Generic;
using System.Globalization;
using TinCan.Core.Domain;

namespace TinCan.DevTools.Perf
{
    /// <summary>
    /// What the perf sampler should do in this instance, read from <see cref="LaunchArguments"/>:
    /// <c>-perf</c> turns it on; <c>-perfrole &lt;name&gt;</c> names the report (default: server, host or client&lt;id&gt;);
    /// <c>-perfwarmup &lt;s&gt;</c> and <c>-perfduration &lt;s&gt;</c> set the measured window after the session starts;
    /// <c>-perfout &lt;dir&gt;</c> is where the report goes (default <c>&lt;project&gt;/Logs/perf</c>);
    /// <c>-perflabel &lt;text&gt;</c> names the load (a scenario such as CrewLoad); <c>-perfquit</c> quits once the
    /// report is written, so a container or a scheduled run ends by itself; <c>-perfprofile &lt;frames&gt;</c> also records a
    /// Unity profiler capture (<c>&lt;role&gt;.raw</c> next to the report) of the first frames of the window, for finding
    /// what costs (profiling skews the numbers, so such a run is a diagnosis, not a measurement).
    /// See .docs/PERFORMANCE.md.
    /// </summary>
    public sealed class PerfOptions
    {
        public const string PerfFlag = "-perf";
        public const string RoleFlag = "-perfrole";
        public const string WarmupFlag = "-perfwarmup";
        public const string DurationFlag = "-perfduration";
        public const string OutputFlag = "-perfout";
        public const string LabelFlag = "-perflabel";
        public const string QuitFlag = "-perfquit";
        public const string ProfileFlag = "-perfprofile";

        public const float DefaultWarmupSeconds = 20f;
        public const float DefaultDurationSeconds = 90f;

        public bool Enabled { get; }
        public string? Role { get; }
        public float WarmupSeconds { get; }
        public float DurationSeconds { get; }
        public string? OutputDirectory { get; }
        public string Label { get; }
        public bool QuitWhenDone { get; }
        /// <summary>Frames of the window to record into a profiler capture; 0 records none.</summary>
        public int ProfileFrames { get; }

        public PerfOptions(bool enabled, string? role = null, float warmupSeconds = DefaultWarmupSeconds,
            float durationSeconds = DefaultDurationSeconds, string? outputDirectory = null, string? label = null, bool quitWhenDone = false, int profileFrames = 0)
        {
            Enabled = enabled;
            Role = role;
            WarmupSeconds = warmupSeconds < 0f ? 0f : warmupSeconds;
            DurationSeconds = durationSeconds <= 0f ? DefaultDurationSeconds : durationSeconds;
            OutputDirectory = outputDirectory;
            Label = string.IsNullOrWhiteSpace(label) ? "none" : label!;
            QuitWhenDone = quitWhenDone;
            ProfileFrames = profileFrames < 0 ? 0 : profileFrames;
        }

        public static PerfOptions Parse(IReadOnlyList<string> args)
        {
            bool enabled = LaunchArguments.HasFlag(args, PerfFlag);
            if (!enabled) return new PerfOptions(false);

            return new PerfOptions(
                true,
                LaunchArguments.TryGetValue(args, RoleFlag, out var role) ? role : null,
                ParseSeconds(args, WarmupFlag, DefaultWarmupSeconds),
                ParseSeconds(args, DurationFlag, DefaultDurationSeconds),
                LaunchArguments.TryGetValue(args, OutputFlag, out var output) ? output : null,
                LaunchArguments.TryGetValue(args, LabelFlag, out var label) ? label : null,
                LaunchArguments.HasFlag(args, QuitFlag),
                LaunchArguments.TryGetValue(args, ProfileFlag, out var frames) && int.TryParse(frames, out var count) ? count : 0);
        }

        private static float ParseSeconds(IReadOnlyList<string> args, string flag, float fallback) =>
            LaunchArguments.TryGetValue(args, flag, out var value) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : fallback;
    }
}
